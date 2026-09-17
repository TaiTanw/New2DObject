using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.U2D;

/// <summary>
/// 移动平台在相位 0 更新位置并记录 delta；实体踩在上面时，经 ENV-S1 读到该帧位移。
/// 职能：平台自身运动与位移能力提供者；启停钩子只管理自身回调，环境解除由旧基类统一处理。
/// </summary>
public class Taijie : BaseGround, IPlatformMotion, IMovementSpeedModifier
{
    [SerializeField] protected float speedChangeNum; // 主动速度加性修饰，旧序列化名称保留。
    public float MovementSpeedOffset => speedChangeNum;

    /// <summary>
    /// 平台效应器
    /// </summary>
    [SerializeField]
    private SpriteRenderer spriteRenderer;
    /// <summary>
    /// 目标点
    /// </summary>
    Vector3 toPoint;
    /// <summary>
    /// 生成时高度
    /// </summary>
    float startPoint=5;
    /// <summary>
    /// 到目标点时间
    /// </summary>
    double endTime=3;
    /// <summary>
    /// 当前时间
    /// </summary>
    double nowtime;
    /// <summary>
    /// 速度
    /// </summary>

    /// <summary>
    /// 开始运行
    /// </summary>
    bool start;
    /// <summary>
    /// 起始点缓存
    /// </summary>
    Vector3 startPos;

    protected override void OnSourceEnabled()
    {
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(FixFun, 0);
    }
    /// <summary>
    /// 生成时初始化
    /// </summary>
    /// <param name="v3"></param>
    public void ToMovePoint(Vector3 v3)
    {
        toPoint = v3;
        this.transform.position = new Vector3(v3.x, v3.y+startPoint,v3.z);

        startPos = transform.position;

        nowtime= AudioSettings.dspTime;
        //lastT = 0;
        start = true;
    }
    void FixFun()
    {
        if (!start)
        {
            delta = Vector2.zero;
            return;
        }

        double rawT = (AudioSettings.dspTime - nowtime) / endTime;
        float t = Mathf.Clamp01((float)rawT);

        //t = Mathf.Max(t, lastT);

        Vector3 newPos;

        if (t >= 1.0f)
        {
            delta = (Vector2)(toPoint - transform.position);
            transform.position = toPoint;

            start = false;
            //lastT = 1f;
            return;
        }
        else
        {
            newPos = Vector3.Lerp(startPos, toPoint, t);
        }

        delta = (Vector2)(newPos - transform.position);
        transform.position = newPos;

        //lastT = t;
    }

    protected override void OnSourceDisabled()
    {
        delta = Vector2.zero;
        if (!MonoPublicMgr.IsQuitting)
        {
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(FixFun, 0);

        }
    }
}
