using PhyData;
using UnityEngine;

/// <summary>
/// 实体进入力场区域时，用 add / maxSpeed 给出动态力初值；后续每帧沿用这组参数。
/// 职能：具体区域算法提供者；接入从公共 ENV-R1 汇入 ENV-03，不自行登记或清理实体。
/// </summary>
public class ForceField : BasicPhysicalObject, IDynamicEnvironmentForce, IMovementSpeedModifier
{
    [SerializeField] protected float speedChangeNum; // 主动速度加性修饰，旧序列化名称保留。
    public float MovementSpeedOffset => speedChangeNum;

    [SerializeField] protected float maxSpeed;
    [SerializeField] protected float add;

    protected override bool UsesTriggerRegion => true;

    public ForceData CreateEnvironmentForce(IForceAction receiver)
    {
        ForceData force = new ForceData();
        force.Init(add, 0, maxSpeed);
        return force;
    }

    public void ForceCalculation(IForceAction receiver)
    {
        // 固定施力参数沿用进入时的值；不在每帧重新初始化实体的累计速度。
    }
}
