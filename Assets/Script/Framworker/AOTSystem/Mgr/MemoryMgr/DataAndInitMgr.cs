using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;


/// <summary>
/// 当前可修改的键位
/// </summary>
public enum E_InputActioon
{
    left,
    right,
    jump,
}
/// <summary>
/// 数据与初始化管理器
/// </summary>
public class DataAndInitMgr 
{
    private static DataAndInitMgr instance = new DataAndInitMgr();
    public static DataAndInitMgr Instance => instance;

    /// <summary>
    /// 必备资源同步动态加载路径，框架依赖
    /// </summary>
    public string resourcesNecessaryAssetsPath = "NecessaryAssets/";
    /// <summary>
    /// 同步动态加载默认路径
    /// </summary>
    public string defaultResourcesPath = "default/";

    private DataAndInitMgr()
    {
        //初始化
        LoadRebind();
        musicData = new();
        //Debug.Log("数据管理器加载");

    }
    public void Init()
    {
        musicData = LoadDataBinary<MusicInfo>();
        //Debug.Log("数据管理器执行");
    }

    #region 持久化数据结构类
    /// <summary>
    /// 改建数据
    /// </summary>
    public class InputInfo
    {

        [FieldOrder(0)] public string left = "<Keyboard>/a";
        [FieldOrder(1)] public string right = "<Keyboard>/d";

        [FieldOrder(2)] public string jump = "<Keyboard>/space";
    }

    /// <summary>
    /// 音乐数据
    /// </summary>
    public class MusicInfo
    {
        [FieldOrder(0)] public bool bkMusicOpen = true;
        [FieldOrder(1)] public bool soundOpen = true;
        [FieldOrder(2)] public float bkMusicValue = 0.5f;
        [FieldOrder(3)] public float soundValue = 0.5f;
    }
    #endregion

    #region 音乐数据相关
    public MusicInfo musicData;
    /// <summary>
    /// 保存音乐数据
    /// </summary>
    public void SaveMusicData()
    {
        SaveDataBinary(musicData);
    }
    #endregion

    #region 改键相关
    //
    //核心文件，只读
    private InputActionAsset assetin;
    public InputActionAsset asset => assetin;

    public InputInfo info;
    
    public E_InputActioon nowInputType;
    //改键相关变量

 
    /// <summary>
    /// 保存改建数据
    /// 保存数据唯一入口，为便于管理，所以info为外部只读
    /// </summary>
    public void SeveInputValue()
    {

        //获取字符串值
        string toJson = JsonUtility.ToJson(info);
        //写入数据文件
        File.WriteAllText(UnityEngine.Application.persistentDataPath + "/InputInfo.json", toJson);
        //同步内存数据
        LoadRebind();
        
    }
    /// <summary>
    /// 加载改建数据
    /// </summary>
    public void LoadRebind()
    {
        string path = UnityEngine.Application.persistentDataPath + "/InputInfo.json";
        if (File.Exists(path))
        {
            string strJson = File.ReadAllText(path);
            info = JsonUtility.FromJson<InputInfo>(strJson);
        }
        else
        {
            info= new InputInfo();
        }
        //读取原始文件
        string jsonbase = Resources.Load<TextAsset>("player").text;
        //替换关键字符
        //string json = jsonbase.Replace("<up>", info.up);
        //json= json.Replace("<down>", info.down);
        string json= jsonbase.Replace("<left>", info.left);
        json= json.Replace("<right>", info.right);
        json= json.Replace("<jump>", info.jump);
        //生成配置文件
        assetin = InputActionAsset.FromJson(json);
    }
    #endregion

    #region 二进制数据相关
    /// <summary>
    /// 二进制配置文件存储路径
    /// </summary>
    public static string binaryDataPath = UnityEngine.Application.streamingAssetsPath + "/BinaryData/";
    /// <summary>
    /// 默认二进制数据持久化路径
    /// </summary>
    public static string usingBinaryDataPath =  UnityEngine.Application.persistentDataPath + "/";
    /// <summary>
    /// 容器类的容器，配置文件信息
    /// 键名表示容器类名
    /// </summary>
    public static Dictionary<string,BaseCollection> ininDataDic = new Dictionary<string,BaseCollection>();

