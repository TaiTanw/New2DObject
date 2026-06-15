using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>
/// 场景物体基类（非生物
/// </summary>
public class BaseGround : MonoBehaviour

    //只要是外部需要用到的数据，均在基类声明
    //当前行为：基类只负责所有可能用到的数据，具体行为由子类实现
{
    /// <summary>
    /// 速度影响百分比
    /// </summary>
    [SerializeField]
    protected float speedChangeNum ;
    public float SpeendChangeNum=>speedChangeNum;
    /// <summary>
    /// 跳跃高度速度影响百分比
    /// </summary>
    [SerializeField]
    protected float jumpHeightNum ;
    public float JumpHeightNum=>jumpHeightNum;
    /// <summary>
    /// 自身状态性质施力
    /// </summary>
    [SerializeField]
    protected Vector2 phySpeed;
    /// <summary>
    /// 本帧位移，动态物体可用，默认0,运行时数据
    /// </summary>
    protected Vector2 delta;
    public Vector2 Delta=>delta;
    /// <summary>
    /// 受控物理角色容器（表示所有受环境影响的物体站在此平台上）
    /// </summary>
    protected HashSet<IPhysicalconstraint> objIPhyHas=new HashSet<IPhysicalconstraint>();

    public void SetObjToPhyList(IPhysicalconstraint obj)
    {
        //print("aaaaa");
        //不重复则进入
        if (!objIPhyHas.Contains(obj))
        {
            objIPhyHas.Add(obj);
            obj.OnPhyEnter(this, speedChangeNum);
            obj.AddForce(this, phySpeed);
        }

    }


    public void OutObjPhy(IPhysicalconstraint obj)
    {
        //print("bbbbbbbb");
        if (objIPhyHas.TryGetValue(obj,out var theo))
        {
            theo.OnPhyExit(this);
            theo.RemoveForce(this);
            objIPhyHas.Remove(obj);
        }

    }

    private void OnDisable()
    {
        foreach(var obj in objIPhyHas)
        {
            //取消影响
            obj.OnPhyExit(this);
            obj.RemoveForce(this);
        }
        objIPhyHas.Clear();
    }
}
