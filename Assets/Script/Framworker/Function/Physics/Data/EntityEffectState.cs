using System.Collections.Generic;
using PhyData;
using UnityEngine;

/// <summary>环境来源的参数与限时命令；动量归 Rigidbody2D，不保留实体接触力或速度尾效。</summary>
internal sealed class EntityEffectState
{
    private readonly Dictionary<EnvironmentRegistration, float> movementModifiers = new Dictionary<EnvironmentRegistration, float>();
    private readonly Dictionary<EnvironmentRegistration, Vector2> stateVelocities = new Dictionary<EnvironmentRegistration, Vector2>();
    private readonly List<SpeedStackData> timedSpeeds = new List<SpeedStackData>();
    private readonly Dictionary<IDynamicAddForce, ForceData> dynamicForces = new Dictionary<IDynamicAddForce, ForceData>();
    private readonly List<IDynamicAddForce> forceSources = new List<IDynamicAddForce>();
    private IForceAction receiver;
    private bool isRecalculate;
#if UNITY_EDITOR
    public int DebugMovementModifierCount => movementModifiers.Count;
    public int DebugStateVelocityCount => stateVelocities.Count;
    public int DebugDynamicForceCount => dynamicForces.Count;
#endif
    public void BindReceiver(IForceAction value) => receiver = value;
    public void ResetTransientMotion() { timedSpeeds.Clear(); isRecalculate = true; }
    public void ClearMotionParameters()
    {
        movementModifiers.Clear();
        stateVelocities.Clear();
        dynamicForces.Clear();
        forceSources.Clear();
        ResetTransientMotion();
    }
    public void RegisterMovementModifier(EnvironmentRegistration source, float offset) { movementModifiers[source] = offset; isRecalculate = true; }
    public void RemoveMovementModifier(EnvironmentRegistration source) { movementModifiers.Remove(source); isRecalculate = true; }
    /// <summary>
    /// 处理主动速度修饰
    /// </summary>
    /// <param name="movement"></param>
    public void UpdateMovementModifier(PlayerPhysicsData movement)
    {
        if (!isRecalculate) return;
        float sum = 0f;
        foreach (float offset in movementModifiers.Values) sum += offset;
        movement.nowPhyNum = Mathf.Clamp(sum, -0.95f, 4f);
        isRecalculate = false;
    }
    public void AddStateVelocity(EnvironmentRegistration source, Vector2 velocity) => stateVelocities[source] = velocity;
    public void RemoveStateVelocity(EnvironmentRegistration source) => stateVelocities.Remove(source);
    public void AddTimedSpeed(float duration, float speed, float envImpact) => timedSpeeds.Add(new SpeedStackData
        { responseTimer1 = Time.time + duration, hspeed = speed * envImpact });
    /// <summary>
    /// 更新速度附加命令
    /// </summary>
    /// <param name="movement"></param>
    public void UpdatePassiveSpeed(PlayerPhysicsData movement)
    {
        Vector2 speed = Vector2.zero;
        foreach (Vector2 velocity in stateVelocities.Values) speed += velocity;
        for (int i = timedSpeeds.Count - 1; i >= 0; i--)
        {
            if (timedSpeeds[i].responseTimer1 < Time.time) timedSpeeds.RemoveAt(i);
            else speed.x += timedSpeeds[i].hspeed;
        }
        movement.phyHSpeed = speed.x;
        movement.phyVSpeed = speed.y;
    }
    public void AddDynamicForce(IDynamicAddForce source, ForceData force) => dynamicForces[source] = force;
    public void ChangeDynamicForce(IDynamicAddForce source, float force)
    {
        if (!dynamicForces.TryGetValue(source, out ForceData data)) return;
        data.parameters = new DynamicForceParameters(force, data.balanceSpeed, data.recoverySpeed);
        dynamicForces[source] = data;
    }
    public void ChangeDynamicType(IDynamicAddForce source, E_PhyForceType type)
    {
        if (!dynamicForces.TryGetValue(source, out ForceData data)) return;
        data.type = type;
        dynamicForces[source] = data;
    }
    public void RemoveDynamicForce(IDynamicAddForce source) => dynamicForces.Remove(source);

    /// <summary>先处理动态来源，再施加自身阻力；返回本步被动水平速度增量，不保存跨帧动量。</summary>
    /// <param name="selfResistance">已应用地面倍率的自身阻力，环境影响系数和步长在本方法中计算一次。</param>
    public float CalculateDynamicVelocityDelta(float initialPassiveVelocityX, float envImpact, float dt, float selfResistance)
    {
        float nextPassiveVelocityX = initialPassiveVelocityX;
        forceSources.Clear();
        //副本
        forceSources.AddRange(dynamicForces.Keys);
        foreach (IDynamicAddForce source in forceSources)
        {
            //更新环境自身特殊动态力规则
            source.ForceCalculation(receiver);
            //防止环境主动的更新对字典产生修改导致报错
            if (!dynamicForces.TryGetValue(source, out ForceData data)) continue;
            if (data.type == E_PhyForceType.controlRecovery)
                nextPassiveVelocityX = Mathf.MoveTowards(nextPassiveVelocityX, 0f,
                    Mathf.Max(0f, data.recoverySpeed * envImpact) * dt);
            else if (data.type == E_PhyForceType.apply)
            {
                float change = data.Force * envImpact * dt;
                // 已超过该源限速时不骤然清掉其它原生动量；它仍能沿反方向减速。
                if (change > 0f && nextPassiveVelocityX < data.balanceSpeed)
                    nextPassiveVelocityX = Mathf.Min(data.balanceSpeed, nextPassiveVelocityX + change);
                else if (change < 0f && nextPassiveVelocityX > -data.balanceSpeed)
                    nextPassiveVelocityX = Mathf.Max(-data.balanceSpeed, nextPassiveVelocityX + change);
            }
        }
        // 自身制动始终存在；受控复原是来源提供的额外减速，施加阶段的上限不补偿这一步制动。
        nextPassiveVelocityX = Mathf.MoveTowards(nextPassiveVelocityX, 0f,
            Mathf.Max(0f, selfResistance) * Mathf.Max(0f, envImpact) * dt);
        return nextPassiveVelocityX - initialPassiveVelocityX;
    }
}
