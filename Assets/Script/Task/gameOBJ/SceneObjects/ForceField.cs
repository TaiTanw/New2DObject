using PhyData;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ForceField : BasicPhysicalObject,IDynamicAddForce
{
    /// <summary>
    /// 最大速度
    /// </summary>
    [SerializeField]
    protected float maxSpeed;

    /// <summary>
    /// 加速程度
    /// </summary>
    [SerializeField]
    protected float add;

    public void ForceCalculation(IForceAction IF)
    {
        
    }

    public override void OnPhyEnter(IForceAction obj)
    {
        //不重复则进入
        if (!objIPhyHas.Contains(obj))
        {
            objIPhyHas.Add(obj);
            obj.StatePowerRegistration(this, speedChangeNum);
            obj.AddSpeedStatus(this, phySpeed);
            ForceData force = new ForceData();
            force.Init(add, 0, maxSpeed);
            obj.AddForce(this, force);
        }
    }

    public override void OnPhyExit(IForceAction obj)
    {
        if (objIPhyHas.TryGetValue(obj, out var theo))
        {
            theo.RemoveForce(this);
            theo.StatePowerCancellation(this);
            theo.RemoveSpeedStatus(this);
            objIPhyHas.Remove(obj);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.gameObject.TryGetComponent<IForceAction>(out IForceAction entity))
        {
            OnPhyEnter(entity);
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.gameObject.TryGetComponent<IForceAction>(out IForceAction entity))
        {
            OnPhyExit(entity);
        }
    }
}
