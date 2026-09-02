using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class BaseMgr<T> where T : class, new()
{
    private static T instance;

    /// <summary>
    /// 用于加锁的对象
    /// </summary>
    protected static readonly object lockObj = new object();

    // 注意：static 字段在泛型类中是“每个具体 T 一份”
    //基类里面的加锁对象由于泛型，则被CLR 类型系统识别为不同对象，因此不必担心因为继承同基类，使得不同管理器实例化会占有共同锁

    //懒汉式单例模式，需注意线程安全
    public static T Instance
    {

        get 
        { 
            //若已经实例化，则直接返回
            if (instance == null)
            {
                //开始实例化，加锁避免并发问题
                lock (lockObj)
                {
                    //可能同帧进入此处逻辑，实例化时候需要再次判断
                    if (instance == null)
                    {
                        instance = new T();
                    }
                }
            }
            
            
            return instance; 
        }
    }
}
