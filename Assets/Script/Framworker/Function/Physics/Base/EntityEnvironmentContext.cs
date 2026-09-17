using System;
using System.Collections.Generic;
using PhyData;
using UnityEngine;

/// <summary>
/// 例如角色踩上冰面，这里记下“这块冰面正在影响这个角色，以及已经登记了哪些效果”。
/// 同一来源即使同时通过脚下支撑和区域重叠接入，也只登记一份；最后一个依据消失才撤销。
/// 职能：实体持有的接入关系层；数值仍写入 IForceAction 的原容器，本类不积分速度。
/// 阅读：ENV-02 核对脚下 → ENV-03 判断是否首次接入 → ENV-04 登记；退出看 ENV-07。
/// </summary>
public sealed class EntityEnvironmentContext
{
    [Flags]
    internal enum Access { Ground = 1, Region = 2, Explicit = 4 }

    /// <summary>
    /// 一个来源对当前实体的登记单：access 记“为什么还有效”，其余字段记“退出时要撤销什么”。
    /// 例如 Ground 被移除但 Region 仍存在，就保留整份效果。
    /// 职能：Context 私有关系记录；不存 ForceData 或累计速度。
    /// </summary>
    private sealed class Binding
    {
        public Access access;
        public bool hasMovement;
        public bool hasStateVelocity;
        public IDynamicEnvironmentForce dynamicForce;
    }

    // 写入端指向实体自身。通过既有窄口更新数值，不直接接管实体的字典。
    private readonly IForceAction receiver;
    private readonly Behaviour owner;
    // 唯一关系记录：一个 Registration 对应一张登记单。
    private readonly Dictionary<EnvironmentRegistration, Binding> bindings = new Dictionary<EnvironmentRegistration, Binding>();
    // 以下均为可复用工作缓存；不是另一份关系或物理状态。
    private readonly List<EnvironmentRegistration> removalBuffer = new List<EnvironmentRegistration>();
    // 本次相位 2 采样到的唯一脚下来源；实体整体的多来源关系仍在 bindings 中。
    private EnvironmentRegistration groundSource;

    public EntityEnvironmentContext(IForceAction receiver, Behaviour owner)
    {
        this.receiver = receiver;
        this.owner = owner;
    }

    public bool IsActive => owner != null && owner.isActiveAndEnabled;
    /// <summary>
    /// 瞬时物理快照
    /// </summary>
    public EntityEnvironmentFrame Frame { get; private set; } = EntityEnvironmentFrame.Empty;

#if UNITY_EDITOR
    /// <summary>[EditorOnly] 绑定来源数，不包含已退出、仍在实体中衰减的动态速度。</summary>
    public int DebugSourceCount => bindings.Count;
#endif

    /// <summary>
    /// [ENV-03] 汇合 Ground / Region / Explicit 三种入口，判断需新增、保留还是撤销登记。
    /// 阅读重点是两个提前返回：退出只删自己的依据；重复进入只合并依据。首次接入才去 ENV-04。
    /// </summary>
    internal void SetAccess(EnvironmentRegistration source, Access access, bool present)
    {
        // 接入逻辑：本次入口报告离开；这不等于其它入口也失效。
        if (!present)
        {
            // 幂等清理：没有登记单就没有东西需要撤销。
            if (!bindings.TryGetValue(source, out Binding previous)) return;
            previous.access &= ~access;
            // 接入逻辑：所有依据都消失，才统一撤销三类效果。
            if (previous.access == 0) Detach(source);
            return;
        }
        // 生命周期保护：禁用或已销毁的双方不能新建关系。
        if (!IsActive || !source.IsActive) return;
        // 接入逻辑：相位 2 重复确认、或第二种入口接入，都不能再次初始化效果。
        if (bindings.TryGetValue(source, out Binding existing))
        {
            existing.access |= access;
            return;
        }
        //首次接入
        var binding = new Binding { access = access };
        bindings.Add(source, binding);
        //环境容器关联受力者相关数据
        source.Track(this);
        RegisterEffects(source, binding);
    }

    /// <summary>
    /// [ENV-04] 首次接入时按能力写入实体的三类原有容器，并在 Binding 上留下撤销凭据。
    /// 冰面的动态参数去 ENV-05；下一物理速度阶段如何消费这些容器去 ENV-06。
    /// </summary>
    private void RegisterEffects(EnvironmentRegistration source, Binding binding)
    {
        object provider = source.Provider;
        // 能力选择：有移速修饰就登记，即使值为零也有效；缺席由“没实现接口”表示。
        if (provider is IMovementSpeedModifier movement)
        {
            receiver.StatePowerRegistration(source, movement.MovementSpeedOffset);
            binding.hasMovement = true;
        }
        // 能力选择：持续速度独立于移速修饰，以 Registration 为稳定来源键。
        if (provider is IStateVelocitySource velocity)
        {
            receiver.AddSpeedStatus(source, velocity.StateVelocity);
            binding.hasStateVelocity = true;
        }
        // 能力选择：仅动态施力需要具体组件按受力者产生参数；动态键仍是该组件接口。
        if (provider is IDynamicEnvironmentForce dynamicForce)
        {
            receiver.AddForce(dynamicForce, dynamicForce.CreateEnvironmentForce(receiver));
            binding.dynamicForce = dynamicForce;
        }
    }

