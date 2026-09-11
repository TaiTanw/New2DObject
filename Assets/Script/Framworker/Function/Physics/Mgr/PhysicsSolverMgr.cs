using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using PhyData;

/// <summary>
/// 物理解算器（预测 AABB 接触对：本帧位移偏置 + 跨帧动态受力）。
/// 相位 4：全员 PositionPrediction 上报完毕后统一解算，保证顺序无关（N=1 冻结快照累加）。
/// </summary>
public class PhysicsSolverMgr : BaseMgr<PhysicsSolverMgr>
{
    // 允许保留的微小重叠。偏置只修正 rawDepth - ContactSlop，避免在零重叠附近反复成立/失效。
    const float ContactSlop = 0.002f;
    // 低于该值视为没有新的迎面挤压；低速重叠仍由位移偏置处理。
    const float ClosingSpeedEpsilon = 0.01f;
    // 沿用旧推箱原型的力/目标速度比例；最终速度仍由 ForceData.balanceSpeed 截止。
    const float ContactForceGain = 10f;
    const float MinEnvironmentSum = 0.0001f;

    /// <summary>
    /// 本帧预测世界快照。相位 3 由实体上报，相位 4 消费后清空。
    /// </summary>
    readonly List<PhysicalBoundingBox> projectingArray = new List<PhysicalBoundingBox>();

    // 接触力是有方向的：A 推 B 与 B 推 A 是两条不同的数据链路。
    // previous 用于在本帧扫描结束后找出退出的链路，并让对应动态力进入 fadeAway。
    HashSet<ContactForceLink> previousContactForceLinks = new HashSet<ContactForceLink>();//旧接触
    HashSet<ContactForceLink> currentContactForceLinks = new HashSet<ContactForceLink>();//本帧接触（每帧清空，帧状态快照

    // 同一实体同一挤出方向一帧只衰减一次；多个接触请求取最小保留率（最强阻挡）。
    readonly Dictionary<ContactAttenuationKey, float> pendingAttenuations = new Dictionary<ContactAttenuationKey, float>();//（每帧清空

#if UNITY_EDITOR
    readonly List<ContactPairDebugSnapshot> debugContacts = new List<ContactPairDebugSnapshot>();
    /// <summary>
    /// 编辑器只读接触快照；每个物理帧在解算开始时刷新。
    /// </summary>
    public IReadOnlyList<ContactPairDebugSnapshot> DebugContacts => debugContacts;
#endif


    public void Init()
    {
        // 第 4 相位：速度预测之后、位移应用之前
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(ContactForSolution, 4);
    }
    /// <summary>
    /// 添加投射（由实体 PositionPrediction 在相位 3 末调用）
    /// </summary>
    /// <param name="box"></param>
    public void AddPhyBoX(PhysicalBoundingBox box)
    {
        projectingArray.Add(box);
    }

