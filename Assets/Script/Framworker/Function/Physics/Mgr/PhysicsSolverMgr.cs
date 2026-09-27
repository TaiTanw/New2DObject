using System.Collections;
using System;
using System.Collections.Generic;
using UnityEngine;
using PhyData;

/// <summary>
/// 相位 4 的全体实体接触协调器，读取相位 3 上报的预测 AABB 与同帧运动快照。
/// 先累加预测重叠的纠偏，再调用实体求环境可行请求；环境裁剪产生残余接触时最多回算两轮。
/// 最终请求确定后清理受阻速度、登记可行的有向接触力，并撤销退出的旧关系。
/// 本类不移动刚体；接触力由实体在下一帧积分，移动请求由各实体在相位 5 提交。
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
    // F_6.2_7-2：环境裁剪后的接触对最多再回算两轮，限制多实体时的查询成本。
    const int MaxContactFeedbackPasses = 2;

    /// <summary>
    /// 本帧预测世界快照。相位 3 由实体上报，相位 4 消费后清空。
    /// </summary>
    readonly List<PhysicalBoundingBox> projectingArray = new List<PhysicalBoundingBox>();

    // 接触力是有方向的：A 推 B 与 B 推 A 是两条不同的数据链路。
    // previous 用于在本帧扫描结束后找出退出的链路，并让对应动态力进入 fadeAway。
    HashSet<ContactForceLink> previousContactForceLinks = new HashSet<ContactForceLink>();//旧接触
    HashSet<ContactForceLink> currentContactForceLinks = new HashSet<ContactForceLink>(); // 本帧有向接触关系（每帧清空）

    // 同一实体同一挤出方向一帧只衰减一次；多个接触请求取最小保留率（最强阻挡）。
    readonly Dictionary<ContactAttenuationKey, float> pendingAttenuations = new Dictionary<ContactAttenuationKey, float>(); // 新接触速度衰减请求（每帧清空）

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
    /// 相位 4 总入口，建议先沿调用顺序阅读，再展开各私有步骤。
    /// ① 预测 AABB 重叠按最小深度轴分配接触偏置；② 求环境请求并有限次回算剩余水平重叠；
    /// ③ 按最终阻挡清理速度；④ 登记可行接触力、处理新接触衰减与旧关系退出。
    /// 相位 3 快照保持不变，当前请求可被重写；结束时清空上报列表，等待下一物理帧。
    /// </summary>
    void ContactForSolution()
    {
        currentContactForceLinks.Clear();
        pendingAttenuations.Clear();
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

                }
            }
        }
        // F_6.2_7-3：先求环境可行位移，再把静态裁剪反馈给实体接触对。
        ResolveEnvironmentRequests();
        ResolveResidualHorizontalOverlaps();
        foreach (PhysicalBoundingBox prediction in projectingArray)
            if (prediction.myPhyBox != null)
                prediction.myPhyBox.ClearTransientSpeedForFinalBlock();
        // F_6.2_7-4：只有几何与环境都允许的接触，才登记下一帧的有向力。
        RegisterFeasibleContactForces();
        //调用实体方法衰减要求的被动速度
        ApplyPendingAttenuations();
        ReconcileContactForceLinks();

        // 本帧预测快照用完即清；下一帧由各实体重新上报。
        projectingArray.Clear();
    }

    /// <summary>
    /// 逐实体查询“基础预测＋接触偏置”的环境约束，再合成为本轮请求位移。
    /// 首轮及每轮接触反馈结束后复用这个入口；只更新结果，不改变 Unity 世界位置。
    /// </summary>
    void ResolveEnvironmentRequests()
    {
        foreach (PhysicalBoundingBox prediction in projectingArray)
            if (prediction.myPhyBox != null)
            {
                prediction.myPhyBox.ProbeCandidateEnvironment(prediction);
                prediction.myPhyBox.FinalizeRequestedDisplacement();
            }
    }

    /// <summary>
    /// F_6.2_7-3：环境裁剪可能破坏首轮接触分离，用本轮请求终点再检查水平残余重叠。
    /// 朝挤出方向被静态障碍挡住的一侧不分担纠偏，其余实体按 EnvImpact 分担剩余量。
    /// 一轮中累加偏置，轮末统一重查环境；无新纠偏就停止，最多 MaxContactFeedbackPasses 轮。
    /// 只补水平接触反馈，不保证任意多箱链在固定轮数内完全收敛。
    /// </summary>
    void ResolveResidualHorizontalOverlaps()
    {
        for (int pass = 0; pass < MaxContactFeedbackPasses; pass++)
        {
            bool changed = false;
            for (int i = 0; i < projectingArray.Count; i++)
            {
                PhysicalBoundingBox a = projectingArray[i];
                if (a.myPhyBox == null) continue;
                for (int j = i + 1; j < projectingArray.Count; j++)
                {
                    PhysicalBoundingBox b = projectingArray[j];
                    if (b.myPhyBox == null) continue;
                    Vector2 centerA = a.point - a.motionFrame.plannedWorldDelta + a.myPhyBox.RequestedWorldDelta;
                    Vector2 centerB = b.point - b.motionFrame.plannedWorldDelta + b.myPhyBox.RequestedWorldDelta;
                    Vector2 delta = centerA - centerB;
                    Vector2 overlap = a.size + b.size - new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                    // 已在容差内、竖直已分离或应沿 Y 挤出的接触，不进入水平反馈。
                    if (overlap.x <= ContactSlop || overlap.y <= 0f || overlap.x > overlap.y) continue;

                    float signA = delta.x >= 0f ? 1f : -1f;
                    float weightA = a.myPhyBox.BlockedHorizontalDirection == signA
                        ? 0f : Mathf.Max(0f, a.myPhyBox.EnvImpact);
                    float weightB = b.myPhyBox.BlockedHorizontalDirection == -signA
                        ? 0f : Mathf.Max(0f, b.myPhyBox.EnvImpact);
                    float sum = weightA + weightB;
                    // 双方都无法分担纠偏时保持本轮请求，最终施力检查会排除未解开的接触。
                    if (sum <= MinEnvironmentSum) continue;
                    float correction = overlap.x - ContactSlop;
                    a.myPhyBox.AccumulateDisplacementOffset(new Vector2(signA * correction * weightA / sum, 0f));
                    b.myPhyBox.AccumulateDisplacementOffset(new Vector2(-signA * correction * weightB / sum, 0f));
                    changed = true;
                }
            }
            if (!changed) return;
            ResolveEnvironmentRequests();
        }
    }

    /// <summary>
    /// 首轮预测识别接触轴与双方方向，最终请求终点判断这对接触是否仍贴合且已经分离。
    /// 仅水平有效接触进入闭合速度与有向力登记；残余深度嵌入或已分离的关系不会继续登记。
    /// 此时才登记下一帧动态力，避免先施力、后被环境裁剪推翻；编辑器报告不参与判定。
    /// </summary>
    void RegisterFeasibleContactForces()
    {
        for (int i = 0; i < projectingArray.Count; i++)
        {
            PhysicalBoundingBox a = projectingArray[i];
            if (a.myPhyBox == null) continue;
            for (int j = i + 1; j < projectingArray.Count; j++)
            {
                PhysicalBoundingBox b = projectingArray[j];
                if (b.myPhyBox == null) continue;
                Vector2 delta = a.point - b.point;
                Vector2 overlap = a.size + b.size - new Vector2(Mathf.Abs(delta.x), Mathf.Abs(delta.y));
                if (overlap.x <= 0f || overlap.y <= 0f) continue;
                bool separateOnX = overlap.x <= overlap.y;
                float envA = Mathf.Max(0f, a.myPhyBox.EnvImpact);
                float envB = Mathf.Max(0f, b.myPhyBox.EnvImpact);
                float envSum = Mathf.Max(envA + envB, MinEnvironmentSum);
#if UNITY_EDITOR
                float rawDepth = separateOnX ? overlap.x : overlap.y;
                float correctionDepth = Mathf.Max(rawDepth - ContactSlop, 0f);
                float weightA = envA / envSum;
                float weightB = envB / envSum;
                float sign = separateOnX ? (delta.x >= 0f ? 1f : -1f) : (delta.y >= 0f ? 1f : -1f);
                Vector2 offsetA = separateOnX ? new Vector2(sign * correctionDepth * weightA, 0f)
                    : new Vector2(0f, sign * correctionDepth * weightA);
                Vector2 offsetB = separateOnX ? new Vector2(-sign * correctionDepth * weightB, 0f)
                    : new Vector2(0f, -sign * correctionDepth * weightB);
#endif

                Vector2 finalA = a.point - a.motionFrame.plannedWorldDelta + a.myPhyBox.RequestedWorldDelta;
                Vector2 finalB = b.point - b.motionFrame.plannedWorldDelta + b.myPhyBox.RequestedWorldDelta;
                Vector2 finalDelta = finalA - finalB;
                Vector2 finalOverlap = a.size + b.size - new Vector2(Mathf.Abs(finalDelta.x), Mathf.Abs(finalDelta.y));
                ContactSpeedResolution speedResolution = default;
                // 回算仍留下深度嵌入时，不能把这对尚未解开的接触继续登记为推力。
                if (separateOnX && finalOverlap.x >= -ContactSlop &&
                    finalOverlap.x <= ContactSlop + 0.001f && finalOverlap.y > 0f)
                    speedResolution = TryApplyHorizontalContactForce(a, b, delta, separateOnX, envSum);

#if UNITY_EDITOR
                // 观测中的 offsetA/B 仍表示首轮预测分配；最终请求位移由实体观测单独记录。
                PhysicsEditorObservationBridge.ReportContactPair(
                    a.myPhyBox, b.myPhyBox, a.point, b.point, delta, separateOnX,
                    rawDepth, correctionDepth, speedResolution.closingSpeed,
                    speedResolution.aAppliedForce, speedResolution.bAppliedForce,
                    speedResolution.targetSpeedAToB, speedResolution.targetSpeedBToA,
                    weightA, weightB, offsetA, offsetB);
#endif
            }
        }
    }

    /// <summary>
    /// 水平接触速度解算。
    /// separateSign 表示物体远离接触面的挤出方向；速度与它异号才表示朝接触面运动。
    /// 同帧自身运动负责判断和计算传递目标；平台携带只参与预测位置，不当作自身施力速度。
    /// </summary>
    ContactSpeedResolution TryApplyHorizontalContactForce(
        PhysicalBoundingBox a,
        PhysicalBoundingBox b,
        Vector2 centerDelta,
        bool separateOnX,
        float envSum)
    {
        ContactSpeedResolution result = new ContactSpeedResolution();
        //暂留缺口，不处理垂直方向
        if (!separateOnX)
            return result;
        BasicEntity entityA = a.myPhyBox;
        BasicEntity entityB = b.myPhyBox;
        // 相位 3 已直接保存经支撑面处理的自身速度；平台携带仍只参与预测位置。
        float velocityA = a.motionFrame.motionVelocity.x;
        float velocityB = b.motionFrame.motionVelocity.x;
        float separateSignA = centerDelta.x >= 0f ? 1f : -1f;
        float separateSignB = -separateSignA;

        // 仅看单体方向会把两个同速同行的贴合物误判为继续挤压；相对闭合速度先排除该情况。*
        result.closingSpeed = -(velocityA - velocityB) * separateSignA;
        if (result.closingSpeed <= ClosingSpeedEpsilon)
            return result;

        // 接收者已被静态环境挡住时，本帧接触不应重新把力登记为 apply。
        if (velocityA * separateSignA < -ClosingSpeedEpsilon &&
            entityA.BlockedHorizontalDirection != -separateSignA &&
            entityB.BlockedHorizontalDirection != -separateSignA)
        {
            result.targetSpeedAToB = RegisterDirectedContactForce(
                entityA,
                entityB,
                velocityA,
                separateSignA,
                envSum);
            result.aAppliedForce = true;
        }

        if (velocityB * separateSignB < -ClosingSpeedEpsilon &&
            entityB.BlockedHorizontalDirection != -separateSignB &&
            entityA.BlockedHorizontalDirection != -separateSignB)
        {
            result.targetSpeedBToA = RegisterDirectedContactForce(
                entityB,
                entityA,
                velocityB,
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
            new DynamicForceParameters(targetSpeed * ContactForceGain, Mathf.Abs(targetSpeed), 0f));

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
                oldLink.receiver.ClearBlockedContactSpeed(oldLink.source);
                oldLink.receiver.RemoveForce(oldLink.source);
            }
        }

        HashSet<ContactForceLink> temp = previousContactForceLinks;
        previousContactForceLinks = currentContactForceLinks;
        currentContactForceLinks = temp;
        currentContactForceLinks.Clear();
    }

    /// <summary>单个接触对的水平读速与登记结果；不持有接收者的累计速度。</summary>
    struct ContactSpeedResolution
    {
        public float closingSpeed; // 相对闭合速度
        public bool aAppliedForce; // A→B 本帧已登记
        public bool bAppliedForce; // B→A 本帧已登记
        public float targetSpeedAToB; // A→B 目标速度
        public float targetSpeedBToA; // B→A 目标速度
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
