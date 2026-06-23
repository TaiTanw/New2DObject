using PhyData;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 冰面
/// </summary>
public class IceGround : BaseGround
{
    /// <summary>
    /// 加速影响因子
    /// </summary>
    public float Idex=1;
    /// <summary>
    /// 减速影响因子
    /// </summary>
    public float Qdex=1;
    /// <summary>
    /// 最大滑动速度系数
    /// </summary>
    public float maxISpeed=1;


    public override void SetObjToPhyList(BasePhysicsEntity obj)
    {
        //不重复则进入
        if (!objIPhyHas.Contains(obj))
        {
            objIPhyHas.Add(obj);
            obj.OnPhyEnter(this, speedChangeNum);
            obj.AddSpeedStatus(this, phySpeed);
            ForceData force = new ForceData();
            //设置受力,复原速度和最大速度
            force.Init(Idex,Qdex, obj.speed * maxISpeed * speedChangeNum);
            obj.AddForce(this, force);
        }
    }
    public override void OutObjPhy(BasePhysicsEntity obj)
    {
        //延迟删除
        if (objIPhyHas.TryGetValue(obj, out var theo))
        {
            theo.OnPhyExit(this);
            //状态力的延迟删除
            theo.RemoveSpeedStatus(this);
            theo.RemoveForce(this);
            objIPhyHas.Remove(obj);
        }
    }
    private void FixedUpdate()
    {
        foreach (var item in objIPhyHas)
        {
            //得到角色当前主动移动方向
            float v =item.ReadOnly_PlayerPhyData.horizontalSpeed;
            if (v > 0)
            {
                item.ChangeForce(this, Idex);
                item.ChangeType(this, E_PhyForceType.apply);
            }
            else if (v < 0)
            {
                item.ChangeForce(this, -Idex);
                item.ChangeType(this, E_PhyForceType.apply);
            }          
            else
            {
                item.ChangeType(this,E_PhyForceType.controlRecovery);
            }
                

            ////得到角色当前主动速度
            //float v = item.ReadOnly_PlayerPhyData.horizontalSpeed * maxISpeed;
            ////得到此角色受此物体的受力情况
            //float x = item.StartForceDic[this].x;
            ////玩家移动时加速过程
            //if (v > 0)
            //{
            //    if (x < v)
            //    {
            //        x += Idex * Time.fixedDeltaTime;
            //    }
            //    else if (x > v)
            //    {
            //        x = v; 
            //    }
            //}
            //else if(v < 0)
            //{
            //    if (x > v)
            //    {
            //        x -= Idex * Time.fixedDeltaTime;
            //    }
            //    else if (x < v)
            //    {
            //        x = v;
            //    }
            //}
            //else//角色主动位移归零时，叠加速度复原过程
            //{
            //    if(x <-0.5f)
            //    {
            //        x += Qdex * Time.fixedDeltaTime;
            //    }
            //    else if(x > 0.5f)
            //    {
            //        x -= Qdex * Time.fixedDeltaTime;
            //    }
            //    else
            //    {
            //        x = 0f;
            //    }
            //}
            ////施力
            //item.AddSpeedStatus(this, new Vector2(x, 0));
        }
    }

    protected override void OnDisable()
    {
        foreach (var obj in objIPhyHas)
        {
            //取消影响
            obj.OnPhyExit(this);
            obj.RemoveSpeedStatus(this);
            obj.RemoveForce(this);
        }
        objIPhyHas.Clear();
    }
}
