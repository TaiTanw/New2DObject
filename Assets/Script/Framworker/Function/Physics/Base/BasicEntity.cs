using PhyData;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色与箱子共用的物理执行主体：按相位读取环境、计算速度、预测并提交位移。
/// 环境关系由 EntityEnvironmentContext 管理；效果数值与累计速度由 EntityEffectState 持有。
/// 职能：实体物理相位编排、预测和位移提交；环境及接触效果经窄口转发。
/// </summary>
public abstract class BasicEntity : MonoBehaviour, IForceAction, IDynamicAddForce, IEnvironmentReceiver
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

    // 环境关系和效果数值分别由两个对象持有，不重复累计。
    private EntityEnvironmentContext environmentContext;
    public EntityEnvironmentContext EnvironmentContext => environmentContext ??= new EntityEnvironmentContext(this, this);
    protected EntityEnvironmentFrame EnvironmentFrame => EnvironmentContext.Frame;

    // 数值状态与实体同生命周期；来源关系仍由 environmentContext 管理。
    private readonly EntityEffectState effectState = new EntityEffectState();
    /// <summary>
    /// 自身阻力系数
    /// </summary>
    protected float self_resistanceCoefficient = 3;

    /// <summary>
    /// 本帧未约束运动快照：相位 3 在速度结算后整体替换，预测和相位 5 提交共同读取。
    /// 已包含现有地面运动规则与平台补偿；不保存接触偏置，也不承担跨帧速度累计。
    /// </summary>
    private EntityMotionFrame motionFrame;

    /// <summary>
    /// 实体解算结果：相位 4 累加 displacementOffset，相位 5 在同一物理帧消费。
    /// </summary>
    protected EntitySolutionResult entitySolutionResult;

    // 相位 1 overlap 工作缓存；有效 Trigger 再写入 nowGemetry.regionOverlaps。
    private readonly List<Collider2D> regionOverlapHits = new List<Collider2D>();

#if UNITY_EDITOR
    // [EditorOnly] 只读输出口；编辑器侧负责采样、缓存、快照组装与跨帧差值计算。
    public RigidbodyType2D DebugBodyType => rb != null ? rb.bodyType : RigidbodyType2D.Static;
    public Vector2 DebugActualPosition => rb != null ? rb.position : (Vector2)transform.position;
    public float DebugRotation => rb != null ? rb.rotation : transform.eulerAngles.z;
    public float DebugAngularVelocity => rb != null ? rb.angularVelocity : 0f;
    public Vector2 DebugPlanarVelocity => GetPlanarVelocity();
    public bool DebugIsGrounded => nowGemetry != null && nowGemetry.isGrounded;
    public bool DebugIsTopBlocked => nowGemetry != null && nowGemetry.istop;
    public bool DebugIsOnLeftWall => nowGemetry != null && nowGemetry.onLeftWall;
    public bool DebugIsOnRightWall => nowGemetry != null && nowGemetry.onRightWall;
    public Vector2 DebugGroundNormal => nowGemetry != null ? nowGemetry.groundNormal : Vector2.zero;
    public int DebugEnvironmentSourceCount => environmentContext?.DebugSourceCount ?? 0;
    public int DebugMovementModifierCount => effectState.DebugMovementModifierCount;
    public int DebugStateVelocityCount => effectState.DebugStateVelocityCount;
    public int DebugDynamicForceCount => effectState.DebugDynamicForceCount;
    public float DebugEnvironmentSlowingMultiplier => environmentContext?.Frame.slowingMultiplier ?? 1f;
    public float DebugEnvironmentJumpHeightOffset => environmentContext?.Frame.jumpHeightOffset ?? 0f;

    public bool TryGetEditorGizmoData(out PhysicsEditorObservationBridge.GizmoData data)
    {
        data = default;
        if (cPhysics == null) return false;

        data.hasGround = groundV != null;
        data.hasTop = upV != null;
        data.hasLeft = leftV != null;
        data.hasRight = rightV != null;
        data.groundCenter = data.hasGround ? groundV.position : default;
        data.topCenter = data.hasTop ? upV.position : default;
        data.leftCenter = data.hasLeft ? leftV.position : default;
        data.rightCenter = data.hasRight ? rightV.position : default;
        data.horizontalSize = cPhysics.boxCastH;
        data.verticalSize = cPhysics.boxCastV;
        return true;
    }
