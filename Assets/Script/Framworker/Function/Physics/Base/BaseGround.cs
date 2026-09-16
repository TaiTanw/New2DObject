using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>
/// 旧地面家族共用的消退阻力倍率和起跳加成；同时允许实体从脚下支撑登记持续效果。
/// 职能：旧场景参数兼容层，通过 IGroundResponse 给 ENV-S1 提供读值。
/// </summary>
public class BaseGround : BasicPhysicalObject, IGroundResponse
{

    // 旧地面组件的参数兼容层。实体读取能力接口，不再以 BaseGround 类型判定环境效果。
    protected override bool AppliesOnGround => true;

    /// <summary>
    /// 减速影响因子（处于此地面时，对消退类型力的衰减幅度）
    /// </summary>
    [SerializeField]
    protected float slowingEffect = 1;
    public float SlowingEffect=>slowingEffect;
    /// <summary>
    /// 跳跃高度速度影响百分比
    /// </summary>
    [SerializeField]
    protected float jumpHeightNum ;
    public float JumpHeightNum=>jumpHeightNum;

    /// <summary>
    /// 本帧位移的旧存储位置；只有实现 IPlatformMotion 的平台才会被运动管线读取。
    /// </summary>
    protected Vector2 delta;
    public Vector2 Delta=>delta;

}
