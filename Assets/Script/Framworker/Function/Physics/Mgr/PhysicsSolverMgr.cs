using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PhyData;

/// <summary>
/// 物理解算器（预测 AABB 接触对：本帧位移偏置 + 下帧速度意图）
/// 相位 4：全员 PositionPrediction 上报完毕后统一解算，保证顺序无关（N=1 冻快照累加）
/// </summary>
public class PhysicsSolverMgr : BaseMgr<PhysicsSolverMgr>
{
    /// <summary>
    /// 投射数组（本帧预测世界快照，仅可推实体，不含静态墙）
    /// </summary>
    List<PhysicalBoundingBox> projectingArray = new List<PhysicalBoundingBox>();

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
    /// 接触对解算
    /// </summary>
    void ContactForSolution()
    {
#if UNITY_EDITOR
        debugContacts.Clear();
#endif
        for (int i = 0; i < projectingArray.Count; i++)
        {
            PhysicalBoundingBox a = projectingArray[i];

            for (int j = i + 1; j < projectingArray.Count; j++)
            {
                //当此循环无法进入，则代表外循环已经到最后一个元素，同时也表示结束
                //因此循环后不宜有逻辑
                PhysicalBoundingBox b = projectingArray[j];
                if (a.myPhyBox == null || b.myPhyBox == null)
                    continue;
                //此处正式开始实体物理算法
                //先算是否重叠
                //计算位置向量差（v1在后续作方位判断，计算自己是否施力，和是否受力）
                Vector2 v1 = a.point - b.point;
                //计算绝对距离
                Vector2 v2 = new Vector2(Mathf.Abs(v1.x), Mathf.Abs(v1.y));
                //计算框选范围和（size 为半长宽，两半宽之和即中心距阈值）
                Vector2 v3 = a.size + b.size;
                //计算重叠程度
                Vector2 v4 = v3 - v2;
                //均大于0才能表示重合
                if (v4.x > 0 && v4.y > 0)
                {
                    //v4表示重叠程度：比较xy大小，小的作为挤出方向
                    float dl = Mathf.Min(v4.x, v4.y);
                    bool separateOnX = v4.x <= v4.y;
                    //计算二者受环境影响程度
                    float envA = a.myPhyBox.EnvImpact;
                    float envB = b.myPhyBox.EnvImpact;
                    float envSum = envA + envB;
                    if (envSum < 0.0001f)
                        envSum = 0.0001f;
                    //a/(a+b),计算权重（envImpact 大 → 被挤出更多，近似质量倒数）
                    float weightA = envA / envSum;
                    float weightB = envB / envSum;
                    //赋值位移偏置（X/Y轴) —— 本帧位移阶段消费
                    Vector2 offsetA = Vector2.zero;
                    Vector2 offsetB = Vector2.zero;
                    if (separateOnX)
                    {
                        float sign = v1.x >= 0f ? 1f : -1f; // a 在 b 的右侧为正，a 沿 +X 挤出
                        offsetA.x = sign * dl * weightA;
                        offsetB.x = -sign * dl * weightB;
                    }
                    else
                    {
                        float sign = v1.y >= 0f ? 1f : -1f;
                        offsetA.y = sign * dl * weightA;
                        offsetB.y = -sign * dl * weightB;
                    }
                    a.myPhyBox.AccumulateDisplacementOffset(offsetA);
                    b.myPhyBox.AccumulateDisplacementOffset(offsetB);

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
                        depth = dl,
                        weightA = weightA,
                        weightB = weightB,
                        offsetA = offsetA,
                        offsetB = offsetB
                    });
#endif

                    //计算可能的施力
                    //只有可移动物体可施力（因为依据大小速度判断施力过于复杂，用可移动物体结合施力物体被动速度计算实际施力）
                    //施力物体的被动速度作为施力的叠加速度，随后结合受环境影响程度计算实际速度影响（使用专属容器，每次清空）
                    //（只有时间和动态力(type为fadeAway)可致速度叠加，状态速度作为接触所致的持续性影响，在瞬间作为计算参数不太合适，因为清空施力者状态速度后还会再复原）
                    //探讨是否确实需要存储引用？（似乎不太必要，每帧重算即可）
                    // → 不存长期施力引用；速度写入 EntitySolutionResult.secondOrderSpeed，下一帧 PositionPrediction 入账
                    TryApplyContactSpeed(a, b, v1, separateOnX, envSum);
                }
            }
        }

        // 本帧快照用完即清，避免跨帧污染（下一帧由各实体重新上报）
        projectingArray.Clear();
    }

    /// <summary>
    /// 可移动体沿挤出轴朝对方闭合时，把自身平面速度按对方权重写入对方的下帧二阶速度
    /// 施力源用 GetPlanarVelocity（主动+被动）：玩家推箱主要靠主动速度；不含单独拆状态速度（传送带推箱为 MVP 可接受偏差）
    /// </summary>
    void TryApplyContactSpeed(PhysicalBoundingBox a, PhysicalBoundingBox b, Vector2 v1, bool separateOnX, float envSum)
    {
        Vector2 velA = a.myPhyBox.GetPlanarVelocity();
        Vector2 velB = b.myPhyBox.GetPlanarVelocity();
        bool aCanMove = a.myPhyBox is ICanMove;
        bool bCanMove = b.myPhyBox is ICanMove;
        if (!aCanMove && !bCanMove)
            return;

        if (separateOnX)
        {
            float sign = v1.x >= 0f ? 1f : -1f; // a 在右侧为正
            // a 在左侧(sign<0)且 vx>0，或 a 在右侧且 vx<0 → a 朝 b 闭合
            if (aCanMove && ((sign < 0f && velA.x > 0f) || (sign > 0f && velA.x < 0f)))
            {
                float recv = velA.x * (b.myPhyBox.EnvImpact / envSum);
                b.myPhyBox.AccumulateSecondOrderSpeed(new Vector2(recv, 0f));
            }
            if (bCanMove && ((sign > 0f && velB.x > 0f) || (sign < 0f && velB.x < 0f)))
            {
                float recv = velB.x * (a.myPhyBox.EnvImpact / envSum);
                a.myPhyBox.AccumulateSecondOrderSpeed(new Vector2(recv, 0f));
            }
        }
        else
        {
            float sign = v1.y >= 0f ? 1f : -1f;
            if (aCanMove && ((sign < 0f && velA.y > 0f) || (sign > 0f && velA.y < 0f)))
            {
                float recv = velA.y * (b.myPhyBox.EnvImpact / envSum);
                b.myPhyBox.AccumulateSecondOrderSpeed(new Vector2(0f, recv));
            }
            if (bCanMove && ((sign > 0f && velB.y > 0f) || (sign < 0f && velB.y < 0f)))
            {
                float recv = velB.y * (a.myPhyBox.EnvImpact / envSum);
                a.myPhyBox.AccumulateSecondOrderSpeed(new Vector2(0f, recv));
            }
        }
    }
}
