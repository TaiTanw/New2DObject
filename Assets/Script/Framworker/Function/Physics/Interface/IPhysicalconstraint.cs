using PhyData;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>
/// 动态施力（施力物体可有
/// </summary>
public interface IDynamicAddForce 
{
    /// <summary>
    /// 动态施力规则
    /// </summary>
    public void ForceCalculation(IForceAction IF);

}

/// <summary>
/// 受力实体的效果写入口；来源关系由环境上下文管理，累计速度由实体数值状态持有。
/// </summary>
public interface IForceAction
{
    /// <summary>
    /// 粘滞力注册
    /// </summary>
    /// <param name="id">环境来源身份</param>
    /// <param name="num">大小</param>
    public void StatePowerRegistration(EnvironmentRegistration id,float num);
    /// <summary>
    /// 粘滞力注销
    /// </summary>
    /// <param name="id">环境来源身份</param>
    public void StatePowerCancellation(EnvironmentRegistration id);
    /// <summary>
    /// 状态速度添加
    /// </summary>
    /// <param name="iD">环境来源身份</param>
    /// <param name="force">速度大小</param>
    public void AddSpeedStatus(EnvironmentRegistration iD, Vector2 force);
    /// <summary>
    /// 状态速度移除
    /// </summary>
    /// <param name="iD">环境来源身份</param>
    public void RemoveSpeedStatus(EnvironmentRegistration iD);
    /// <summary>
    /// 添加限时速度（水平
    /// </summary>
    /// <param name="time">持续时间</param>
    /// <param name="addSpeed">速度大小</param>
    public void AddTimeSpeed(float time, float addSpeed);

    /// <summary>
    /// 登记来源提供的初始动态力数据；同来源再次登记时沿用原参数和累计速度。
    /// </summary>
    /// <param name="iD">动态力来源</param>
    /// <param name="force">初始数据</param>
    public void AddForce(IDynamicAddForce iD, ForceData force);

    /// <summary>
    /// 新增或刷新来源的施力参数，保留该实体已累计的速度并恢复施加状态。
    /// 接触解算器在相位 4 写入，新参数从下一帧相位 3 开始积分。
    /// </summary>
    /// <param name="source">动态力来源</param>
    /// <param name="parameters">施力参数，不携带运行状态</param>
    public void SetOrUpdateDynamicForce(IDynamicAddForce source, DynamicForceParameters parameters);

    /// <summary>
    /// 只改变已登记来源的施力大小，保留其余参数、类型和累计速度。
    /// </summary>
    /// <param name="iD">动态力来源</param>
    /// <param name="newForce">新的施力大小</param>
    public void ChangeForce(IDynamicAddForce iD, float newForce);

    /// <summary>
    /// 切换已登记来源的受力类型，不清除累计速度。
    /// </summary>
    /// <param name="iD">动态力来源</param>
    /// <param name="type">新的受力类型</param>
    public void ChangeType(IDynamicAddForce iD, E_PhyForceType type);
    /// <summary>
    /// 来源退出控制，已累计速度进入渐隐阶段。
    /// </summary>
    /// <param name="iD">动态力来源</param>
    public void RemoveForce(IDynamicAddForce iD);


}

/// <summary>
/// 可移动能力（受力物体可有
/// </summary>
public interface ICanMove
{
    /// <summary>
    /// 移动方向
    /// </summary>
    public float MovingDirection { get; }
    /// <summary>
    /// 移动能力
    /// </summary>
    public float Mobility { get; }
}
