using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 每个环境来源一份身份：源禁用时按反向索引通知实体退出，也作为实体速度容器的来源键。
/// 职能：源端清理辅助与稳定来源身份；不再采样区域，区域重叠由实体相位 1 提供。
/// </summary>
public sealed class EnvironmentRegistration
{
    /// <summary>
    /// 引擎组件生命周期读取
    /// </summary>
    private readonly Behaviour owner;
    /// <summary>
    /// 纯c#类结构占用
    /// </summary>
    internal object Provider { get; }
    public bool IsActive => owner != null && owner.isActiveAndEnabled;

    /// <summary>
    /// 注册表本身：
    /// Context 在建立/解除 Binding 时 Track / Untrack；用于源禁用时找到所有接收者。
    /// </summary>
    private readonly HashSet<EntityEnvironmentContext> receivers = new HashSet<EntityEnvironmentContext>();
    //缓存，防止遍历中移除自身元素
    private readonly List<EntityEnvironmentContext> releaseBuffer = new List<EntityEnvironmentContext>();

    public EnvironmentRegistration(Behaviour owner, object provider)
    {
        this.owner = owner;
        Provider = provider;
    }

    internal void Track(EntityEnvironmentContext context) => receivers.Add(context);
    internal void Untrack(EntityEnvironmentContext context) => receivers.Remove(context);

    /// <summary>
    /// 显式接入窄口：只增删 Explicit 依据，不代替脚下或区域采样。
    /// </summary>
    public void SetExplicitAccess(IEnvironmentReceiver receiver, bool present)
    {
        // 安全：没有接收者时不能建 Explicit 依据。
        if (receiver == null) return;
        // 接入逻辑：只增删 Explicit；Ground / Region 仍由各自采样入口决定。
        receiver.EnvironmentContext.SetAccess(this, EntityEnvironmentContext.Access.Explicit, present);
    }

    /// <summary>[ENV-L1] 源禁用入口：按反向索引通知每个 Context 走 ENV-07，不清零实体已有速度。</summary>
    public void ReleaseAll()
    {
        // 遍历保护：Detach 会反向 Untrack，所以先复制名单再通知。
        releaseBuffer.Clear();
        releaseBuffer.AddRange(receivers);
        foreach (EntityEnvironmentContext context in releaseBuffer) context.Detach(this);
        releaseBuffer.Clear();
    }
}
