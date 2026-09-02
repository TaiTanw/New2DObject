using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 内部事件系统
/// </summary>
/// <typeparam name="T">所有可触发的事件枚举</typeparam>
public class LocalEventSystem<T> where T : Enum
{
    public abstract class BaseLocalEventData { }
    /// <summary>
    /// 有参事件触发
    /// </summary>
    /// <typeparam name="V">参数类型</typeparam>
    public class EventData<V> : BaseLocalEventData
    {
        public UnityAction<V> action;
    }
    public class EventData : BaseLocalEventData
    {
        public UnityAction action;
    }
    /// <summary>
    /// 事件引用
    /// </summary>
    protected Dictionary<T, BaseLocalEventData> dicEvent = new Dictionary<T, BaseLocalEventData>();
    /// <summary>
    /// 延迟事件触发容器（区分延迟执行，和某组件拿到事件后延迟执行
    /// </summary>
    //private Dictionary<T,BaseLocalEventData> delayDicEvent= new Dictionary<T,BaseLocalEventData>();
    public void EventTigger(T e_Play)
    {
        if (dicEvent.TryGetValue(e_Play, out var data))
        {
            (data as EventData).action?.Invoke();

        }
    }
    public void EventTigger<V>(T e_Play, V value)
    {
        if (dicEvent.TryGetValue(e_Play, out var data))
        {
            (data as EventData<V>).action?.Invoke(value);
        }
    }

    public void AddEventListener(T e_Play, UnityAction action)
    {
        if (!dicEvent.ContainsKey(e_Play))
        {
            dicEvent.Add(e_Play, new EventData());
        }
        (dicEvent[e_Play] as EventData).action += action;
    }
    /// <summary>
    /// 有参事件注册
    /// </summary>
    /// <typeparam name="V">参数类型</typeparam>
    /// <param name="e_Play"></param>
    /// <param name="action"></param>
    public void AddEventListener<V>(T e_Play, UnityAction<V> action)
    {
        if (!dicEvent.ContainsKey(e_Play))
        {
            dicEvent.Add(e_Play, new EventData<V>());
        }
        (dicEvent[e_Play] as EventData<V>).action += action;
    }

    //清空所有事件
    public void ClearAll()
    {
        dicEvent.Clear();
    }
    //此处暂时不做事件注销，主要原因是各状态类保证在玩家存在时一定存在且实例不变
    //后续若有状态引用切换等需求，则开发注销逻辑，并在状态类生命周期结束后调用
}
