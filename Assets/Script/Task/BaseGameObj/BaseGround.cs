using System.Collections;
using System.Collections.Generic;
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
    /// 本帧位移，动态物体可用，默认0,运行时数据
    /// </summary>
    protected Vector2 delta; // 本帧位移
    public Vector2 Delta=>delta;
    /// <summary>
    /// 受控物理角色容器（表示所有受环境影响的物体站在此平台上）
    /// </summary>
    protected Dictionary<int,IPhysicalconstraint> objPhyDic=new Dictionary<int,IPhysicalconstraint>();

    public void SetObjToPhyList(int id,IPhysicalconstraint obj)
    {
        print("aaaaa");
        //if (!objPhyDic.ContainsKey(id))
        {
            objPhyDic[id] = obj;
            obj.OnPhyEnter(gameObject.GetInstanceID(), speedChangeNum);
        }
       
    }
    public void OutObjPhy(int id)
    {
        print("bbbbbbbb");
        if (objPhyDic.ContainsKey(id))
        {
            objPhyDic[id].OnPhyExit(gameObject.GetInstanceID());
            objPhyDic.Remove(id);
        }

    }

}
