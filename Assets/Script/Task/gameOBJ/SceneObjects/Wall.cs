using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Wall : BaseGround
{
    /// <summary>
    /// 是否可以攀爬
    /// </summary>
    [SerializeField]
    bool canClimb;
    /// <summary>
    /// 墙跳距离加成（-1
    /// </summary>
    [SerializeField]
    float wallJumpDistance;
    /// <summary>
    /// 墙跳高度加持
    /// </summary>
    [SerializeField]
    float wallJumpHeight;
    /// <summary>
    /// 贴墙下滑影响程度（必须是正数，1表示无特殊影响
    /// </summary>
    [SerializeField]
    float wallFriction=1f;
    /// <summary>
    /// 摩擦力
    /// </summary>
    public float WallFRICTION => wallFriction;
}
