using System.Collections.Generic;

/// <summary>
/// 每个环境来源一份身份：源禁用时按反向索引通知实体退出，也作为实体速度容器的来源键。
/// 职能：源端清理辅助与稳定来源身份；不再采样区域，区域重叠由实体相位 1 提供。
/// </summary>
public sealed class EnvironmentRegistration
{
    /// <summary>同一来源提供有效性与效果能力；注册表不解释具体引擎的生命周期。</summary>
    internal IEnvironmentSource Source { get; }
    // 安全：空引用无效；存活与启用等具体规则由来源实现方判断。
    public bool IsActive => Source != null && Source.IsEnvironmentActive;

    /// <summary>
    /// 注册表本身：
    /// Context 在建立/解除 Binding 时 Track / Untrack；用于源禁用时找到所有接收者。
    /// </summary>
    private readonly HashSet<EntityEnvironmentContext> receivers = new HashSet<EntityEnvironmentContext>();
    //缓存，防止遍历中移除自身元素
    private readonly List<EntityEnvironmentContext> releaseBuffer = new List<EntityEnvironmentContext>();

    public EnvironmentRegistration(IEnvironmentSource source)
    {
        Source = source;
    }

    internal void Track(EntityEnvironmentContext context) => receivers.Add(context);
    internal void Untrack(EntityEnvironmentContext context) => receivers.Remove(context);

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
