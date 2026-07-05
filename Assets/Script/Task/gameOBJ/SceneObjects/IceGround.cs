using PhyData;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEditor.Progress;

/// <summary>
/// 冰面
/// </summary>
public class IceGround : BaseGround, IDynamicAddForce
{
    /// <summary>
    /// 加速影响因子
    /// </summary>
    public float Idex=1;

    /// <summary>
    /// 最大滑动速度系数
    /// </summary>
    public float maxISpeed=1;


    public override void OnPhyEnter(IForceAction obj)
    {
        //不重复则进入
        if (!objIPhyHas.Contains(obj))
        {
            objIPhyHas.Add(obj);
            obj.StatePowerRegistration(this, speedChangeNum);
            obj.AddSpeedStatus(this, phySpeed);
            //开始动态施力
            ForceData force = new ForceData();
            //受力物体是否有可移动能力
            if (obj is ICanMove Ic)
            {
                force.Init(Idex, slowingEffect, Ic.Mobility * maxISpeed * speedChangeNum);
            }
            else
            {
                //实体无移动能力，则直接0
                force.Init(Idex, slowingEffect, 0);
            }

            //设置受力,复原速度和最大速度

            obj.AddForce(this, force);
        }
    }
    public override void OnPhyExit(IForceAction obj)
    {
        if (objIPhyHas.TryGetValue(obj, out var theo))
        {
            theo.StatePowerCancellation(this);
            //状态力的延迟删除
            theo.RemoveSpeedStatus(this);
            theo.RemoveForce(this);
            objIPhyHas.Remove(obj);
        }
    }
    //private void FixedUpdate()
    //{
    //    foreach (var item in objIPhyHas)
    //    {

    //        //得到角色当前主动移动方向
    //        float v =item.GetPhyData().horizontalSpeed;
    //        if (v > 0)
    //        {
    //            item.ChangeForce(this, Idex);
    //            item.ChangeType(this, E_PhyForceType.apply);
    //        }
    //        else if (v < 0)
    //        {
    //            item.ChangeForce(this, -Idex);
    //            item.ChangeType(this, E_PhyForceType.apply);
    //        }          
    //        else
    //        {
    //            item.ChangeType(this,E_PhyForceType.controlRecovery);
    //        }

    //    }
    //}

    protected override void OnDisable()
    {
        foreach (var obj in objIPhyHas)
        {
            //取消影响
            obj.StatePowerCancellation(this);
            obj.RemoveSpeedStatus(this);
            obj.RemoveForce(this);
        }
        objIPhyHas.Clear();
    }

    public void ForceCalculation(IForceAction IF)
    {
        
        if (IF is ICanMove ICa)
        {
            float v = ICa.MovingDirection;
            if (v != 0)
            {
                IF.ChangeForce(this, v * Idex);
                IF.ChangeType(this, E_PhyForceType.apply);
            }
            else
            {
                IF.ChangeType(this, E_PhyForceType.controlRecovery);
            }

        }
    }
}
