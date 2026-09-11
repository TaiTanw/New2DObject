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
    /// 本帧未约束运动快照：相位 3 在速度结算后整体替换，预测和相位 5 提交共同读取。
    /// 已包含现有地面运动规则与平台补偿；不保存接触偏置，也不承担跨帧速度累计。
    /// </summary>
    private EntityMotionFrame motionFrame;

    /// <summary>
    /// 实体解算结果：相位 4 累加 displacementOffset，相位 5 在同一物理帧消费。
    /// </summary>
    protected EntitySolutionResult entitySolutionResult;

#if UNITY_EDITOR
    // [EditorOnly] 观测缓存统一使用 debug 前缀；只向 DebugSnapshot 输出，不作为运行时解算输入。
    int debugFixedTick;
    bool debugHasRequestedTarget;
    Vector2 debugActualPosition;
    Vector2 debugPredictedCenter;
    Vector2 debugPredictedExtents;
    Vector2 debugSolverOffset;
    Vector2 debugUnconstrainedDelta;
    Vector2 debugRequestedDelta;
    Vector2 debugRequestedTarget;
    Vector2 debugEngineCorrection;
    bool debugStaticWallClamped;

    /// <summary>
    /// [EditorOnly] 编辑器只读观测入口。调试工具不得通过该快照改变物理解算。
    /// </summary>
    public EntityPhysicsDebugSnapshot DebugSnapshot
    {
        get
        {
            return new EntityPhysicsDebugSnapshot
            {
                fixedTick = debugFixedTick,
                entityName = name,
                bodyType = rb != null ? rb.bodyType : RigidbodyType2D.Static,
                actualPosition = debugActualPosition,
                rotation = rb != null ? rb.rotation : transform.eulerAngles.z,
                angularVelocity = rb != null ? rb.angularVelocity : 0f,
                planarVelocity = motionFrame.debugFreeVelocity,
                predictedCenter = debugPredictedCenter,
                predictedExtents = debugPredictedExtents,
                integratedVelocityDelta = motionFrame.debugIntegratedVelocityDelta,
                motionDelta = motionFrame.debugMotionDelta,
                platformDelta = motionFrame.debugPlatformDelta,
                plannedWorldDelta = motionFrame.plannedWorldDelta,
                solverOffset = debugSolverOffset,
                unconstrainedDelta = debugUnconstrainedDelta,
                requestedDelta = debugRequestedDelta,
                requestedTarget = debugRequestedTarget,
                engineCorrection = debugEngineCorrection,
                staticWallClamped = debugStaticWallClamped,
                isGrounded = nowGemetry != null && nowGemetry.isGrounded,
                isTopBlocked = nowGemetry != null && nowGemetry.istop,
                isOnLeftWall = nowGemetry != null && nowGemetry.onLeftWall,
                isOnRightWall = nowGemetry != null && nowGemetry.onRightWall,
                groundNormal = nowGemetry != null ? nowGemetry.groundNormal : Vector2.zero
            };
        }
    }
