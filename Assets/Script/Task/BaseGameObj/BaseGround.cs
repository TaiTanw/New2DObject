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
    protected Dictionary<int,IPhysicalconstraint> objPhyDic=new Dictionary<int,IPhysicalconstraint>();

    StringBuilder idid = new ("ground");

    public void SetObjToPhyList(int id, IPhysicalconstraint obj)
    {
        print("aaaaa");
        //if (!objPhyDic.ContainsKey(id))
        {
            objPhyDic[id] = obj;
            obj.OnPhyEnter(idid.Append(gameObject.GetInstanceID()), speedChangeNum);
            obj.AddForce(idid.Append(gameObject.GetInstanceID()), phySpeed);
        }

    }

    //public void SetObjToPhyList(int id, IPhysicalconstraint obj)
    //{
    //    if (objPhyDic.ContainsKey(id))
    //    {
    //        // 已存在，先移除旧的
    //        objPhyDic[id].OnPhyExit(GetPhyID(id));
    //        objPhyDic[id].RemoveForce(GetPhyID(id));
    //    }

    //    objPhyDic[id] = obj;
    //    StringBuilder phyID = GetPhyID(id);
    //    obj.OnPhyEnter(phyID, speedChangeNum);
    //    obj.AddForce(phyID, phySpeed);
    //}
    //Dictionary<int,StringBuilder> phyIDCache=new Dictionary<int,StringBuilder>();
    //private StringBuilder GetPhyID(int objId)
    //{
    //    // 缓存 StringBuilder，避免重复创建和污染
    //    if (!phyIDCache.TryGetValue(objId, out var sb))
    //    {
    //        sb = new StringBuilder("ground").Append(gameObject.GetInstanceID())
    //                                       .Append("_").Append(objId);
    //        phyIDCache[objId] = sb;
    //    }
    //    return sb;
    //}
    public void OutObjPhy(int id)
    {
        print("bbbbbbbb");
        if (objPhyDic.ContainsKey(id))
        {
            objPhyDic[id].OnPhyExit(idid.Append(gameObject.GetInstanceID()));
            objPhyDic[id].RemoveForce(idid.Append(gameObject.GetInstanceID()));
            objPhyDic.Remove(id);
        }

    }

    private void OnDisable()
    {
        foreach(var obj in objPhyDic.Values)
        {
            //取消影响
            obj.OnPhyExit(idid.Append(gameObject.GetInstanceID()));
            obj.RemoveForce(idid.Append(gameObject.GetInstanceID()));
        }
        objPhyDic.Clear();
    }
}
