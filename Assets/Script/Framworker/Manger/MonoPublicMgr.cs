using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
/// <summary>
/// 公共Mono模块,用于其他非继承mono的脚本使用mono方法，且便于管理帧更新逻辑，提升性能
/// </summary>
public class MonoPublicMgr : BaseAutoMonoMgr<MonoPublicMgr>
{
    //生命周期和帧更新逻辑不需要传入参数，即使有参数传入也可直接包裹
    //因此只需要声明无参事件即可，事件用于安全触发
    //私有事件，用方法来添加
    private event UnityAction UpdateEvent;
    private event UnityAction FixedUpdateEvent;
    private event UnityAction LateUpdateEvent;

    public static bool IsQuitting;

    private void OnApplicationQuit()
    {
        IsQuitting = true;
    }
    public void Init()
    {

    }

    public UnityAction[] actionsLen=new UnityAction[6];
    /// <summary>
    /// 添加物理时序更新
    /// </summary>
    /// <param name="i">时序层，数越大，更新越靠后</param>
    public void AddPhysicalTimingUpdate(UnityAction action,int i)
    {
        actionsLen[i]+=action;
    }
    public void RemovePhysicalTimingUpdate(UnityAction action, int i)
    {
        if(IsQuitting) return;
        actionsLen[i] -= action;
    }
    public void AddUpdatEvent(UnityAction action)
    {
        UpdateEvent += action;
    }
    public void RemoveUpdatEvent(UnityAction action)
    {
        UpdateEvent -= action;
    }
    public void AddFixedUpdateEvent(UnityAction action)
    {
        FixedUpdateEvent += action;
    }
    public void RemoveFixedUpdateEvent( UnityAction action)
    {
        FixedUpdateEvent -= action;
    }
    public void AddLateUpdateEvent(UnityAction action)
    {
        LateUpdateEvent += action;
    }
    public void RemoveLateUpdateEvent( UnityAction action)
    {
        LateUpdateEvent -= action;
    }
    

    // Update is called once per frame
    void Update()
    {
        UpdateEvent?.Invoke();
    }

    private void FixedUpdate()
    {
        FixedUpdateEvent?.Invoke();
        for (int i = 0; i < actionsLen.Length; i++)
        {
            actionsLen[i]?.Invoke();
        }
    }

    private void LateUpdate()
    {
        LateUpdateEvent?.Invoke();
    }
}
