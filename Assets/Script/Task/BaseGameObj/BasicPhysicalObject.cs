using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 基础物理作用体
/// </summary>
public class BasicPhysicalObject : MonoBehaviour
{
    //当此物体的持续力对所有施力物体一致时，可用父类的增减方法

    /// <summary>
    /// 速度影响百分比（粘滞力
    /// </summary>
    [SerializeField]
    protected float speedChangeNum;

    /// <summary>
    /// 自身状态性质施力（状态力
    /// </summary>
    [SerializeField]
    protected Vector2 phySpeed;

    /// <summary>
    /// 受控物理角色容器（表示所有受环境影响的物体站在此平台上）
    /// </summary>
    protected HashSet<BasePhysicsEntity> objIPhyHas = new HashSet<BasePhysicsEntity>();


    /// <summary>
    /// 接触施力
    /// </summary>
    /// <param name="obj">受力角色接口</param>
    protected void ApplyForceOnContact(BasePhysicsEntity obj)
    {
        //不重复则进入
        if (!objIPhyHas.Contains(obj))
        {
            objIPhyHas.Add(obj);
            obj.OnPhyEnter(this, speedChangeNum);
            obj.AddForce(this, phySpeed);
        }
    }
    /// <summary>
    /// 离开注销
    /// </summary>
    /// <param name="obj"></param>
    protected void LeaveTOoutForce(BasePhysicsEntity obj)
    {
        if (objIPhyHas.TryGetValue(obj, out var theo))
        {
            theo.OnPhyExit(this);
            theo.RemoveForce(this);
            objIPhyHas.Remove(obj);
        }
    }


    //记录受控角色的目的：为了删除时确保消除对已影响角色的影响
    //外部继承必须注意在增减力的情况下注册注销对象容器（后续可对此优化）
    protected virtual void OnDisable()
    {
        foreach (var obj in objIPhyHas)
        {
            //取消影响
            obj.OnPhyExit(this);
            obj.RemoveForce(this);
        }
        objIPhyHas.Clear();
    }
}
