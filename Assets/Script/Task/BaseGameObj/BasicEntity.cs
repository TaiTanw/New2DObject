using PhyData;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 实体基类
/// </summary>
public abstract class BasicEntity : MonoBehaviour, IForceAction,IPhyBaseI, IDynamicAddForce
{
    protected Rigidbody2D rb; //刚体
    protected BoxCollider2D boxCollider;//碰撞器
    /// <summary>
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
    /// <summary>
    /// 头顶检测
    /// </summary>
    [SerializeField]
    protected Transform upV;

    public float gravity = -35f;    //重力速度

    public float airResistance = 1;//空气阻力（0-1范围，影响下落情况
    /// <summary>
    /// 受到环境影响程度（近似理解为质量的倒数
    /// </summary>
    public float envImpact = 1;

    #region 运行时数据
    /// <summary>
    /// 当前几何检测数据
    /// </summary>
    protected GeometryPhysicsData nowGemetry;
    /// <summary>
    /// 物理职能数据
    /// </summary>
    protected BasePhyFunData nowPhyFun;
    /// <summary>
    /// 当前移动属性
    /// </summary>
    protected PlayerPhysicsData playerPhysicsData;

    /// <summary>
    /// 持续性环境物理受限状态容器（左右移动速度（粘滞力
    /// </summary>
    Dictionary<IApplyingForceAction, float> phyStateDic = new Dictionary<IApplyingForceAction, float>();
    /// <summary>
    /// 时间速度容器
    /// </summary>
    List<SpeedStackData> UnderForceList = new List<SpeedStackData>();
    /// <summary>
    /// 状态速度容器（传送带模型
    /// </summary>
    Dictionary<IApplyingForceAction, Vector2> startSpeedDic = new Dictionary<IApplyingForceAction, Vector2>();

    /// <summary>
    /// 受到哪些物体的施力影响
    /// </summary>
    HashSet<IDynamicAddForce> objectApplyingForce = new HashSet<IDynamicAddForce>();
    /// <summary>
    /// 受力计算容器（可确保一定有动态施力接口
    /// </summary>
    Dictionary<IDynamicAddForce, ForceData> dynamicForceDic = new Dictionary<IDynamicAddForce, ForceData>();
    /// <summary>
    /// 自身阻力系数
    /// </summary>
    protected float self_resistanceCoefficient = 3;

    /// <summary>
    /// 物理约束情况改变时才重算（脏标识
    /// </summary>
    bool isRecalculate;

    /// <summary>
    /// 持续性移动受限开始
    /// </summary>
    /// <param name="iD">唯一标识</param>
    /// <param name="num">影响程度</param>
    public void StatePowerRegistration(IApplyingForceAction iD, float num)
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
    public void StatePowerCancellation(IApplyingForceAction iD)
    {
        phyStateDic.Remove(iD);
        isRecalculate = true;
    }
    /// <summary>
    /// 外部提供速度(固定时间影响
    /// </summary>
    /// <param name="time">持续时间</param>
    /// <param name="addSpeed">水平速度叠加</param>

    public void AddTimeSpeed(float time, float addSpeed)
    {
        SpeedStackData data = new SpeedStackData();
        data.responseTimer1 = Time.time + time;
        data.hspeed = addSpeed * envImpact;
        UnderForceList.Add(data);
    }
    /// <summary>
    /// 外部提供速度（状态持续影响
    /// </summary>
    /// <param name="force"></param>
    public void AddSpeedStatus(IApplyingForceAction iD, Vector2 force)
    {

        startSpeedDic[iD] = force;

        //isRecalculate = true;
    }
    /// <summary>
    /// 外部取消状态性质速度
    /// </summary>
    /// <param name="force"></param>
    /// <param name="Isdelayed">是否延迟</param>
    public void RemoveSpeedStatus(IApplyingForceAction iD)
    {
        //否则再将受力容器对应值删除
        startSpeedDic.Remove(iD);

    }

