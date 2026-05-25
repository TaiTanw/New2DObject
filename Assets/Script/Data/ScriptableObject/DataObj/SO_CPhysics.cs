using System.Collections;
using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "StartResName", menuName = "自定义数据结构/角色配置数据/角色物理配置")]
public class SO_CPhysics : ScriptableObject
{
    /// <summary>
    /// 检测地面的层级
    /// </summary>
    public LayerMask groundLayer;
    /// <summary>
    /// 墙体检测层级
    /// </summary>
    public LayerMask wallLayer;
    /// <summary>
    /// 底盒地面检测高度
    /// </summary>
    public Vector2 boxCastH =new Vector2(1,0.1f);
    /// <summary>
    /// 左右检测厚度
    /// </summary>
    public Vector2 boxCastV = new Vector2(0.1f, 1);

    public float speed = 15;    // 基础水平移速
    public float upSpeed = 15;//基础跳跃速度
    public float gravity = -35f;    //重力速度
    /// <summary>
    /// 贴墙下滑最大速度
    /// </summary>
    public float wallDownSpeed = 2f;
    /// <summary>
    /// 物理帧更新时序层级
    /// </summary>
    public int phyMask = 1;

}