#endif

    #endregion

    #region 实体推动与接触

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
    /// 接触刚成立时，按挤出方向一次性削弱会继续进入接触面的非持续速度来源。
    /// 只改限时速度与 fadeAway 动态力；主动速度、状态速度和仍在 apply 的持续力不属于本次碰撞消耗。
    /// </summary>
    public void AttenuateTransientContactSpeed(float separateSign, float retainRatio)
    {
        effectState.AttenuateTransientContactSpeed(separateSign, retainRatio);
    }

    #endregion

    #region IForceAction 受力入口

    /// <summary>
    /// 写入主动移速修饰；ENV-04 以 Registration 为环境来源键，数值状态在相位 3 汇总。
    /// </summary>
    /// <param name="iD">唯一标识</param>
    /// <param name="num">影响程度</param>
    public void StatePowerRegistration(EnvironmentRegistration iD, float num)
    {
        effectState.RegisterMovementModifier(iD, num);
    }
    /// <summary>
    /// 撤销该来源的主动移速修饰，标记下一速度阶段重新汇总。
    /// </summary>
    /// <param name="iD"></param>
    public void StatePowerCancellation(EnvironmentRegistration iD)
    {
        effectState.RemoveMovementModifier(iD);
    }
    /// <summary>
    /// 外部提供速度(固定时间影响
    /// </summary>
    /// <param name="time">持续时间</param>
    /// <param name="addSpeed">水平速度叠加</param>

    public void AddTimeSpeed(float time, float addSpeed)
    {
        effectState.AddTimedSpeed(time, addSpeed, envImpact);
    }
    /// <summary>
    /// 写入该来源的持续速度；ENV-04 登记，ENV-06 在数值状态中逐来源相加。
    /// </summary>
    /// <param name="iD">来源键；环境状态使用 Registration。</param>
    /// <param name="force">持续附加速度；沿用旧参数名，不代表需要积分的力。</param>
    public void AddSpeedStatus(EnvironmentRegistration iD, Vector2 force)
    {
        effectState.AddStateVelocity(iD, force);
    }
    /// <summary>
    /// 外部取消状态性质速度
    /// </summary>
    /// <param name="iD">需要删除的持续速度来源。</param>
    public void RemoveSpeedStatus(EnvironmentRegistration iD)
    {
        effectState.RemoveStateVelocity(iD);
    }

    /// <summary>接收 ENV-04 创建的动态参数；累计速度归数值状态，来源键仍是动态能力提供者。</summary>
    public void AddForce(IDynamicAddForce iD, ForceData force)
    {
        effectState.AddDynamicForce(iD, force);
    }

    /// <summary>
    /// 新增或刷新动态力控制参数，同时保留已经跨帧累计的 speedStacking。
    /// 接触数据链路：管理器相位 4 调用 → 下一帧数值状态积分 → phyHSpeed 参与位移预测。
    /// IForceAction 窄口只传施力参数，累计速度仍由数值状态持有。
    /// </summary>
    public void SetOrUpdateDynamicForce(IDynamicAddForce source, DynamicForceParameters parameters)
    {
        effectState.SetOrUpdateDynamicForce(source, parameters);
    }

    /// <summary>
    /// 改变受力
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newForce"></param>
    public void ChangeForce(IDynamicAddForce iD, float newForce)
    {
        effectState.ChangeDynamicForce(iD, newForce);
    }
    /// <summary>
    /// 施力类型变化
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newSpeed"></param>
    public void ChangeType(IDynamicAddForce iD, E_PhyForceType type)
    {
        effectState.ChangeDynamicType(iD, type);
    }


    /// <summary>ENV-07 的数值出口：停止来源控制，转 fadeAway；剩余速度由 ENV-06 继续衰减。</summary>
    public void RemoveForce(IDynamicAddForce iD)
    {
        effectState.RemoveDynamicForce(iD);
    }


    #endregion

    protected virtual void Awake()
    {
        BindEffectReceiver();
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

    /// <summary>供自行实现 Awake 的实体明确完成效果回调关联。</summary>
    protected void BindEffectReceiver() => effectState.BindReceiver(this);
    /// <summary>
    /// 初始化附加逻辑(子类可重写，初始化自身子类详细数据
    /// </summary>
    protected virtual void Init()
    {
        nowPhyFun = new BasePhyFunData();
    }

    protected virtual void OnEnable()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            PhysicsEditorObservationBridge.NotifyEntityEnabled(this);
#endif
        //物理更新时序设置
        //几何查询
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(GeometricQueryPhase, 1);
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
    /// 相位 1：先做脚下/贴墙查询，再记下本帧区域 Trigger 重叠。
    /// 职能：几何感知入口；登记效果仍在相位 2。
    /// </summary>
    void GeometricQueryPhase()
    {
        GeometricQuery();
        CollectRegionOverlapFacts();
    }

    /// <summary>
    /// 用自身碰撞体扫到的区域 Trigger 写入几何事实列表。例如碰到水的触发器只记碰撞体。
    /// 职能：相位 1 感知；不在这里登记或撤销环境效果。
    /// </summary>
    protected void CollectRegionOverlapFacts()
    {
        nowGemetry.regionOverlaps.Clear();
        // 安全：没有可用碰撞体时本帧没有区域重叠事实。
        if (boxCollider == null || !boxCollider.enabled) return;

        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(Physics2D.GetLayerCollisionMask(boxCollider.gameObject.layer));
        regionOverlapHits.Clear();
        boxCollider.OverlapCollider(filter, regionOverlapHits);
        foreach (Collider2D other in regionOverlapHits)
        {
            // 安全：销毁、禁用或不在激活层级的碰撞体不是有效命中。
            if (other == null || !other.enabled || !other.gameObject.activeInHierarchy) continue;
            // 区域选择：只保留对方 Trigger，避免把实心地面/墙写进区域事实。
            if (!other.isTrigger) continue;
            // 区域规则：排除自身与显式忽略对；层矩阵已在 filter 中筛选。
            if (other.gameObject == gameObject || Physics2D.GetIgnoreCollision(boxCollider, other)) continue;
            // 与 Unity 触发条件一致：至少一方具有 Rigidbody2D。
            if (boxCollider.attachedRigidbody == null && other.attachedRigidbody == null) continue;
            nowGemetry.regionOverlaps.Add(other);
        }
    }
    /// <summary>
    /// 物理职能更新（自身
    /// </summary>
    protected virtual void PhyFunUpdate()
    {
        // 相位 1 已拿到脚下 Collider 和区域 Trigger 重叠；从这里进入 RefreshEnvironment。
        // 着地才传脚下；离地传 null，使旧 Ground 依据被撤销。普通无脚本地面也可着地。
        // 相位 1 已按空中/着地重置自身阻力。本相位只乘一次表面倍率，不跨帧累乘。
        EnvironmentContext.RefreshEnvironment(
            nowGemetry.isGrounded ? nowGemetry.groundCollider : null,
            nowGemetry.regionOverlaps);
        self_resistanceCoefficient *= EnvironmentFrame.slowingMultiplier;
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
        // 上一帧接触动态力已由上面的速度计算入账。在这里读取相位 2 环境帧的平台位移并构建一次运动快照，
        // 相位 4 仍只累计偏置及刷新下帧动态力，相位 5 不再重算基础位移。
        Vector2 platformDelta = EnvironmentFrame.platformDelta;
        motionFrame = BuildMotionFrame(
#if UNITY_EDITOR
            this,
#endif
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
    /// 沿用原有位移公式；编辑器宏下旁路输出已计算分量，不改变快照结果或写入 Unity 世界。
    /// fixedDeltaTime 显式传入，确保各分量使用同一物理步长。
    /// </summary>
    private static EntityMotionFrame BuildMotionFrame(
#if UNITY_EDITOR
        BasicEntity entity,
#endif
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

        Vector2 appliedPlatformDelta = isGrounded ? platformDelta : Vector2.zero;
#if UNITY_EDITOR
        PhysicsEditorObservationBridge.ReportMotionBuilt(
            entity,
            freeVelocity,
            integratedVelocityDelta,
            moveDelta,
            appliedPlatformDelta,
            moveDelta + appliedPlatformDelta);
#endif
        return new EntityMotionFrame(moveDelta, appliedPlatformDelta);
    }

    /// <summary>
    /// 位置预测：按本帧运动快照投射 AABB 到管理器，不再单独使用速度乘 dt。
    /// 预测形状包含地面运动和平台补偿，但尚不包含相位 4 偏置、相位 5 静墙裁剪或 Unity 修正。
    /// </summary>
    void PositionPrediction()
    {
        // 相位 4 会重新累加本帧偏置，先清零避免无接触时沿用旧结果。
        // 接触速度不再写入解算结果，而由实体数值状态在下一帧相位 3 跨帧累计。
        entitySolutionResult.displacementOffset = Vector2.zero;

        if (boxCollider == null || rb == null)
            return;

        PhysicalBoundingBox box = new PhysicalBoundingBox();
        box.point = (Vector2)boxCollider.bounds.center + motionFrame.plannedWorldDelta;
        box.size = boxCollider.bounds.extents; // 半长宽，与解算器 v3=a.size+b.size 一致
        box.myPhyBox = this;
#if UNITY_EDITOR
        PhysicsEditorObservationBridge.ReportPredictionBuilt(this, box.point, box.size);
#endif
        PhysicsSolverMgr.Instance.AddPhyBoX(box);
    }

    /// <summary>
    /// 水平速度计算
    /// </summary>
    void HorizontalSpeedCalculation()
    {
        // [ENV-06] 第一遍只看这三次调用：移速修饰、被动速度、主动速度。登记层到这里才变成速度。
        //计算环境约束
        effectState.UpdateMovementModifier(playerPhysicsData);
        //计算被动速度
        UpdatePassiveEffectSpeed();
        //计算主动操作的速度影响（计算主动速度,以及被动速度的特殊影响
        HActiveSpeedOperation();
    }

    /// <summary>
    /// ENV-06 的被动速度入口；限时、动态、持续速度的具体更新归 EntityEffectState。
    /// </summary>
    void UpdatePassiveEffectSpeed()
    {
        effectState.UpdatePassiveSpeed(playerPhysicsData, envImpact, self_resistanceCoefficient);
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
    /// 位移应用
    /// 消费相位 3 的基础运动快照及相位 4 的接触结果，再执行原有静墙裁剪与一次 MovePosition。
    /// 基础位移已包含平台补偿，此处不再读地面/平台重算，避免预测与提交采用两套位移。
    /// </summary>
    void DisplacementCorrection()
    {
        Vector2 solverOffset = entitySolutionResult.displacementOffset;
        Vector2 wordDelta = motionFrame.plannedWorldDelta + solverOffset;
#if UNITY_EDITOR
        Vector2 debugUnconstrainedDelta = wordDelta;
        bool debugStaticWallClamped = false;
#endif
        //静态墙裁剪：可推实体走接触对偏置，不得再整轴置零（否则推箱抖动）
        //无脚本的墙层碰撞体同样视为静墙
        bool staticLeft = nowGemetry.onLeftWall && EnvironmentCapabilities.FindEntity(nowGemetry.leftWallCollider) == null;
        bool staticRight = nowGemetry.onRightWall && EnvironmentCapabilities.FindEntity(nowGemetry.rightWallCollider) == null;
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
                effectState.ClearTransientSpeedAtWall();
            }
        }
        else if (wordDelta.x > 0 && staticRight)
        {
#if UNITY_EDITOR
            debugStaticWallClamped = true;
#endif
            wordDelta.x = 0;
            if (playerPhysicsData.phyHSpeed > 0)
            {
                effectState.ClearTransientSpeedAtWall();
            }
        }
#if UNITY_EDITOR
        Vector2 debugRequestedTarget = rb.position + wordDelta;
        PhysicsEditorObservationBridge.ReportMovementRequested(
            this,
            solverOffset,
            debugUnconstrainedDelta,
            wordDelta,
            debugRequestedTarget,
            debugStaticWallClamped);
#endif
        // 相位 5 的最后一步：所有状态更新和观测采样完成后，仅提交一次位移。
        rb.MovePosition(rb.position + wordDelta);
    }

    protected virtual void OnDisable()
    {
#if UNITY_EDITOR
        if (Application.isPlaying)
            PhysicsEditorObservationBridge.NotifyEntityDisabled(this);
#endif
        //事件注销
        if (!MonoPublicMgr.IsQuitting)
        {
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(GeometricQueryPhase, 1);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(PhyFunUpdate, 2);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(SpeedCalculation, 3);
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(DisplacementCorrection, 5);
        }

        environmentContext?.ReleaseAll();
    }

    public virtual void ForceCalculation(IForceAction IF)
    {
        // 实体接触力由 PhysicsSolverMgr 在相位 4 统一刷新。
        // 下一帧相位 3 的数值状态更新不重复推导接触力，直接使用相位 4 留下的参数。
    }
}