    public void AddForce(IDynamicAddForce iD, ForceData force)
    {
        //若已有影响，则直接设置类型并返回
        if (objectApplyingForce.Contains(iD))
        {
            ChangeType(iD, E_PhyForceType.apply);
            return;
        }

        //添加引用（（用于遍历
        objectApplyingForce.Add(iD);
        //只有未找到此影响，则新增赋值
        dynamicForceDic[iD] = force;
        //否则按照原有数据继续计算
    }
    /// <summary>
    /// 改变受力
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newForce"></param>
    public void ChangeForce(IDynamicAddForce iD, float newForce)
    {
        ForceData data = dynamicForceDic[iD];
        data.Force = newForce;
        dynamicForceDic[iD] = data;
    }
    /// <summary>
    /// 施力类型变化
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newSpeed"></param>
    public void ChangeType(IDynamicAddForce iD, E_PhyForceType type)
    {
        ForceData data = dynamicForceDic[iD];
        data.type = type;
        dynamicForceDic[iD] = data;
    }


    public void RemoveForce(IDynamicAddForce iD)
    {
        if (!dynamicForceDic.TryGetValue(iD, out var data))
            return;
        //受力类型改为消除，物理循环自动更新和移除
        ChangeType(iD, E_PhyForceType.fadeAway);
    }


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
        nowGemetry =new GeometryPhysicsData();
        //下落加速度受空气阻力影响
        gravity*=airResistance;
        Init();
    }
    /// <summary>
    /// 初始化附加逻辑(子类可重写，初始化自身子类详细数据
    /// </summary>
    protected virtual void Init()
    {
        nowPhyFun = new BasePhyFunData();
    }

    protected virtual void OnEnable()
    {
        //物理更新时序设置
        //几何查询
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(GeometricQuery, 1);
        //物理职能更新
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(PhyFunUpdate, 2);
        //速度计算
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(SpeedCalculation, 3);
        //最终位移
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(DisplacementCorrection, 4);
        //二阶响应
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(SecondOrderPhyFun, 5);

    }

    /// <summary>
    /// 几何查询，最早阶段，检测碰撞信息
    /// </summary>
    protected abstract void GeometricQuery();
    /// <summary>
    /// 物理职能更新（自身
    /// </summary>
    protected virtual void PhyFunUpdate()
    {
        //得到地面的物理世界组件
        //解析并获得站地物理职能
        nowPhyFun.nowGround = null;
        if (nowGemetry.nowtaijie is BaseGround)
        {
            nowPhyFun.nowGround = nowGemetry.nowtaijie as BaseGround;
            //当找到脚本时，自身阻力受到地面阻力影响
            self_resistanceCoefficient *= nowPhyFun.nowGround.SlowingEffect;
        }
        // 比对状态，只在地面改变时调用
        if (nowPhyFun.nowGround !=  nowPhyFun.lastFrameGroundPlatform)//简易理解为状态（同时最多只能在一种地面上）
        {
            // 离开上一个平台
            if (nowPhyFun.lastFrameGroundPlatform != null)
            {
                nowPhyFun.lastFrameGroundPlatform.OnPhyExit(this);
            }

            // 进入新平台
            if (nowPhyFun.nowGround != null)
            {
                nowPhyFun.nowGround.OnPhyEnter(this);
            }
            // 记录本帧状态，供下帧对比
            nowPhyFun.lastFrameGroundPlatform = nowPhyFun.nowGround;
        }
    
    }

    /// <summary>
    /// 速度计算
    /// </summary>
    void SpeedCalculation()
    {
        //水平速度计算
        HorizontalSpeedCalculation();
        //竖直速度计算
        VerticalSpeedCalculation();
    }

    /// <summary>
    /// 水平速度计算
    /// </summary>
    void HorizontalSpeedCalculation()
    {
        //计算环境约束
        PhyStateCalculate();
        //计算被动速度
        HUnderForce();
        //计算主动操作的速度影响（计算主动速度,以及被动速度的特殊影响
        HActiveSpeedOperation();
    }

    /// <summary>
    /// 统一计算持续性物理环境影响移动的参数（快照重算模式
    /// </summary>
    void PhyStateCalculate()
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
            isRecalculate = false;
        }
    }


    /// <summary>
    /// 统一计算外界因素带来的水平附加速度改变
    /// </summary>
    void HUnderForce()
    {
        //快照重算，所以需要新变量承载，不能直接在原始数据上叠加
        Vector2 sp = Vector2.zero;
        //先计算时间力
        // 必须反向遍历，因为要操作删除
        for (int i = UnderForceList.Count - 1; i >= 0; i--)
        {
            if (UnderForceList[i].responseTimer1 < Time.time)//表示此力过期
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
                sp.x += UnderForceList[i].hspeed;
            }
        }
        //计算动态受力
        if (objectApplyingForce.Count != 0)
        {
            //待删除列表
            var removelist = new List<IDynamicAddForce>(objectApplyingForce.Count);
            //更新受力所致速度变化
            foreach (var i in objectApplyingForce)
            {
                //结构体需要先拷贝，再传入更新
                ForceData data = dynamicForceDic[i];
                // 如果力正在渐隐消除，则跳过施力者的更新逻辑，只执行后续衰减
                if (data.type != E_PhyForceType.fadeAway)
                {
                    i.ForceCalculation(this);
                    data = dynamicForceDic[i]; // ForceCalculation 可能已经修改了 data，需要重新获取
                }
                switch (data.type)
                {
                    case E_PhyForceType.apply:
                        sp.x += data.FixUpdate(envImpact);
                        //更新
                        dynamicForceDic[i] = data;
                        break;
                    case E_PhyForceType.controlRecovery://如果是受控复原
                        //速度受控情况下衰减
                        sp.x += data.ControlledSpeedRecovery(envImpact);
                        dynamicForceDic[i] = data;
                        break;
                    case E_PhyForceType.balance:
                        //平衡状态下不做处理
                        break;
                    case E_PhyForceType.fadeAway:
                        //计算受自身阻力影响的速度衰减
                        if (data.speedStacking > 0f)//正向速度减衰减
                        {
                            data.speedStacking -= self_resistanceCoefficient * envImpact * Time.fixedDeltaTime;
                            //防止速度反向
                            data.speedStacking = Mathf.Clamp(data.speedStacking, 0f, data.speedStacking);
                        }
                        else if (data.speedStacking < 0f)
                        {
                            data.speedStacking += self_resistanceCoefficient * envImpact * Time.fixedDeltaTime;
                            //防止速度反向
                            data.speedStacking = Mathf.Clamp(data.speedStacking, data.speedStacking, 0f);
                        }
                        //速度绝对值小于阈值则消除
                        if (Mathf.Abs(data.speedStacking) < 0.2)
                        {
                            //移入删除
                            removelist.Add(i);
                            //删除受力影响
                            dynamicForceDic.Remove(i);
                        }
                        else
                        {
                            sp.x += data.speedStacking;
                            dynamicForceDic[i] = data;
                        }
                        break;
                    default:
                        break;
                }
            }
            //遍历删除
            foreach (var i in removelist)
            {
                objectApplyingForce.Remove(i);
            }
        }
        //再计算状态力//为传送带模型，不受质量影响
        //此处不用标识，因为内部没有专门缓存当前状态力的数据容器（也没有必要专门占用一个数据内存
        //if (isRecalculate)
        {
            //print("测试状态力计算次数");

            foreach (var i in startSpeedDic.Values)
            {
                sp += i;
            }

        }
        //赋值操作，保证正确
        playerPhysicsData.phyHSpeed = sp.x;
        playerPhysicsData.phyVSpeed = sp.y;
    }
    /// <summary>
    /// 子类实现水平速度主动操作(后处理
    /// </summary>
    protected virtual void HActiveSpeedOperation()
    {

    }

    /// <summary>
    /// 竖直速度计算
    /// </summary>
    void VerticalSpeedCalculation()
    {
        //先计算被动速度
        VUnderForce();
        //再计算主动操作影响：表示主动操作可覆盖被动速度（例如跳跃
        VActiveSpeedOperation();
    }
    /// <summary>
    /// 计算垂直被动速度
    /// </summary>
    void VUnderForce()
    {
        // 重力
        if (!nowGemetry.isGrounded)
        {
            playerPhysicsData.verticalSpeed += gravity * Time.fixedDeltaTime;     //纵向速度改变（增减处理
        }
        else if (playerPhysicsData.verticalSpeed < 0)
        {
            playerPhysicsData.verticalSpeed = 0;
        }
    }
    /// <summary>
    /// 子类实现竖直速度主动操作（子类可选操作使用虚函数
    /// </summary>
    protected virtual void VActiveSpeedOperation()
    {

    }

    /// <summary>
    /// 位移修正与应用
    /// </summary>
    void DisplacementCorrection()
    {
        //计算当前速度向量与移动距离
        float nowHSpeed = playerPhysicsData.horizontalSpeed + playerPhysicsData.phyHSpeed;
        Vector2 velocity = new Vector2(nowHSpeed,
                                        playerPhysicsData.verticalSpeed + playerPhysicsData.phyVSpeed);

        Vector2 moveDelta = velocity * Time.fixedDeltaTime;             //计算相对位移
        //计算平台补偿位移
        Vector2 platformDelta = Vector2.zero;
        //使用于斜率位移修正
        if (nowGemetry.isGrounded)
        {
            //若有地面脚本，则获取位移修正
            if (nowPhyFun.nowGround)
                platformDelta = nowPhyFun.nowGround.Delta;
            //此处斜率补正
            moveDelta = new Vector2(0, playerPhysicsData.verticalSpeed * Time.fixedDeltaTime);
            //使用总速度
            float moveAmount = nowHSpeed * Time.fixedDeltaTime;
            //碰撞法线的垂线
            Vector2 tangent = new Vector2(nowGemetry.groundNormal.y, -nowGemetry.groundNormal.x);
            //判断方向是否一致（sign返回正负性
            if (Mathf.Sign(tangent.x) != Mathf.Sign(nowHSpeed))
                tangent *= -1;

            //Debug.Log(tangent);
            //此处表示，沿斜坡移动的速度和水平速度一致
            Vector2 slopeMove = tangent.normalized * Mathf.Abs(moveAmount);

            moveDelta += slopeMove;
        }

        Vector2 wordDelta = moveDelta + platformDelta;      //计算世界绝对位移
        //计算世界物理位移受限(防止过度挤压出现奇怪问题,此处只考虑了左右贴墙，后续考虑逻辑收束（上下处理
        if (wordDelta.x < 0 && nowGemetry.onLeftWall)
        {
            //横向速度制0
            wordDelta.x = 0;
            //碰墙后需要清空时间力，但状态力生命周期严格由施力物体控制，此处若清空状态力会导致问题,而附加力则清空速度但不移除受力影响
            if (playerPhysicsData.phyHSpeed < 0)//只有当被动速度也趋向于挤压
            {
                UnderForceList.Clear();
                foreach (var i in objectApplyingForce)//遍历置零
                {
                    ForceData data = dynamicForceDic[i];
                    data.speedStacking = 0;
                    dynamicForceDic[i] = data;
                }
            }

            nowPhyFun.nowWall = nowGemetry.canLeftWall;
        }
        else if (wordDelta.x > 0 && nowGemetry.onRightWall)
        {
            wordDelta.x = 0;
            if (playerPhysicsData.phyHSpeed > 0)
            {
                UnderForceList.Clear();
                foreach (var i in objectApplyingForce)//遍历置零
                {
                    ForceData data = dynamicForceDic[i];
                    data.speedStacking = 0;
                    dynamicForceDic[i] = data;
                }
            }

            //设置当前靠墙
            nowPhyFun.nowWall = nowGemetry.canRightWall;
        }
        else
        {
            //离开墙面后重置
            nowPhyFun.nowWall = null;
        }
        //  一次性统一移动
        rb.MovePosition(rb.position + wordDelta);
    }
    /// <summary>
    /// 二阶物理职能更新与应用
    /// </summary>
    protected virtual void SecondOrderPhyFun()
    {
        nowPhyFun.nowForceThing = null;
        if(nowPhyFun.nowWall != null && nowPhyFun.nowWall is IForceAction)
        {
            nowPhyFun.nowForceThing = nowPhyFun.nowWall as IForceAction;
        }

        if(nowPhyFun.nowForceThing!=nowPhyFun.lastForceThing)
        {
            //Debug.Log("AddForce");
            ForceData d =new ForceData();
            d.balanceSpeed = playerPhysicsData.phyHSpeed + playerPhysicsData.horizontalSpeed;
            d.Force = 10*d.balanceSpeed;
            nowPhyFun.nowForceThing?.AddForce(this,d );
            nowPhyFun.lastForceThing?.RemoveForce(this);
            //更新数据
            nowPhyFun.lastForceThing = nowPhyFun.nowForceThing;
        }

    }

    protected virtual void OnDisable()
    {
        //事件注销
        if (!MonoPublicMgr.IsQuitting)
        {
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(GeometricQuery, 1);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(PhyFunUpdate, 2);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(SpeedCalculation, 3);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(DisplacementCorrection, 4);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(SecondOrderPhyFun, 5);
        }

        nowPhyFun.nowGround?.OnPhyExit(this);


        nowPhyFun.lastFrameGroundPlatform?.OnPhyExit(this);
        nowPhyFun.lastFrameGroundPlatform = null;
    }

    public virtual void ForceCalculation(IForceAction IF)
    {
        
    }
}
