using System.Collections;
using System.Collections.Generic;
using UnityEngine;
/// <summary>
/// 生物物理约束影响
/// </summary>
public interface IPhysicalconstraint 
{
    /// <summary>
    /// 物理进入
    /// </summary>
    /// <param name="phyValueName">影响物唯一ID</param>
    /// <param name="num"><影响程度/param>
    public void OnPhyEnter(int iD, float num);

    public void OnPhyExit(int iD);
}