    /// <summary>
    /// [ENV-07] 撤销一份登记，先同步双方关系，再按登记单撤销实际效果。
    /// 普通离开经 ENV-03 到这里；源或实体禁用经 ENV-L1 的 ReleaseAll 到这里。
    /// </summary>
    internal void Detach(EnvironmentRegistration source)
    {
        // 幂等清理：双方可能先后发起解除，第二次直接结束。
        if (!bindings.TryGetValue(source, out Binding binding)) return;
        bindings.Remove(source);
        source.Untrack(this);
        // 按登记凭据撤销，而不是重新询问来源当前有什么能力。
        if (binding.hasMovement) receiver.StatePowerCancellation(source);
        if (binding.hasStateVelocity) receiver.RemoveSpeedStatus(source);
        // RemoveForce 只停止控制并切到 fadeAway；取得的速度仍归实体，不在这里清零。
        if (binding.dynamicForce != null) receiver.RemoveForce(binding.dynamicForce);
    }

    /// <summary>
    /// [ENV-02] 从 ENV-01 传来的脚下 Collider 开始，只按这三个步骤通读。
    /// 关系分支进入 ENV-03；采样分支生成 ENV-S1。脚下没换也要核对，以恢复原地重新启用的来源。
    /// </summary>
    public void RefreshGround(Collider2D ground)
    {
        CollectGroundSources(ground);
        SynchronizeGroundSources();
        SampleGroundFrame(ground);
    }

    /// <summary>核对事实，找来源：唯一环境宿主若允许脚下接入，就成为本次 Ground 来源。</summary>
    private void CollectGroundSources(Collider2D ground)
    {
        groundSource = null;
        // 几何有效性：没有有效脚下 Collider 时留下空来源，让下一步移除旧 Ground 依据。
        BasicPhysicalObject host = EnvironmentCapabilities.FindHost(ground);
        // 单入口约束：只从唯一宿主取得 Registration，不再遍历同物体的多个 MonoBehaviour。
        if (host is IEnvironmentSource source && source.EnvironmentRegistration.AcceptsGroundContact)
            groundSource = source.EnvironmentRegistration;
    }

    /// <summary>核对关系：失效源完全解除；换地只移除 Ground；本次来源逐个确认接入。</summary>
    private void SynchronizeGroundSources()
    {
        // 遍历保护：先收集再修改 bindings，避免枚举期间删除字典。
        removalBuffer.Clear();
        foreach (var entry in bindings)
            // 两类待处理项：任何已失效的来源，或已不在脚下的旧支撑来源（属于地面类型）。
            if (!entry.Key.IsActive ||
                ((entry.Value.access & Access.Ground) != 0 && entry.Key != groundSource))
                removalBuffer.Add(entry.Key);
        foreach (EnvironmentRegistration source in removalBuffer)
        {
            // 生命周期失效撤整份效果；仅离开脚下时，其余 Region / Explicit 依据仍可保留。
            if (!source.IsActive) Detach(source);
            else SetAccess(source, Access.Ground, false);
        }
        if (groundSource != null) SetAccess(groundSource, Access.Ground, true);
    }

    /// <summary>采样参数：表面读值不要求建立持续效果绑定，写成一帧只读结果供原消费点使用。</summary>
    private void SampleGroundFrame(Collider2D ground)
    {
        // 先找一次宿主，再读取它的多个接口；不从同物体的不同组件分别拼装参数。
        BasicPhysicalObject host = EnvironmentCapabilities.FindHost(ground);
        IGroundResponse response = host as IGroundResponse;
        IPlatformMotion platform = host as IPlatformMotion;
        Frame = new EntityEnvironmentFrame(response?.SlowingEffect ?? 1f, response?.JumpHeightNum ?? 0f,
            platform?.Delta ?? Vector2.zero);
    }

    /// <summary>[ENV-L1] 实体禁用入口：快照全部来源再逐个走 ENV-07，同步源端索引；可重复调用。</summary>
    public void ReleaseAll()
    {
        removalBuffer.Clear();
        removalBuffer.AddRange(bindings.Keys);
        foreach (EnvironmentRegistration source in removalBuffer) Detach(source);
        removalBuffer.Clear();
        groundSource = null;
        Frame = EntityEnvironmentFrame.Empty;
    }
}
