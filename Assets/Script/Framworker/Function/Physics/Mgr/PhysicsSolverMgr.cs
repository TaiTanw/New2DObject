using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PhyData;

/// <summary>
/// 物理解算器
/// </summary>
public class PhysicsSolverMgr : BaseMgr<PhysicsSolverMgr>
{
    /// <summary>
    /// 投射数组
    /// </summary>
    List<PhysicalBoundingBox> projectingArray=new List<PhysicalBoundingBox>();


    public void Init()
    {
        //第5相位
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(ContactForSolution,4);
    }
    /// <summary>
    /// 添加投射
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
        for(int i = 0; i < projectingArray.Count; i++)
        {
            PhysicalBoundingBox a = projectingArray[i];

            for (int j = i+1; j < projectingArray.Count; j++)
            {
                //当此循环无法进入，则代表外循环已经到最后一个元素，同时也表示结束
                //因此循环后不宜有逻辑
                PhysicalBoundingBox b = projectingArray[j];
                //此处正式开始实体物理算法
                //先算是否重叠
                //计算位置向量差（v1在后续作方位判断，计算自己是否施力，和是否受力）
                Vector2 v1 = a.point - b.point;
                //计算绝对距离
                Vector2 v2 = new Vector2(Mathf.Abs(v1.x), Mathf.Abs(v1.y));
                //计算框选范围和
                Vector2 v3 = a.size + b.size;
                //计算重叠程度
                Vector2 v4 = v3 - v2;
                //均大于0才能表示重合
                if(v4.x>0&&v4.y>0)
                {
                    //v4表示重叠程度：比较xy大小，小的作为挤出方向
                    float dl=Mathf.Min(v4.x, v4.y);
                    //计算二者受环境影响程度
                    //a/(a+b),计算权重
                    //赋值位移偏置（X/Y轴)
                    //计算可能的施力
                    //只有可移动物体可施力（因为依据大小速度判断施力过于复杂，用可移动物体结合施力物体被动速度计算实际施力）
                    //施力物体的被动速度作为施力的叠加速度，随后结合受环境影响程度计算实际速度影响（使用专属容器，每次清空）
                    //（只有时间和动态力(type为fadeAway)可致速度叠加，状态速度作为接触所致的持续性影响，在瞬间作为计算参数不太合适，因为清空施力者状态速度后还会再复原）
                    //探讨是否确实需要存储引用？（似乎不太必要，每帧重算即可）

                }
            }

        }
    }
}
