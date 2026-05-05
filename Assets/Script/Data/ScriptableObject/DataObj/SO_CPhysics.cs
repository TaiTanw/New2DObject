using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "StartResName", menuName = "自定义数据结构/角色配置数据/角色物理配置")]
public class SO_CPhysics : ScriptableObject
{
    /// <summary>
    /// 地面检测中心
    /// </summary>
    public Transform groundV;

    /// <summary>
    /// 检测地面的层级
    /// </summary>
    public LayerMask groundLayer;
    /// <summary>
    /// 底盒地面检测高度
    /// </summary>
    public float boxCastH;

    public float speed = 15;    // 基础水平移速
    public float upSpeed = 15;//基础跳跃速度
    public float gravity = -35f;    //重力速度
}
