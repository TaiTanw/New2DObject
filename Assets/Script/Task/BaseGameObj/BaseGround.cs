using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
/// <summary>
/// 碰撞物体基类（非生物
/// </summary>
public class BaseGround : BasicPhysicalObject

    //只要是外部需要用到的数据，均在基类声明
    //当前行为：基类只负责所有可能用到的数据，具体行为由子类实现
{

    /// <summary>
    /// 跳跃高度速度影响百分比
    /// </summary>
    [SerializeField]
    protected float jumpHeightNum ;
    public float JumpHeightNum=>jumpHeightNum;

    /// <summary>
    /// 本帧位移，动态平台可用，默认0,运行时数据
    /// </summary>
    protected Vector2 delta;
    public Vector2 Delta=>delta;


    /// <summary>
    /// 进入时施加影响(外部由基础受力角色控制器主动调用
    /// </summary>
    /// <param name="obj"></param>
    public virtual void SetObjToPhyList(BasePhysicsEntity obj)
    {
        ApplyForceOnContact(obj);

    }

    /// <summary>
    /// 离开时消除影响(外部由基础受力角色控制器主动调用
    /// </summary>
    /// <param name="obj"></param>

    public virtual void OutObjPhy(BasePhysicsEntity obj)
    {
        LeaveTOoutForce(obj);

    }


}