#endif

    /// <summary>
    /// 受环境影响程度（质量倒数，接触挤出权重）
    /// </summary>
    public float EnvImpact => envImpact;

    /// <summary>
    /// 当前平面速度（主动 + 被动；动态接触力在相位 3 从容器累计到被动速度）
    /// 保留现有接触施力的读口；该值尚未经过地面位移处理，不等于最终世界位移除以 dt。
    /// </summary>
    public Vector2 GetPlanarVelocity()
    {
        return new Vector2(
            playerPhysicsData.horizontalSpeed + playerPhysicsData.phyHSpeed,
            playerPhysicsData.verticalSpeed + playerPhysicsData.phyVSpeed);
    }

    /// <summary>
    /// 累加本帧位移偏置（由 PhysicsSolverMgr 接触对写入）
    /// </summary>
    public void AccumulateDisplacementOffset(Vector2 offset)
    {
        entitySolutionResult.displacementOffset += offset;
    }

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
    /// 新增或刷新动态力控制参数，同时保留已经跨帧累计的 speedStacking。
    /// 接触数据链路：管理器相位 4 调用 → 下一帧 HUnderForce/FixUpdate 累计 → phyHSpeed 参与位移预测。
    /// 此方法不加入 IForceAction，避免改变冰面、力场等现有通用施力接口的约定。
    /// </summary>
    public void SetOrUpdateDynamicForce(
        IDynamicAddForce source,
        float force,
        float balanceSpeed,
        float recoverySpeed = 0f)
    {
        if (dynamicForceDic.TryGetValue(source, out ForceData data))
        {
            data.Force = force;
            data.balanceSpeed = Mathf.Abs(balanceSpeed);
            data.recoverySpeed = recoverySpeed;
            data.type = E_PhyForceType.apply;
            dynamicForceDic[source] = data;
            objectApplyingForce.Add(source);
            return;
        }

        ForceData newData = new ForceData();
        newData.Init(force, recoverySpeed, balanceSpeed);
        dynamicForceDic[source] = newData;
        objectApplyingForce.Add(source);
    }

    /// <summary>
    /// 接触刚成立时，按挤出方向一次性削弱会继续进入接触面的非持续速度来源。
    /// 只改限时速度与 fadeAway 动态力；主动速度、状态速度和仍在 apply 的持续力不属于本次碰撞消耗。
    /// </summary>
    public void AttenuateTransientContactSpeed(float separateSign, float retainRatio)
    {
        if (Mathf.Approximately(separateSign, 0f))
            return;

        separateSign = separateSign >= 0f ? 1f : -1f;
        retainRatio = Mathf.Clamp01(retainRatio);
        //时间力衰减
        for (int i = 0; i < UnderForceList.Count; i++)
        {
            SpeedStackData data = UnderForceList[i];
            // 速度与挤出方向异号，表示该速度分量仍朝接触面运动。
            if (data.hspeed * separateSign < 0f)
            {
                data.hspeed *= retainRatio;
                UnderForceList[i] = data;
            }
        }
        //动态（fadeAway状态）力衰减
        foreach (IDynamicAddForce source in objectApplyingForce)
        {
            ForceData data = dynamicForceDic[source];
            if (data.type == E_PhyForceType.fadeAway &&
                data.speedStacking * separateSign < 0f)
            {
                data.speedStacking *= retainRatio;
                dynamicForceDic[source] = data;
            }
        }
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
        //重力由 EPhy 计算；当前保留 Dynamic，让 Unity 2D 负责静态世界的最终非穿透约束
        rb.gravityScale = 0;
        //待静态扫掠、残嵌恢复和接触稳定闭环后，再评估是否切换为全 Kinematic
        //rb.bodyType = RigidbodyType2D.Kinematic;
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
        //速度计算 → 构建本帧运动快照 → PositionPrediction 上报预测框
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(SpeedCalculation, 3);
        // 相位 4：PhysicsSolverMgr.ContactForSolution（管理器注册）
        //最终位移（复用本帧运动快照，叠加相位 4 的 displacementOffset）
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(DisplacementCorrection, 5);
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
#if UNITY_EDITOR
        debugFixedTick++;
        debugActualPosition = rb != null ? rb.position : (Vector2)transform.position;
        debugEngineCorrection = debugHasRequestedTarget
            ? debugActualPosition - debugRequestedTarget
            : Vector2.zero;
#endif
        //水平速度计算
        HorizontalSpeedCalculation();
        //竖直速度计算
        VerticalSpeedCalculation();
        // 上一帧接触动态力已由上面的速度计算入账。本帧仅在这里采样地面/平台并构建一次快照，
        // 相位 4 仍只累计偏置及刷新下帧动态力，相位 5 不再重算基础位移。
        Vector2 platformDelta = nowGemetry.isGrounded && nowPhyFun.nowGround != null
            ? nowPhyFun.nowGround.Delta
            : Vector2.zero;
        motionFrame = BuildMotionFrame(
            GetPlanarVelocity(),
            playerPhysicsData.verticalSpeed,
            nowGemetry.isGrounded,
            nowGemetry.groundNormal,
            platformDelta,
            Time.fixedDeltaTime);
        PositionPrediction();
    }

    /// <summary>
    /// 把速度、几何和平台的本帧采样转换为未约束运动快照。
    /// 从旧相位 5 提取原有位移公式；只计算值，不积分额外力、不读写实体或 Unity 世界。
    /// fixedDeltaTime 显式传入，确保各分量使用同一物理步长。
    /// </summary>
    private static EntityMotionFrame BuildMotionFrame(
        Vector2 freeVelocity,
        float verticalSpeed,
        bool isGrounded,
        Vector2 groundNormal,
        Vector2 platformDelta,
        float fixedDeltaTime)
    {
        Vector2 integratedVelocityDelta = freeVelocity * fixedDeltaTime;
        Vector2 moveDelta = integratedVelocityDelta;
        if (isGrounded)
        {
            // TODO（竖直环境速度语义，另案核对）：保留旧着地分支的提交规则。
            // freeVelocity.y 包含 verticalSpeed + phyVSpeed，但这里重建位移时只保留 verticalSpeed，
            // 因此着地时 phyVSpeed 不进入 motionDelta；verticalSpeed 本身还含重力/跳跃，并非纯主动速度。
            // 平台的 platformDelta.y 会单独相加，它与 phyVSpeed 是不同来源，不能互相替代。
            // 本次仅让预测与提交共同遵循现状，不在提取函数时补加或清零 phyVSpeed。
            // 后续应分别验证正/负竖直环境速度、起跳首帧及移动平台后，再确定地面处理规则。
            moveDelta = new Vector2(0, verticalSpeed * fixedDeltaTime);
            float moveAmount = freeVelocity.x * fixedDeltaTime;
            Vector2 tangent = new Vector2(groundNormal.y, -groundNormal.x);
            if (Mathf.Sign(tangent.x) != Mathf.Sign(freeVelocity.x))
                tangent *= -1;

            // 沿用原规则：总水平速度的大小作为沿地面切线的运动速度。
            moveDelta += tangent.normalized * Mathf.Abs(moveAmount);
        }

        return new EntityMotionFrame(
            freeVelocity,
            integratedVelocityDelta,
            moveDelta,
            isGrounded ? platformDelta : Vector2.zero);
    }

    /// <summary>
    /// 位置预测：按本帧运动快照投射 AABB 到管理器，不再单独使用速度乘 dt。
    /// 预测形状包含地面运动和平台补偿，但尚不包含相位 4 偏置、相位 5 静墙裁剪或 Unity 修正。
    /// </summary>
    void PositionPrediction()
    {
        // 相位 4 会重新累加本帧偏置，先清零避免无接触时沿用旧结果。
        // 接触速度不再写入解算结果，而由 dynamicForceDic 在下一帧相位 3 跨帧累计。
        entitySolutionResult.displacementOffset = Vector2.zero;

        if (boxCollider == null || rb == null)
            return;

        PhysicalBoundingBox box = new PhysicalBoundingBox();
        box.point = (Vector2)boxCollider.bounds.center + motionFrame.plannedWorldDelta;
        box.size = boxCollider.bounds.extents; // 半长宽，与解算器 v3=a.size+b.size 一致
        box.myPhyBox = this;
#if UNITY_EDITOR
        debugPredictedCenter = box.point;
        debugPredictedExtents = box.size;
#endif
        PhysicsSolverMgr.Instance.AddPhyBoX(box);
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
    /// 消费相位 3 的基础运动快照及相位 4 的接触结果，再执行原有静墙裁剪与一次 MovePosition。
    /// 基础位移已包含平台补偿，此处不再读地面/平台重算，避免预测与提交采用两套位移。
    /// </summary>
    void DisplacementCorrection()
    {
        Vector2 solverOffset = entitySolutionResult.displacementOffset;
        Vector2 wordDelta = motionFrame.plannedWorldDelta + solverOffset;
#if UNITY_EDITOR
        // [EditorOnly] 裁剪前位移和“本帧发生裁剪”仅用于观测，直接写调试缓存。
        // 正式运行仅使用下方的 wordDelta/墙面状态，不保留额外观测局部变量。
        debugUnconstrainedDelta = wordDelta;
        debugStaticWallClamped = false;
#endif
        //静态墙裁剪：可推实体走接触对偏置，不得再整轴置零（否则推箱抖动）
        //无脚本的墙层碰撞体同样视为静墙
        bool staticLeft = nowGemetry.onLeftWall && nowGemetry.canLeftWall is not BasicEntity;
        bool staticRight = nowGemetry.onRightWall && nowGemetry.canRightWall is not BasicEntity;
        if (wordDelta.x < 0 && staticLeft)
        {
#if UNITY_EDITOR
            debugStaticWallClamped = true;
#endif
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
        else if (wordDelta.x > 0 && staticRight)
        {
#if UNITY_EDITOR
            debugStaticWallClamped = true;
#endif
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
        // 由已解析的静墙状态刷新下一帧墙滑引用；不读取移动后的接触结果，故在最终位移提交前完成。
        RefreshWallSlideSnapshot();
#if UNITY_EDITOR
        // [EditorOnly] 提交前记录请求结果，下一物理帧再观察 Unity 的实际位置修正。
        debugSolverOffset = solverOffset;
        debugRequestedDelta = wordDelta;
        debugRequestedTarget = rb.position + wordDelta;
        debugHasRequestedTarget = true;
#endif
        // 相位 5 的最后一步：所有状态更新和观测采样完成后，仅提交一次位移。
        rb.MovePosition(rb.position + wordDelta);
    }

    /// <summary>
    /// 位移提交前，依据本帧已解析的墙面状态刷新墙滑引用（子类角色覆盖）。
    /// 不依赖 Unity 在 MovePosition 之后的真实接触结果；最终位移提交保持在相位 5 末尾。
    /// </summary>
    protected virtual void RefreshWallSlideSnapshot()
    {
    }

    protected virtual void OnDisable()
    {
        //事件注销
        if (!MonoPublicMgr.IsQuitting)
        {
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(GeometricQuery, 1);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(PhyFunUpdate, 2);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(SpeedCalculation, 3);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(DisplacementCorrection, 5);
        }

        nowPhyFun.nowGround?.OnPhyExit(this);


        nowPhyFun.lastFrameGroundPlatform?.OnPhyExit(this);
        nowPhyFun.lastFrameGroundPlatform = null;
    }

    public virtual void ForceCalculation(IForceAction IF)
    {
        // 实体接触力由 PhysicsSolverMgr 在相位 4 统一刷新。
        // HUnderForce 在下一帧相位 3 回调到这里时不重复推导，直接使用容器内上一相位 4 的参数。
    }
}
