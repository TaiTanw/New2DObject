using PhyData;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>
/// 生物物理约束影响（供外部调用和引用
/// </summary>
public interface IPhysicalconstraint 
{
    /// <summary>
    /// 持续性移动受限开始
    /// </summary>
    /// <param name="iD">唯一标识</param>
    /// <param name="num">影响程度</param>
    public void OnPhyEnter(BasicPhysicalObject iD, float num);
    /// <summary>
    /// 持续性移动受限取消
    /// </summary>
    /// <param name="iD"></param>
    public void OnPhyExit(BasicPhysicalObject iD);

    /// <summary>
    /// 外部提供速度(固定时间影响
    /// </summary>
    /// <param name="time">持续时间</param>
    /// <param name="force">受力大小</param>
    public void AddSpeed(float time,Vector2 force);
    /// <summary>
    /// 外部提供速度（状态持续影响
    /// </summary>
    /// <param name="force"></param>
    public void AddForce(BasicPhysicalObject iD,Vector2 force);
    /// <summary>
    /// 外部取消状态性质速度
    /// </summary>
    /// <param name="force"></param>
    public void RemoveForce(BasicPhysicalObject iD);

}