    /// <summary>
    /// 数据链路：预测重叠 → 当帧 slop 位移偏置 → 水平闭合判断 → 跨帧动态力；
    /// 扫描结束后统一处理瞬时速度衰减，并把已经退出的旧接触力切换为 fadeAway。
    /// </summary>
    void ContactForSolution()
    {
        currentContactForceLinks.Clear();
        pendingAttenuations.Clear();
#if UNITY_EDITOR
        debugContacts.Clear();
#endif
        for (int i = 0; i < projectingArray.Count; i++)
        {
            PhysicalBoundingBox a = projectingArray[i];

            for (int j = i + 1; j < projectingArray.Count; j++)
            {
                PhysicalBoundingBox b = projectingArray[j];
                if (a.myPhyBox == null || b.myPhyBox == null)
                    continue;
                // v1 同时提供双方方位；v4 是预测 AABB 在 X/Y 轴上的原始重叠深度。
                Vector2 v1 = a.point - b.point;
                Vector2 v2 = new Vector2(Mathf.Abs(v1.x), Mathf.Abs(v1.y));
                Vector2 v3 = a.size + b.size;
                Vector2 v4 = v3 - v2;

                if (v4.x > 0 && v4.y > 0)
                {
                    bool separateOnX = v4.x <= v4.y;
                    float rawDepth = separateOnX ? v4.x : v4.y;
                    //小于某值时忽略嵌入深度
                    // slop 留在每个接触对内部；不能在最终 displacementOffset 上统一忽略小值。
                    float correctionDepth = Mathf.Max(rawDepth - ContactSlop, 0f);
                    //计算权重
                    float envA = Mathf.Max(0f, a.myPhyBox.EnvImpact);
                    float envB = Mathf.Max(0f, b.myPhyBox.EnvImpact);
                    float envSum = Mathf.Max(envA + envB, MinEnvironmentSum);
                    float weightA = envA / envSum;
                    float weightB = envB / envSum;

                    // 位移偏置与速度响应相互独立：只要重叠就恢复几何有效状态。
                    Vector2 offsetA = Vector2.zero;
                    Vector2 offsetB = Vector2.zero;
                    if (separateOnX)
                    {
                        float sign = v1.x >= 0f ? 1f : -1f;
                        offsetA.x = sign * correctionDepth * weightA;
                        offsetB.x = -sign * correctionDepth * weightB;
                    }
                    else
                    {
                        float sign = v1.y >= 0f ? 1f : -1f;
                        offsetA.y = sign * correctionDepth * weightA;
                        offsetB.y = -sign * correctionDepth * weightB;
                    }
                    a.myPhyBox.AccumulateDisplacementOffset(offsetA);
                    b.myPhyBox.AccumulateDisplacementOffset(offsetB);

                    // 当前最小实现只传播水平接触力；Y 轴只承担几何挤出。
                    ContactSpeedResolution speedResolution = TryApplyHorizontalContactForce(
                        a.myPhyBox,
                        b.myPhyBox,
                        v1,
                        separateOnX,
                        envSum);

#if UNITY_EDITOR
                    Vector2 normalA = separateOnX
                        ? new Vector2(v1.x >= 0f ? 1f : -1f, 0f)
                        : new Vector2(0f, v1.y >= 0f ? 1f : -1f);
                    debugContacts.Add(new ContactPairDebugSnapshot
                    {
                        entityA = a.myPhyBox,
                        entityB = b.myPhyBox,
                        predictedCenterA = a.point,
                        predictedCenterB = b.point,
                        separateOnX = separateOnX,
                        normalA = normalA,
                        depth = rawDepth,
                        correctionDepth = correctionDepth,
                        closingSpeed = speedResolution.closingSpeed,
                        aAppliedForce = speedResolution.aAppliedForce,
                        bAppliedForce = speedResolution.bAppliedForce,
                        targetSpeedAToB = speedResolution.targetSpeedAToB,
                        targetSpeedBToA = speedResolution.targetSpeedBToA,
                        weightA = weightA,
                        weightB = weightB,
                        offsetA = offsetA,
                        offsetB = offsetB
                    });
#endif
                }
            }
        }
        //调用实体方法衰减要求的被动速度
        ApplyPendingAttenuations();
        ReconcileContactForceLinks();

        // 本帧预测快照用完即清；下一帧由各实体重新上报。
        projectingArray.Clear();
    }

    /// <summary>
    /// 水平接触速度解算。
    /// separateSign 表示物体远离接触面的挤出方向；速度与它异号才表示朝接触面运动。
    /// 总速度负责判断和计算传递目标，实际速度通过接收者的动态力容器跨帧累计。
    /// </summary>
    ContactSpeedResolution TryApplyHorizontalContactForce(
        BasicEntity entityA,
        BasicEntity entityB,
        Vector2 centerDelta,
        bool separateOnX,
        float envSum)
    {
        ContactSpeedResolution result = new ContactSpeedResolution();
        //暂留缺口，不处理垂直方向
        if (!separateOnX)
            return result;
        //得到总速度
        Vector2 velocityA = entityA.GetPlanarVelocity();
        Vector2 velocityB = entityB.GetPlanarVelocity();
        float separateSignA = centerDelta.x >= 0f ? 1f : -1f;
        float separateSignB = -separateSignA;

        // 仅看单体方向会把两个同速同行的贴合物误判为继续挤压；相对闭合速度先排除该情况。*
        result.closingSpeed = -(velocityA.x - velocityB.x) * separateSignA;
        if (result.closingSpeed <= ClosingSpeedEpsilon)
            return result;

        if (velocityA.x * separateSignA < -ClosingSpeedEpsilon)
        {
            result.targetSpeedAToB = RegisterDirectedContactForce(
                entityA,
                entityB,
                velocityA.x,
                separateSignA,
                envSum);
            result.aAppliedForce = true;
        }

        if (velocityB.x * separateSignB < -ClosingSpeedEpsilon)
        {
            result.targetSpeedBToA = RegisterDirectedContactForce(
                entityB,
                entityA,
                velocityB.x,
                separateSignB,
                envSum);
            result.bAppliedForce = true;
        }

        return result;
    }

