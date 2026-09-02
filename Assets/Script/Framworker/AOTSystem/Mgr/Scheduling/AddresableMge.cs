using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Events;
using UnityEngine.ResourceManagement.AsyncOperations;

public class AddresableMge 
{
    private static AddresableMge instance=new AddresableMge();
    public static AddresableMge Instance=>instance;

    private AddresableMge() { }

    private Dictionary<string, IEnumerator> ResDic=new Dictionary<string, IEnumerator>();

    //函数回调用Action而不是Unity Action
    /// <summary>
    /// 单资源加载
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    /// <param name="callBake"></param>
    public void LoadAssetAsyncI<T>(string name,Action<AsyncOperationHandle<T>> callBake)
    {
        string KeyName=name+typeof(T).Name;

        //声明操作句柄，用于存入和状态观察
        AsyncOperationHandle<T> hande;
        if (ResDic.ContainsKey(KeyName))
        {
            hande=(AsyncOperationHandle<T>)ResDic[KeyName];
            if (hande.IsDone)
            {
                callBake?.Invoke(hande);
            }
            else
            {
                hande.Completed += (obj)=> 
                {
                    if(obj.Status == AsyncOperationStatus.Succeeded)
                    {
                        callBake?.Invoke(hande);
                    }
                };
            }

            return;
        }

        //未加载过资源
        hande=Addressables.LoadAssetAsync<T>(name);
        hande.Completed += (obj) =>
        {
            if (obj.Status == AsyncOperationStatus.Succeeded)
            {
                callBake?.Invoke(hande);
            }
            else
            {
                if (ResDic[KeyName]!=null)
                {
                    Debug.LogError(KeyName + "加载失败");
                    ResDic.Remove(KeyName);
                }
            }
        };
        ResDic.Add(KeyName, hande);
    }

    /// <summary>
    /// 单个资源卸载
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="name"></param>
    public void Release<T>(string name)
    {
        string keyName=name+typeof(T).Name;
        if(ResDic.ContainsKey(keyName))
        {
            AsyncOperationHandle<T> hande=(AsyncOperationHandle<T>)ResDic[keyName];
            Addressables.Release(hande);
            ResDic.Remove(keyName);
        }

    }

    /// <summary>
    /// 加载多个资源或者多条件加载
    /// </summary>
    /// <typeparam name="T"></typeparam>
    /// <param name="mode"></param>
    /// <param name="callBake"></param>
    /// <param name="names"></param>
    public void LoadAssetsAsyncI<T>(Addressables.MergeMode mode,Action<T> callBake,params string[] names)
    {
        List<string> listNames = new List<string>(names);
        string keyName = "";
        foreach(string name in names)
        {
            keyName += name;
        }
        keyName += typeof(T).Name;

        //加载多资源或多种条件加载资源时候，返回值句柄类型的泛型为<IList<T>>
        AsyncOperationHandle<IList<T>> hande;

        if (ResDic.ContainsKey(keyName))
        {
            hande = (AsyncOperationHandle<IList<T>>)ResDic[keyName];
            if(hande.IsDone)
            {
                //加载已经结束，直接遍历后调用回调
                if (hande.Status == AsyncOperationStatus.Succeeded)
                {
                    //hande.Result是Ilist列表，需要遍历
                    foreach (T t in hande.Result)
                    {
                        callBake?.Invoke(t);
                    }
                }

            }
            else
            {
                hande.Completed += (obj) =>
                {
                    //if加载状态判断写在lambda表达式内部，用hande.Completed调用回调时，参数传入句柄自己而非资源本身，所以此处需要注意
                    if (obj.Status == AsyncOperationStatus.Succeeded)//此处小心闭包雷，错误写法：hande.Status==AsyncOperationStatus.Succeeded
                    {                                             //即使此处obj==hande，错误写法此时也能正常运行，但句柄被复用/异步链等等情况下会因为闭包而出错
                        //hande.Result是Ilist列表，需要遍历
                        foreach (T t in hande.Result)
                        {
                            callBake?.Invoke(t);
                        }
                    }
                };
            }
            return;
        }

        hande=Addressables.LoadAssetsAsync<T>(listNames,callBake,mode);
        hande.Completed += (obj) =>
        {
            if(obj.Status== AsyncOperationStatus.Failed)
            {
                Debug.LogWarning(keyName+"加载失败");
                if(ResDic.ContainsKey(keyName))
                {
                    ResDic.Remove(keyName);
                }
            }
        };

        ResDic.Add(keyName, hande);
    }

    /// <summary>
    /// 资源卸载
    /// </summary>
    /// <typeparam name="T">同类</typeparam>
    /// <param name="listNames">同类资源名单</param>
    public void Release<T>(params string[] listNames)
    {
        string keyName = "";
        foreach (string s in listNames)
        {
            keyName += s;
        }
        keyName += typeof(T).Name;
        if (ResDic.ContainsKey(keyName))
        {
            AsyncOperationHandle<IList<T>> hande = (AsyncOperationHandle<IList<T>>)ResDic[keyName];
            //注意Addressables.Release和hande.Release两种用法，本质一样，但不建议混用
            Addressables.Release(hande);
            ResDic.Remove(keyName);
            //用Addressables.InstantiateAsync实例化资源
            //再用Addressables.ReleaseInstance(。。。)销毁资源，会正常移除场景上的对象，并且引用计数正常变化，最为安全
        }
    }

    /// <summary>
    /// 确认资源名称是否存在，用于安全删除等操作
    /// </summary>
    /// <param name="assetName">资源名称</param>
    /// <returns></returns>
    public bool FindNameConfirmexistence(string assetName)
    {
        if (ResDic.ContainsKey(assetName))
        {
            return true;
        }
        else
        {
            return false;
        }
    }
}
