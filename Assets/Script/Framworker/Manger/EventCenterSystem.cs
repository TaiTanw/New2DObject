using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngineInternal;


/// <summary>
/// 事件中心
/// </summary>
public class EventCenterSystem : BaseMgr<EventCenterSystem>
{
    /// <summary>
    /// 事件数据基类，用于里氏替换原则的装载
    /// </summary>
    public abstract class BaseEventDataI { }

    public class EventData<T> : BaseEventDataI
    {
        public UnityAction<T> action;
    }
    public class EventData : BaseEventDataI
    {
        public UnityAction action;
    }


    public Dictionary<E_EventEnum,BaseEventDataI> dicEvent=new Dictionary<E_EventEnum,BaseEventDataI>();

    //事件触发
    /// <summary>
    /// 有参传入方法
    /// </summary>
    /// <typeparam name="T">参数类型</typeparam>
    /// <param name="eventenum">事件枚举</param>
    /// <param name="value"></param>
    public void EventTrigger<T>(E_EventEnum eventenum,T value)
    {
        if (dicEvent.ContainsKey(eventenum))
        {
            (dicEvent[eventenum] as EventData<T>).action?.Invoke(value);
        }
    }
    public void EventTrigger(E_EventEnum eventenum)
    {
        if (dicEvent.ContainsKey(eventenum))
        {
            (dicEvent[eventenum] as EventData).action?.Invoke();
        }
    }

    //监听注册/添加
    public void AddEventListener<T>(E_EventEnum eventEnum,UnityAction<T> inAcion)
    {
        if (!dicEvent.ContainsKey(eventEnum))
        {
            dicEvent.Add(eventEnum, new EventData<T>());
            (dicEvent[eventEnum] as EventData<T>).action += inAcion;
        }
        else
        {
            (dicEvent[eventEnum] as EventData<T>).action += inAcion;
        }
    }
    public void AddEventListener(E_EventEnum eventEnum, UnityAction inAcion)
    {
        if (!dicEvent.ContainsKey(eventEnum))
        {
            dicEvent.Add(eventEnum, new EventData());
            (dicEvent[eventEnum] as EventData).action += inAcion;
        }
        else
        {
            (dicEvent[eventEnum] as EventData).action += inAcion;
        }
    }

    //监听事件减少
    public void RemoveEventListener<T>(E_EventEnum eventEnum, UnityAction<T> inAcion)
    {
        if (dicEvent.ContainsKey(eventEnum))
        {
            (dicEvent[eventEnum] as EventData<T>).action -= inAcion;
        }
        
    }
    public void RemoveEventListener(E_EventEnum eventEnum, UnityAction inAcion)
    {
        if (dicEvent.ContainsKey(eventEnum))
        {
            (dicEvent[eventEnum] as EventData).action -= inAcion;
        }
        
    }

    //监听者注销
    public void ClearOneEvent(E_EventEnum eventEnum)
    {
        if (dicEvent.ContainsKey(eventEnum))
        {
            dicEvent.Remove(eventEnum);
        }
    }


    //清空所有事件
    public void ClearAll()
    {
        dicEvent.Clear();
    }

    //实际上，同一个对象可能动态注册同一个枚举的不同监听触发执行的函数，后续可能考虑对此进行优化
    //添加事件注册时候，传入注册者，在EventData数据类中添加一种成员变量作为容器，外部可主动清除自身所注册的所有事件
    //也可以检测注册对象是否存在，然后用管理器同一执行清除
}