    /// <summary>
    /// 注册 source → receiver 的本帧接触力。
    /// receiverWeight 决定当前最小模型中的目标被动速度；动态力刷新时保留 receiver 已累计的 speedStacking。
    /// </summary>
    float RegisterDirectedContactForce(
        BasicEntity source,
        BasicEntity receiver,
        float sourceAxisSpeed,
        float sourceSeparateSign,
        float envSum)
    {
        ContactForceLink link = new ContactForceLink(source, receiver);
        currentContactForceLinks.Add(link);

        float receiverWeight = Mathf.Clamp01(Mathf.Max(0f, receiver.EnvImpact) / envSum);
        float targetSpeed = sourceAxisSpeed * receiverWeight;
        receiver.SetOrUpdateDynamicForce(
            source,
            targetSpeed * ContactForceGain,
            Mathf.Abs(targetSpeed),
            0f);

        // 百分比削弱只发生在有向接触刚成立时；保持接触不会逐帧相乘而指数缩小。
        if (!previousContactForceLinks.Contains(link))
            QueueContactAttenuation(source, sourceSeparateSign, receiverWeight);

        return targetSpeed;
    }

    /// <summary>
    /// 汇总本帧新接触对施力者的瞬时速度衰减请求。
    /// key 使用实体 + 挤出方向，保证同一方向一帧只执行一次。
    /// </summary>
    void QueueContactAttenuation(BasicEntity entity, float separateSign, float retainRatio)
    {
        ContactAttenuationKey key = new ContactAttenuationKey(entity, separateSign);
        retainRatio = Mathf.Clamp01(retainRatio);
        if (pendingAttenuations.TryGetValue(key, out float oldRetainRatio))
            pendingAttenuations[key] = Mathf.Min(oldRetainRatio, retainRatio);
        else
            pendingAttenuations.Add(key, retainRatio);
    }
    /// <summary>
    /// 应用待处理施力物速度衰减
    /// </summary>
    void ApplyPendingAttenuations()
    {
        foreach (KeyValuePair<ContactAttenuationKey, float> pair in pendingAttenuations)
        {
            if (pair.Key.entity != null)
            {
                // 容器在相位 4 被修改；phyHSpeed 会在下一帧相位 3 重算时反映该变化。
                pair.Key.entity.AttenuateTransientContactSpeed(
                    pair.Key.separateSign,
                    pair.Value);
            }
        }
    }

    /// <summary>
    /// 本帧未重新成立的旧有向关系已退出：接收者不再被持续加速，旧累计速度改走 fadeAway。
    /// </summary>
    void ReconcileContactForceLinks()
    {
        foreach (ContactForceLink oldLink in previousContactForceLinks)
        {
            if (!currentContactForceLinks.Contains(oldLink) &&
                oldLink.source != null && oldLink.receiver != null)
            {
                oldLink.receiver.RemoveForce(oldLink.source);
            }
        }

        HashSet<ContactForceLink> temp = previousContactForceLinks;
        previousContactForceLinks = currentContactForceLinks;
        currentContactForceLinks = temp;
        currentContactForceLinks.Clear();
    }

    struct ContactSpeedResolution
    {
        public float closingSpeed;
        public bool aAppliedForce;
        public bool bAppliedForce;
        public float targetSpeedAToB;
        public float targetSpeedBToA;
    }

    struct ContactForceLink : IEquatable<ContactForceLink>
    {
        public readonly BasicEntity source;
        public readonly BasicEntity receiver;
        readonly int sourceId;
        readonly int receiverId;

        public ContactForceLink(BasicEntity source, BasicEntity receiver)
        {
            this.source = source;
            this.receiver = receiver;
            sourceId = source != null ? source.GetInstanceID() : 0;
            receiverId = receiver != null ? receiver.GetInstanceID() : 0;
        }

        public bool Equals(ContactForceLink other)
        {
            return sourceId == other.sourceId && receiverId == other.receiverId;
        }

        public override bool Equals(object obj)
        {
            return obj is ContactForceLink && Equals((ContactForceLink)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (sourceId * 397) ^ receiverId;
            }
        }
    }

    struct ContactAttenuationKey : IEquatable<ContactAttenuationKey>
    {
        public readonly BasicEntity entity;
        public readonly float separateSign;
        readonly int entityId;
        readonly int direction;

        public ContactAttenuationKey(BasicEntity entity, float separateSign)
        {
            this.entity = entity;
            direction = separateSign >= 0f ? 1 : -1;
            this.separateSign = direction;
            entityId = entity != null ? entity.GetInstanceID() : 0;
        }

        public bool Equals(ContactAttenuationKey other)
        {
            return entityId == other.entityId && direction == other.direction;
        }

        public override bool Equals(object obj)
        {
            return obj is ContactAttenuationKey && Equals((ContactAttenuationKey)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (entityId * 397) ^ direction;
            }
        }
    }
}
