/// <summary>区域状态效果；旧组件名称保留，接入生命周期由公共外壳转发。</summary>
public class pond : BasicPhysicalObject
{
    protected override bool UsesTriggerRegion => true;
}