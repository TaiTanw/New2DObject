using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 物理环境检测点位
/// </summary>
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

    /// <summary>
    /// 地面检测中心（外部拖拽关联
    /// </summary>
    public Vector2 groundV;
    /// <summary>
    /// 左墙检测
    /// </summary>
    public Vector2 leftV;
    /// <summary>
    /// 右墙检测
    /// </summary>
    public Vector2 rightV;

}