    /// <summary>
    /// 存储二进制数据，固定路径，文件名为类名
    /// </summary>
    /// <typeparam name="T">纯数据类</typeparam>
    /// <param name="obj">实例对象</param>
    public void SaveDataBinary<T>(T obj) where T : class
    {
        //获取类型
        Type type = typeof(T);
        //获取所有变量
        FieldInfo[] fields = type.GetFields();

        string DataPath = usingBinaryDataPath + typeof(T).Name + ".bitto";

        if(!Directory.Exists(usingBinaryDataPath))
            Directory.CreateDirectory(usingBinaryDataPath);

        using(FileStream fs=new FileStream(DataPath, FileMode.Create,FileAccess.Write))
        {
            //按序写入内容
            foreach (FieldInfo fi in fields)
            {
                if (fi.FieldType == typeof(int))
                {
                    int intValue = (int)fi.GetValue(obj);
                    fs.Write(BitConverter.GetBytes(intValue), 0, 4);
                }
                else if (fi.FieldType == typeof(float))
                {
                    float floatValue= (float)fi.GetValue(obj);
                    fs.Write(BitConverter.GetBytes(floatValue), 0, 4);
                }
                else if(fi.FieldType == typeof(bool))
                {
                    bool boolValue= (bool)fi.GetValue(obj);
                    fs.Write(BitConverter.GetBytes(boolValue), 0, 1);
                }
                else if (fi.FieldType == typeof(string))
                {
                    string str= (string)fi.GetValue(obj);
                    fs.Write(BitConverter.GetBytes(str.Length), 0, 4);
                    fs.Write(Encoding.UTF8.GetBytes(str),0,str.Length);
                }
                else
                {
                    Debug.LogError($"非法字段,类型{obj.GetType().Name}为非纯数据类");
                    fs.Close();
                    File.Delete(DataPath);
                    return;
                }
            }

            fs.Close ();
        }
    }
    /// <summary>
    /// 读取二进制文件,未找到文件时候返回new对象
    /// </summary>
    /// <typeparam name="T">类型</typeparam>
    /// <returns></returns>
    public T LoadDataBinary<T>() where T : class, new()
    {
        //获取类型
        Type type = typeof(T);
        //获取所有变量
        FieldInfo[] fields = type.GetFields();
        //创建
        T data = new T();
        string DataPath = usingBinaryDataPath + typeof(T).Name + ".bitto";

        if(!File.Exists(DataPath))
        {
            return data;
        }

        using(FileStream fs=File.Open(DataPath, FileMode.Open, FileAccess.Read))
        {
            //读取全部字节内容
            byte[] buffer = new byte[fs.Length];
            fs.Read(buffer, 0, buffer.Length);
            fs.Close();
            int index = 0;

            foreach(FieldInfo fi in fields)
            {
                if(fi.FieldType == typeof(int))
                {
                    int num = BitConverter.ToInt32(buffer, index);
                    index += 4;
                    fi.SetValue(data, num);
                }
                else if(fi.FieldType == typeof(float))
                {
                    float num= BitConverter.ToSingle(buffer, index); 
                    index += 4;
                    fi.SetValue(data, num);
                }
                else if(fi.FieldType == typeof(bool))
                {
                    bool va= BitConverter.ToBoolean(buffer, index);
                    index += 1;
                    fi.SetValue(data, va);
                }
                else if (fi.FieldType == typeof(string))
                {
                    int leng= BitConverter.ToInt32(buffer, index);
                    index += 4;
                    string str=Encoding.UTF8.GetString(buffer, index, leng);
                    index += leng;
                    fi.SetValue(data, str);
                }
                else
                {
                    Debug.LogError($"非法字段,类型{typeof(T)}为非纯数据类");
                    return null;
                }
            }
        }

        return data;
    }
    #endregion
}
