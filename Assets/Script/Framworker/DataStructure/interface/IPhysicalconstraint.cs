using PhyData;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>
/// 施力动作（施力物体必须
/// </summary>
public interface IApplyingForceAction
{
    /// <summary>
    /// 施力动作开始
    /// </summary>
    /// <param name="iD">受力对象</param>
    public void OnPhyEnter(IForceAction iD);
    /// <summary>
    /// 施力动作结束
    /// </summary>
    /// <param name="iD">受力对象</param>
    public void OnPhyExit(IForceAction iD);


}
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
/// 受力动作（受力物体必须
/// </summary>
public interface IForceAction
{
    /// <summary>
    /// 状态力注册
    /// </summary>
    /// <param name="id">施力物体</param>
    /// <param name="num">大小</param>
    public void StatePowerRegistration(IApplyingForceAction id,float num);
    /// <summary>
    /// 状态力注销
    /// </summary>
    /// <param name="id">施力对象</param>
    public void StatePowerCancellation(IApplyingForceAction id);
    /// <summary>
    /// 速度状态添加
    /// </summary>
    /// <param name="iD">施力对象</param>
    /// <param name="force">速度大小</param>
    public void AddSpeedStatus(IApplyingForceAction iD, Vector2 force);
    /// <summary>
    /// 速度状态移除
    /// </summary>
    /// <param name="iD">施力对象</param>
    public void RemoveSpeedStatus(IApplyingForceAction iD);
    /// <summary>
    /// 添加限时速度（水平
    /// </summary>
    /// <param name="time">持续时间</param>
    /// <param name="addSpeed">速度大小</param>
    public void AddTimeSpeed(float time, float addSpeed);

    /// <summary>
    /// 添加受力效果
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="force"></param>
    public void AddForce(IDynamicAddForce iD, ForceData force);

    /// <summary>
    /// 改变受力大小
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newForce"></param>
    public void ChangeForce(IDynamicAddForce iD, float newForce);

    /// <summary>
    /// 受力类型变化
    /// </summary>
    /// <param name="iD"></param>
    /// <param name="newSpeed"></param>
    public void ChangeType(IDynamicAddForce iD, E_PhyForceType type);
    /// <summary>
    /// 移除受力
    /// </summary>
    /// <param name="iD"></param>
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