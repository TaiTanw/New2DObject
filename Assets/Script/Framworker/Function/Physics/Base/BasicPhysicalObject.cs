using PhyData;
using UnityEngine;

/// <summary>
/// 旧地面、冰面、力场仍从这里取得已有移速/持续速度参数；启用时接入调度，禁用时统一通知退出。
/// 职能：现有场景组件的 Unity 兼容外壳；能力接口不要求新组件继承本类。
/// 子类只扩展参数、算法或启停钩子；本类固定入口负责转发，不再要求子类成对重写登记/撤销。
/// F_5.3 入口约束：同一物体只挂本类家族中的一个组件，该组件可实现多个能力接口。
/// </summary>
[DisallowMultipleComponent]
public class BasicPhysicalObject : MonoBehaviour, IApplyingForceAction, IPhyBaseI,
    IEnvironmentSource, IMovementSpeedModifier, IStateVelocitySource
{
    [SerializeField] protected float speedChangeNum; // 主动速度加性修饰，旧序列化名称保留。
    [SerializeField] protected Vector2 phySpeed; // 持续速度，非逐帧积分的力。

    public float MovementSpeedOffset => speedChangeNum;
    public Vector2 StateVelocity => phySpeed;

    private EnvironmentRegistration environmentRegistration;
    public EnvironmentRegistration EnvironmentRegistration => environmentRegistration ??=
        new EnvironmentRegistration(this, this, AppliesOnGround);

    protected virtual bool AppliesOnGround => false; // BaseGround 开启：走 ENV-02 的脚下入口。
    protected virtual bool UsesTriggerRegion => false; // ForceField / pond 开启：走 ENV-R1 区域入口。

    public void OnPhyEnter(IForceAction receiver) => EnvironmentRegistration.OnPhyEnter(receiver);
    public void OnPhyExit(IForceAction receiver) => EnvironmentRegistration.OnPhyExit(receiver);

    protected void OnEnable()
    {
        // 调度选择：只有区域来源注册相位 2 采样；脚下来源由实体自己发现。
        if (UsesTriggerRegion)
            MonoPublicMgr.Instance.AddPhysicalTimingUpdate(RefreshRegion, 2);
        OnSourceEnabled();
    }

    protected void OnDisable()
    {
        // 调度选择与退出保护：仅撤已使用的区域回调；退出程序时不再访问管理器单例。
        if (UsesTriggerRegion && !MonoPublicMgr.IsQuitting)
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(RefreshRegion, 2);
        // [ENV-L1] 源端禁用：已创建辅助对象才清理；最终走各实体 ENV-07。
        environmentRegistration?.ReleaseAll();
        OnSourceDisabled();
    }

    private void RefreshRegion() => EnvironmentRegistration.RefreshTriggerRegion();

#if UNITY_EDITOR
    /// <summary>[EditorOnly] 提示旧资源中的重复入口；不自动删除组件或修改参数。</summary>
    protected void OnValidate()
    {
        // 配置约束：禁用的组件也占入口身份；不同派生类仍属于同一个环境入口家族。
        if (GetComponents<BasicPhysicalObject>().Length > 1)
            Debug.LogError("同一物体只能有一个 BasicPhysicalObject 环境入口（包括不同子类）；请将所需能力放到同一组件。", this);
    }
#endif

    /// <summary>供平台等注册自身运动回调；环境登记清理由固定入口统一执行。</summary>
    protected virtual void OnSourceEnabled() { }
    protected virtual void OnSourceDisabled() { }
}
