using Cysharp.Threading.Tasks.Triggers;
using PhyData;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using UnityEngine;

public abstract class BasePhysicsEntity : MonoBehaviour,IPhysicalconstraint
{
    /// <summary>
    /// 编辑器画图显示碰撞检测范围
    /// </summary>
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (groundV == null) return;
        if (leftV == null) return;
        if (rightV == null) return;
        // 设置颜色：绿色半透明，便于观察
        Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.5f);

        // 绘制检测区域的线框矩形（位置、大小、旋转）
        Gizmos.DrawWireCube(groundV.position, cPhysics.boxCastH);
        Gizmos.DrawWireCube(leftV.position, cPhysics.boxCastV);
        Gizmos.DrawWireCube(rightV.position, cPhysics.boxCastV);
    }
#endif
    #region 必要组件
    protected Rigidbody2D rb; //刚体
    protected BoxCollider2D boxCollider;//碰撞器
    /// 物理配置数据
    /// </summary>
    [SerializeField]
    protected SO_CPhysics cPhysics;
    /// <summary>
    /// 地面检测中心（外部拖拽关联
    /// </summary>
    [SerializeField]
    protected Transform groundV;
    /// <summary>
    /// 左墙检测
    /// </summary>
    [SerializeField]
    protected Transform leftV;
    /// <summary>
    /// 右墙检测
    /// </summary>
    [SerializeField]
    protected Transform rightV;
    #endregion
    #region 运行时数据
    //实时数据(外部修改时传入（如物理命令
    protected PlayerPhysicsData playerPhysicsData;
    //只读数据包装
    public ReadOnly_PlayerPhysicsData readOnly_playerPhysicsData;

    protected BaseGround lastFrameGroundPlatform = null;  //缓存上一帧的平台
    /// <summary>
    /// 持续性环境物理受限状态容器（左右移动速度
    /// </summary>
    Dictionary<StringBuilder, float> phyStateDic = new Dictionary<StringBuilder, float>();
    /// <summary>
    /// 时间受力容器
    /// </summary>
    List<SpeedStackData> UnderForceList=new List<SpeedStackData>();
    /// <summary>
    /// 状态受力容器
    /// </summary>
    Dictionary<StringBuilder,Vector2> startForceDic = new Dictionary<StringBuilder,Vector2>();
    /// <summary>
    /// 物理约束情况改变时才重算（脏标识
    /// </summary>
    bool isRecalculate;
    #endregion

    protected virtual void Awake()
    {
        if (cPhysics == null)
        {
            print("SO_玩家物理配置数据为空，请拖拽引用");
        }
        //必要组件关联
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        //不使用刚体的重力
        rb.gravityScale = 0;
        //实时物理状态信息初始化
        playerPhysicsData = new PlayerPhysicsData();
        readOnly_playerPhysicsData = new(playerPhysicsData);
    }

    protected virtual void OnEnable()
    {
        //物理更新时序为1层
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(FixFun, cPhysics.phyMask);
    }
    /// <summary>
    /// 物理帧更新逻辑
    /// </summary>
    void FixFun()
    {
        // 地面检测（只做检测，不做响应）
        RaycastHit2D hit = Physics2D.BoxCast(groundV.position, cPhysics.boxCastH, 0, Vector2.down, 0f, cPhysics.groundLayer);
        BaseGround currentGroundPlatform = null;
        bool onGroundNow=false;
        //一定角度内正对碰撞才算着地
        if (hit.collider != null && Vector2.Dot(hit.normal, Vector2.up) > 0.7f)
        {
            onGroundNow = true;
            //下文需要判空处理，空表示无任何特殊逻辑的地面
            currentGroundPlatform = hit.collider.GetComponent<BaseGround>();
        }
        // 比对状态，只在地面改变时调用
        if (currentGroundPlatform != lastFrameGroundPlatform)
        {
            // 离开上一个平台
            if (lastFrameGroundPlatform != null)
            {
                lastFrameGroundPlatform.OutObjPhy(gameObject.GetInstanceID());
            }

            // 进入新平台
            if (currentGroundPlatform != null)
            {
                currentGroundPlatform.SetObjToPhyList(gameObject.GetInstanceID(), this);
            }
            // 记录本帧状态，供下帧对比
            lastFrameGroundPlatform = currentGroundPlatform;
        }
        // 更新数据
        playerPhysicsData.nowtaijie = currentGroundPlatform;
        playerPhysicsData.isGrounded=onGroundNow;
        //检测左右靠墙
        playerPhysicsData.onLeftWall = Physics2D.BoxCast(leftV.position, cPhysics.boxCastV, 0, Vector2.left, 0f, cPhysics.wallLayer);
        playerPhysicsData.onRightWall = Physics2D.BoxCast(rightV.position, cPhysics.boxCastV, 0, Vector2.right, 0f, cPhysics.wallLayer);

        //处理基础移动（赋值操作，最先）
        playerPhysicsData.horizontalSpeed = HorizontalSpeedCalculation() * (1 + PhyStateCalculate());
        //统一事件触发（控制时序在物理检测和赋值操作之后
        PhyEventUpdate();
        //处理受力情况
        Vector2 nowforce= UnderForce();
        //水平速度影响
        playerPhysicsData.horizontalSpeed += nowforce.x;//叠加操作
        // 重力
        if (!playerPhysicsData.isGrounded)
        {
            playerPhysicsData.verticalSpeed += cPhysics.gravity * Time.fixedDeltaTime;     //纵向速度改变（增减处理
        }
        else if (playerPhysicsData.verticalSpeed < 0)
        {
            playerPhysicsData.verticalSpeed = 0;
        }
        //垂直变速（赋值或者累加操作
        VerticalTransmission();
        //竖直速度影响
        playerPhysicsData.verticalSpeed += nowforce.y;
        //计算当前速度向量与移动距离
        Vector2 velocity = new Vector2(playerPhysicsData.horizontalSpeed, playerPhysicsData.verticalSpeed);

        Vector2 moveDelta = velocity * Time.fixedDeltaTime;             //计算玩家相对位移
        //计算平台补偿位移
        Vector2 platformDelta = Vector2.zero;
        //使用于斜率位移修正
        if (playerPhysicsData.nowtaijie != null && playerPhysicsData.isGrounded)
        {
            platformDelta = playerPhysicsData.nowtaijie.Delta;
            //此处斜率补正
            moveDelta = new Vector2(0, playerPhysicsData.verticalSpeed * Time.fixedDeltaTime);

            float moveAmount = playerPhysicsData.horizontalSpeed * Time.fixedDeltaTime;

            Vector2 tangent = new Vector2(hit.normal.y, -hit.normal.x);
            //判断方向是否一致
            if (Mathf.Sign(tangent.x) != Mathf.Sign(playerPhysicsData.horizontalSpeed))
                tangent *= -1;

            //Debug.Log(tangent);
            //此处表示，沿斜坡移动的速度和水平速度一致
            Vector2 slopeMove = tangent.normalized * Mathf.Abs(moveAmount);

            moveDelta += slopeMove;
        }

        Vector2 wordDelta = moveDelta + platformDelta;      //计算世界绝对位移
        //计算世界物理位移受限
        if (wordDelta.x < 0 && playerPhysicsData.onLeftWall || wordDelta.x > 0 && playerPhysicsData.onRightWall)
        {
            wordDelta.x = 0;
        }
        //  一次性统一移动
        rb.MovePosition(rb.position + wordDelta);
    }

    /// <summary>
    /// 统一计算持续性物理环境影响移动的参数（快照重算模式
    /// </summary>
    float PhyStateCalculate()
    {
        if (isRecalculate)
        {
            float phyEvData = 0;

            foreach (var i in phyStateDic.Values)
            {
                phyEvData += i;
            }
            //最终影响倍率不能在范围之外
            playerPhysicsData.nowPhyNum = Mathf.Clamp(phyEvData, -0.95f, 4f);//此处可配置=================================
        }
        return playerPhysicsData.nowPhyNum;
    }
    /// <summary>
    /// 最先执行的水平速度赋值操作
    /// </summary>
    /// <param name="horizontalSpeed"></param>
    protected abstract float HorizontalSpeedCalculation();

    /// <summary>
    /// 统一消费物理事件更新逻辑
    /// </summary>
    protected abstract void PhyEventUpdate();

    /// <summary>
    /// 统一计算受力带来的速度改变
    /// </summary>
    protected Vector2 UnderForce()
    {
        Vector2 sp = Vector2.zero;
        //先计算时间力
        // 必须反向遍历，因为要操作删除
        for (int i = UnderForceList.Count - 1; i >= 0; i--)
        {
            if(UnderForceList[i].responseTimer1 < Time.time)//表示此力过期
            {
                int lastI = UnderForceList.Count - 1;
                if (i != lastI)//不在乎顺序，换位删除，提升性能
                {
                    UnderForceList[i] = UnderForceList[lastI];
                }
                UnderForceList.RemoveAt(lastI);
            }
            else
            {
                sp += UnderForceList[i].speedStack;
            }
        }
        //再计算状态力
        if (isRecalculate)
        {
            foreach (var i in startForceDic.Values)
            {
                sp += i;
            }
        }
        return sp;
    }

    /// <summary>
    /// 处理特殊行为的垂直变速（例如贴墙下滑，攀岩
    /// </summary>
    /// <param name="v"></param>
    protected abstract void VerticalTransmission();
    protected virtual void OnDisable()
    {
        //事件注销
        if (!MonoPublicMgr.IsQuitting)
        {
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(FixFun, cPhysics.phyMask);
        }

        lastFrameGroundPlatform?.OutObjPhy(gameObject.GetInstanceID());
        lastFrameGroundPlatform = null;
        playerPhysicsData.nowtaijie?.OutObjPhy(gameObject.GetInstanceID());
    }

    public void OnPhyEnter(StringBuilder iD, float num)
    {
        //避免键重复而报错
        phyStateDic[iD] = num;
        isRecalculate = true;
    }
    public void OnPhyExit(StringBuilder iD)
    {
        phyStateDic.Remove(iD);
        isRecalculate = true;
    }

    public void AddSpeed(float time,Vector2 force)
    {
        SpeedStackData data = new SpeedStackData();
        data.responseTimer1= Time.time+time;
        data.speedStack=force;
        UnderForceList.Add(data);
    }

    public void AddForce(StringBuilder iD, Vector2 force)
    {
        startForceDic[iD] = force;
        isRecalculate = true;
    }

    public void RemoveForce(StringBuilder iD)
    {
        startForceDic.Remove(iD);
        isRecalculate = true;
    }
}
