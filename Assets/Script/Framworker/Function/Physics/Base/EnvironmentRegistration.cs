using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 例如力场要知道区域内有哪些实体，自己禁用时还要找到所有受影响实体通知退出。
/// 每个来源组合一个本对象完成这些通知；真正“登记了什么”的记录在各实体 Context 内。
/// 职能：源端接入辅助与清理反向索引，兼作旧状态容器的来源键；不存数值或决定碰撞响应。
/// 阅读：区域旁支 ENV-R1；普通脚下主线从 BasicEntity 的 ENV-01 开始。
/// </summary>
public sealed class EnvironmentRegistration : IApplyingForceAction
{
    private readonly Behaviour owner;
    internal object Provider { get; }
    /// <summary>
    /// 是登记地面物理效果（当外界符合条件拿到此环境影响时，是否同意）
    /// </summary>
    public bool AcceptsGroundContact { get; }
    public bool IsActive => owner != null && owner.isActiveAndEnabled;

    // Context 在建立/解除 Binding 时 Track / Untrack；用于源禁用时找到所有接收者。
    private readonly HashSet<EntityEnvironmentContext> receivers = new HashSet<EntityEnvironmentContext>();
    private readonly List<EntityEnvironmentContext> releaseBuffer = new List<EntityEnvironmentContext>();
    // 上次/本次区域采样集合只作差集与缓存复用；它们不决定效果是否已经登记。
    private HashSet<EntityEnvironmentContext> regionReceivers = new HashSet<EntityEnvironmentContext>();
    private HashSet<EntityEnvironmentContext> nextRegionReceivers = new HashSet<EntityEnvironmentContext>();
    private readonly List<Collider2D> sourceColliders = new List<Collider2D>();
    private readonly List<Collider2D> overlaps = new List<Collider2D>();

    public EnvironmentRegistration(Behaviour owner, object provider, bool acceptsGroundContact = false)
    {
        this.owner = owner;
        Provider = provider;
        AcceptsGroundContact = acceptsGroundContact;
    }

    internal void Track(EntityEnvironmentContext context) => receivers.Add(context);
    internal void Untrack(EntityEnvironmentContext context) => receivers.Remove(context);

    // 兼容旧入口。只有实现实体环境接收接口的受力者才参与这一套关系管理。
    public void OnPhyEnter(IForceAction receiver)
    {
        // 接入协议：只有持有 Context 的实体才能登记 Explicit 依据。
        if (receiver is IEnvironmentReceiver environment)
            environment.EnvironmentContext.SetAccess(this, EntityEnvironmentContext.Access.Explicit, true);
    }

    public void OnPhyExit(IForceAction receiver)
    {
        // 只撤 Explicit；Ground / Region 仍由各自的采样入口决定。
        if (receiver is IEnvironmentReceiver environment)
            environment.EnvironmentContext.SetAccess(this, EntityEnvironmentContext.Access.Explicit, false);
    }

    /// <summary>[ENV-L1] 源禁用入口：按反向索引通知每个 Context 走 ENV-07，不清零实体已有速度。</summary>
    public void ReleaseAll()
    {
        // 遍历保护：Detach 会反向 Untrack，所以先复制名单再通知。
        releaseBuffer.Clear();
        releaseBuffer.AddRange(receivers);
        foreach (EntityEnvironmentContext context in releaseBuffer) context.Detach(this);
        releaseBuffer.Clear();
        regionReceivers.Clear();
        nextRegionReceivers.Clear();
    }

    /// <summary>
    /// [ENV-R1] 力场/水域的旁支入口：相位 2 采样区域，再汇入与脚下主线相同的 ENV-03。
    /// 先收集本次接收者，撤销离开者的 Region，再确认仍在区域者；具体 Collider 筛选可第二遍读。
    /// </summary>
    public void RefreshTriggerRegion()
    {
        // 生命周期保护：失效源不能继续采样，先撤销已有关系。
        if (!IsActive) { ReleaseAll(); return; }
        CollectRegionReceivers();
        foreach (EntityEnvironmentContext context in regionReceivers)
            // 区域逻辑：仅当本次所有重叠都已消失，才撤该实体的 Region 依据。
            if (!nextRegionReceivers.Contains(context))
                context.SetAccess(this, EntityEnvironmentContext.Access.Region, false);
        // 每次采样确认接入，而不是仅比较旧集合：受力者禁用清理后可在原区域内恢复。
        foreach (EntityEnvironmentContext context in nextRegionReceivers)
            context.SetAccess(this, EntityEnvironmentContext.Access.Region, true);
        var previous = regionReceivers;
        regionReceivers = nextRegionReceivers;
        nextRegionReceivers = previous;
    }

    /// <summary>查询细节：多个源 Trigger、多个接收 Collider 最终合并为一份实体集合。</summary>
    private void CollectRegionReceivers()
    {
        nextRegionReceivers.Clear();
        owner.GetComponents(sourceColliders);
        foreach (Collider2D trigger in sourceColliders)
        {
            // 区域选择：仅启用的 Trigger 参与，不把同物体上的实体阻挡 Collider 当区域。
            if (!trigger.enabled || !trigger.isTrigger) continue;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(trigger.gameObject.layer));
            overlaps.Clear();
            trigger.OverlapCollider(filter, overlaps);
            foreach (Collider2D other in overlaps)
            {
                // 有效性保护：销毁、禁用或不在激活层级的 Collider 不可作为接收对象。
                if (other == null || !other.enabled || !other.gameObject.activeInHierarchy) continue;
                // 区域规则：排除自身物体与显式忽略的碰撞对；层矩阵已在上面的 filter 中筛选。
                if (other.gameObject == owner.gameObject || Physics2D.GetIgnoreCollision(trigger, other)) continue;
                // 与 Unity 触发条件一致：至少一方具有 Rigidbody2D。
                if (trigger.attachedRigidbody == null && other.attachedRigidbody == null) continue;
                IEnvironmentReceiver receiver = other.attachedRigidbody != null
                    ? other.attachedRigidbody.GetComponent<IEnvironmentReceiver>() : null;
                // 复合碰撞体先找刚体上的实体；没有时才退回 Collider 所在物体。
                if (receiver == null) receiver = other.GetComponent<IEnvironmentReceiver>();
                // 接入规则：只接收拥有 Context 且已启用的实体；HashSet 合并多个 Collider 命中。
                if (receiver != null && receiver.EnvironmentContext.IsActive)
                    nextRegionReceivers.Add(receiver.EnvironmentContext);
            }
        }
    }
}
