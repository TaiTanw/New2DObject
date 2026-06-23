using Cysharp.Threading.Tasks.Triggers;
using PhyData;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using UnityEngine;
using System.Runtime.InteropServices;

public abstract class BasePhysicsEntity : MonoBehaviour
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
    //实时数据(外部修改时传入
    protected PlayerPhysicsData playerPhysicsData;
    //只读数据包装
    ReadOnly_PlayerPhysicsData readOnly_playerPhysicsData;
    public ReadOnly_PlayerPhysicsData ReadOnly_PlayerPhyData => readOnly_playerPhysicsData;

    protected BaseGround lastFrameGroundPlatform = null;  //缓存上一帧的平台
    /// <summary>
    /// 持续性环境物理受限状态容器（左右移动速度（粘滞力
    /// </summary>
    Dictionary<BasicPhysicalObject, float> phyStateDic = new Dictionary<BasicPhysicalObject, float>();
    /// <summary>
    /// 时间速度容器
    /// </summary>
    List<SpeedStackData> UnderForceList=new List<SpeedStackData>();
    /// <summary>
    /// 状态速度容器（传送带模型
    /// </summary>
    Dictionary<BasicPhysicalObject, Vector2> startForceDic = new Dictionary<BasicPhysicalObject, Vector2>();
    /// <summary>
    /// 受力情况只读容器
    /// </summary>
    public Dictionary<BasicPhysicalObject, Vector2> StartForceDic=>startForceDic;
    /// <summary>
    /// 受到哪些物体的施力影响
    /// </summary>
    HashSet<BasicPhysicalObject> delayedRemoveForce = new HashSet<BasicPhysicalObject>();
    /// <summary>
    /// 受力计算容器
    /// </summary>
    Dictionary<BasicPhysicalObject,ForceData> ForceDic=new Dictionary<BasicPhysicalObject,ForceData>();
    /// <summary>
    /// 自身阻力系数
    /// </summary>
    float self_resistanceCoefficient = 3;
    /// <summary>
    /// 基础水平速度
    /// </summary>
    public float speed => cPhysics.speed;
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
        bool noNullGround=false;
        BaseGround currentGroundPlatform = null;
        bool onGroundNow=false;
        //默认的空中阻力系数
        self_resistanceCoefficient = 1;
        //一定角度内正对碰撞才算着地
        if (hit.collider != null && Vector2.Dot(hit.normal, Vector2.up) > 0.7f)
        {
            onGroundNow = true;
            //在地面，则自身阻力增大
            self_resistanceCoefficient = 20;
            //下文需要判空处理，空表示无任何特殊逻辑的地面
            noNullGround = hit.collider.TryGetComponent<BaseGround>(out currentGroundPlatform);
        }
        // 比对状态，只在地面改变时调用
        if (currentGroundPlatform != lastFrameGroundPlatform)//简易理解为状态（同时只能在一种地面上）
        {
            // 离开上一个平台
            if (lastFrameGroundPlatform != null)
            {
                lastFrameGroundPlatform.OutObjPhy(this);
            }

            // 进入新平台
            if (currentGroundPlatform != null)
            {
                currentGroundPlatform.SetObjToPhyList(this);
            }
            // 记录本帧状态，供下帧对比
            lastFrameGroundPlatform = currentGroundPlatform;
        }
        // 更新数据
        playerPhysicsData.nowtaijie = currentGroundPlatform;
        playerPhysicsData.isGrounded=onGroundNow;
        //检测左右靠墙
        RaycastHit2D hit1 = Physics2D.BoxCast(leftV.position, cPhysics.boxCastV, 0, Vector2.left, 0f, cPhysics.wallLayer);
        RaycastHit2D hit2 = Physics2D.BoxCast(rightV.position, cPhysics.boxCastV, 0, Vector2.right, 0f, cPhysics.wallLayer);
        //检测墙是否有特殊逻辑（无特殊逻辑则表示无法贴墙下滑
        playerPhysicsData.onLeftWall = false;
        playerPhysicsData.canLeftWall = null;
        if (hit1.collider != null)
        {
            playerPhysicsData.onLeftWall = true;
            hit1.collider.TryGetComponent<Wall>(out playerPhysicsData.canLeftWall);
        }
        playerPhysicsData.onRightWall = false;
        playerPhysicsData.canRightWall = null;
        if (hit2.collider != null)
        {
            playerPhysicsData.onRightWall = true;
            hit2.collider.TryGetComponent<Wall>(out playerPhysicsData.canRightWall);
        }

        //处理基础移动（赋值操作，最先）
        playerPhysicsData.horizontalSpeed = HorizontalSpeedCalculation() * (1 + PhyStateCalculate());
        //统一事件触发（控制时序在物理检测和赋值操作之后
        PhyEventUpdate();
        //处理受力情况
        UnderForce();
        //所有物理受力，速度计算完成，重置标识
        //isRecalculate = false;
        //水平速度影响
        //playerPhysicsData.horizontalSpeed += nowforce.x;//叠加操作
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
        //playerPhysicsData.verticalSpeed += nowforce.y;
        //计算当前速度向量与移动距离
        float nowHSpeed = playerPhysicsData.horizontalSpeed + playerPhysicsData.phyHSpeed;
        Vector2 velocity = new Vector2(nowHSpeed,
                                        playerPhysicsData.verticalSpeed + playerPhysicsData.phyVSpeed);

        Vector2 moveDelta = velocity * Time.fixedDeltaTime;             //计算玩家相对位移
        //计算平台补偿位移
        Vector2 platformDelta = Vector2.zero;
        //使用于斜率位移修正
        if (noNullGround && playerPhysicsData.isGrounded)
        {
            platformDelta = playerPhysicsData.nowtaijie.Delta;
            //此处斜率补正
            moveDelta = new Vector2(0, playerPhysicsData.verticalSpeed * Time.fixedDeltaTime);
            //使用总速度
            float moveAmount = nowHSpeed * Time.fixedDeltaTime;

            Vector2 tangent = new Vector2(hit.normal.y, -hit.normal.x);
            //判断方向是否一致（sign返回正负性
            if (Mathf.Sign(tangent.x) != Mathf.Sign(nowHSpeed))
                tangent *= -1;

            //Debug.Log(tangent);
            //此处表示，沿斜坡移动的速度和水平速度一致
            Vector2 slopeMove = tangent.normalized * Mathf.Abs(moveAmount);

            moveDelta += slopeMove;
        }

        Vector2 wordDelta = moveDelta + platformDelta;      //计算世界绝对位移
        //计算世界物理位移受限(防止过度挤压出现奇怪问题
        if (wordDelta.x < 0 && playerPhysicsData.onLeftWall )
        {
            //横向速度制0
            wordDelta.x = 0;
            //碰墙后需要清空时间力，但状态力生命周期严格由施力物体控制，此处若清空状态力会导致问题
            UnderForceList.Clear();
            playerPhysicsData.nowWall=playerPhysicsData.canLeftWall;
        }
        else if (wordDelta.x > 0 && playerPhysicsData.onRightWall)
        {
            wordDelta.x = 0;
            UnderForceList.Clear();
            //设置当前靠墙
            playerPhysicsData.nowWall = playerPhysicsData.canRightWall;
        }
        else
        {
            //离开墙面后重置
            playerPhysicsData.nowWall=null;
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
            //重置标识
            isRecalculate= false;
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
    /// 统一计算外界因素带来的速度改变
    /// </summary>
    protected void UnderForce()
    {
        //快照重算，所以需要新变量承载，不能直接在原始数据上叠加
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
        ////延迟缓存有值
        //if (delayedRemoveForce.Count != 0)
        //{
        //    //当处于空中时，速度变换小，否则迅速复原
        //    float t = 1;
        //    if (playerPhysicsData.nowtaijie != null)
        //    {
        //        t = 10;
        //    }

        //    var keysToUpdate = new List<BasicPhysicalObject>(delayedRemoveForce.Count);
        //    foreach (var i in delayedRemoveForce)
        //    {
        //        startForceDic[i] *= 1f / (1 + t * cPhysics.envImpact * Time.fixedDeltaTime);
        //        if (startForceDic[i].sqrMagnitude < 0.2f)
        //        {
        //            startForceDic.Remove(i);
        //            keysToUpdate.Add(i);
        //        }
        //    }
        //    //循环删除
        //    foreach (var i in keysToUpdate)
        //    {
        //        delayedRemoveForce.Remove(i);
        //    }

        //    //isRecalculate = true;
        //}
        if (delayedRemoveForce.Count != 0)
        {
            //待删除列表
            var removelist = new List<BasicPhysicalObject>(delayedRemoveForce.Count);
            //更新受力所致速度变化
            foreach (var i in delayedRemoveForce)
            {
                //结构体需要先拷贝，再传入更新
                ForceData data = ForceDic[i];
                switch (data.type)
                {
                    case E_PhyForceType.apply:
                        sp.x += data.FixUpdate(cPhysics.envImpact);
                        //更新
                        ForceDic[i] = data;
                        break;
                    case E_PhyForceType.controlRecovery://如果是受控复原
                        //速度受控情况下衰减
                        sp.x += data.ControlledSpeedRecovery(cPhysics.envImpact);
                        ForceDic[i] = data;
                        break;
                    case E_PhyForceType.balance:
                        //平衡状态下不做处理
                        break;
                    case E_PhyForceType.fadeAway:
                        data.speedStacking -= self_resistanceCoefficient * cPhysics.envImpact * Time.fixedDeltaTime;
                        if (Mathf.Abs( data.speedStacking) < 0.2)
                        {
                            //移入删除
                            removelist.Add(i);
                            //删除受力影响
                            ForceDic.Remove(i);
                        }
                        else
                        {
                            sp.x += data.speedStacking;
                            ForceDic[i] = data;
                        }
                        break;
                    default:
                        break;
                }
            }
            //遍历删除
            foreach (var i in removelist)
            {
                delayedRemoveForce.Remove(i);
            }
        }
        //再计算状态力
        //此处不用标识，因为内部没有专门缓存当前状态力的数据容器（也没有必要专门占用一个数据内存
        //if (isRecalculate)
        {
            //print("测试状态力计算次数");

            foreach (var i in startForceDic.Values)
            {
                sp += i;
            }

        }
        //赋值操作，保证正确
        playerPhysicsData.phyHSpeed = sp.x;
        playerPhysicsData.phyVSpeed = sp.y;
    }

    /// <summary>
    /// 处理特殊行为的状态性垂直变速（例如贴墙下滑，攀岩
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

        lastFrameGroundPlatform?.OutObjPhy(this);
        lastFrameGroundPlatform = null;
        playerPhysicsData.nowtaijie?.OutObjPhy(this);
    }
    /// <summary>
    /// 持续性移动受限开始
    /// </summary>
    /// <param name="iD">唯一标识</param>
    /// <param name="num">影响程度</param>
    public void OnPhyEnter(BasicPhysicalObject iD, float num)
    {
        //避免键重复而报错
        phyStateDic[iD] = num;
        isRecalculate = true;
    }
    /// <summary>
    /// 持续性移动受限取消
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="Isdelayed">是否延迟</param>
    public void OnPhyExit(BasicPhysicalObject iD)
    {
        phyStateDic.Remove(iD);
        isRecalculate = true;
    }
    /// <summary>
    /// 外部提供速度(固定时间影响
    /// </summary>
    /// <param name="time">持续时间</param>
    /// <param name="force">受力大小</param>

    public void AddSpeed(float time,Vector2 force)
    {
        SpeedStackData data = new SpeedStackData();
        data.responseTimer1= Time.time+time;
        data.speedStack=force*cPhysics.envImpact;
        UnderForceList.Add(data);
    }
    /// <summary>
    /// 外部提供速度（状态持续影响
    /// </summary>
    /// <param name="force"></param>
    public void AddSpeedStatus(BasicPhysicalObject iD, Vector2 force)
    {

        startForceDic[iD] = force;

        //isRecalculate = true;
    }
    /// <summary>
    /// 外部取消状态性质速度
    /// </summary>
    /// <param name="force"></param>
    /// <param name="Isdelayed">是否延迟</param>
    public void RemoveSpeedStatus(BasicPhysicalObject iD)
    {
        //否则再将受力容器对应值删除
        startForceDic.Remove(iD);

        //isRecalculate = true;
    }

    public void AddForce(BasicPhysicalObject iD,ForceData force)
    {
        //若已有影响，则直接设置类型并返回
        if (delayedRemoveForce.Contains(iD))
        {
            ChangeType(iD,E_PhyForceType.apply);
            return;
        }
            
        //添加引用（（用于遍历
        delayedRemoveForce.Add(iD);
        //只有未找到此影响，则新增赋值
        ForceDic[iD] = force;
        //否则按照原有数据继续计算
    }
    /// <summary>
    /// 改变受力
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newForce"></param>
    public void ChangeForce(BasicPhysicalObject iD,float newForce)
    {
        ForceData data =ForceDic[iD];
        data.Force = newForce;
        ForceDic[iD] = data;
    }
    /// <summary>
    /// 施力类型变化
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newSpeed"></param>
    public void ChangeType(BasicPhysicalObject iD,E_PhyForceType type)
    {
        ForceData data = ForceDic[iD];
        data.type = type;
        ForceDic[iD] = data;
    }


    public void RemoveForce(BasicPhysicalObject iD)
    {
        //受力类型改为消除，物理循环自动更新和移除
        ChangeType(iD, E_PhyForceType.fadeAway);
    }
}
