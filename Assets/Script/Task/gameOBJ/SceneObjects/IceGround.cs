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
    /// 最大滑动速度
    /// </summary>
    public float maxISpeed=1;

    /// <summary>
    /// 记录每一个所处冰面上物体的计算速度，供删除影响
    /// </summary>
    Dictionary<BasePhysicsEntity, float> measurementCache=new Dictionary<BasePhysicsEntity, float>();

    public override void SetObjToPhyList(BasePhysicsEntity obj)
    {
        //不重复则进入
        if (!measurementCache.ContainsKey(obj))
        {
            measurementCache.Add(obj, 0);
            objIPhyHas.Add(obj);
            obj.OnPhyEnter(this, speedChangeNum);
            //obj.AddForce(this, phySpeed);
        }
    }
    public override void OutObjPhy(BasePhysicsEntity obj)
    {
        if (measurementCache.ContainsKey(obj))
        {
            obj.OnPhyExit(this);
            obj.RemoveForce(this);
            measurementCache.Remove(obj);
            objIPhyHas.Remove(obj);
        }
    }
    private void FixedUpdate()
    {
        foreach (var item in objIPhyHas)
        {
            float v = item.ReadOnly_PlayerPhyData.horizontalSpeed * maxISpeed;
            //玩家移动时加速过程
            if (v > 0)
            {
                if (measurementCache[item] < v)
                {
                    measurementCache[item] += Idex * Time.fixedDeltaTime;
                }
                else if (measurementCache[item] > v)
                {
                    measurementCache[item] = v; 
                }
            }
            else if(v < 0)
            {
                if (measurementCache[item] > v)
                {
                    measurementCache[item] -= Idex * Time.fixedDeltaTime;
                }
                else if (measurementCache[item] < v)
                {
                    measurementCache[item] = v;
                }
            }
            else//玩家主动位移归零时，叠加速度复原过程
            {
                if(measurementCache[item] <-0.5f)
                {
                    measurementCache[item] += Qdex * Time.fixedDeltaTime;
                }
                else if(measurementCache[item] > 0.5f)
                {
                    measurementCache[item] -= Qdex * Time.fixedDeltaTime;
                }
                else
                {
                    measurementCache[item] = 0f;
                }
            }

            item.AddForce(this, new Vector2(measurementCache[item], 0));
        }
    }

    protected override void OnDisable()
    {
        foreach (var obj in measurementCache)
        {
            //取消影响
            obj.Key.OnPhyExit(this);
            obj.Key.RemoveForce(this);
        }
        measurementCache.Clear();
        objIPhyHas.Clear();
    }
}
