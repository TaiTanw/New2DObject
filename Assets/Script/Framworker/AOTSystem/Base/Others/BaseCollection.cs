using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;

public abstract class BaseCollection 
{
    //原则上，容器基类不允许new
    public BaseCollection() 
    {
        //Debug.Log("当前类名" + this.GetType().Name);
        //得到类型
        Type typeC = this.GetType();
        //得到容器类的变量数组
        FieldInfo[] fieldInfos = typeC.GetFields();
        //类型排序
        Array.Sort(fieldInfos, (a, b) =>
        {
            int oa = a.GetCustomAttribute<FieldOrderAttribute>()?.Order ?? int.MaxValue;
            int ob = b.GetCustomAttribute<FieldOrderAttribute>()?.Order ?? int.MaxValue;
            return oa.CompareTo(ob);
        });

        //单个字典容器
        foreach (FieldInfo infoDic in fieldInfos)
        {

            //获取字段类型的类型
            Type fieldType = infoDic.FieldType;

            if (fieldType.IsGenericType)//判断是否为泛型类型
            {
                Type[] args = fieldType.GetGenericArguments();
                //获取数据结构类型名称，作为文件路径拼接
                Type tp = args[1];
                //Debug.Log(tp.Name);
                //获取此类型所有变量，用于按序存入数据
                FieldInfo[] infos = tp.GetFields()
                .Select(f => new { Field = f, Order = f.GetCustomAttribute<FieldOrderAttribute>()?.Order ?? int.MaxValue })
                .OrderBy(x => x.Order)
                .Select(x => x.Field)
                .ToArray();

                if (!File.Exists(DataAndInitMgr.binaryDataPath + tp.Name + ".bitto"))
                {
                    Debug.LogError($"未找到此字段{tp.Name}对应的数据文件，请检查容器类键类型是否正确");
                }
                //文件流读取
                using (FileStream fs = File.Open(DataAndInitMgr.binaryDataPath + tp.Name + ".bitto", FileMode.Open, FileAccess.Read))
                {
                    //声明字节数组长度
                    byte[] data = new byte[fs.Length];
                    //一次性全部读出
                    fs.Read(data, 0, data.Length);
                    //关闭流
                    fs.Close();
                    //当前读取位置
                    int index = 0;
                    //读取数据行数
                    int dataLength = BitConverter.ToInt32(data, 0);
                    index += 4;
                    //读取主键名
                    int nameLength = BitConverter.ToInt32(data, index);
                    index += 4;
                    string keyName = Encoding.UTF8.GetString(data, index, nameLength);
                    index += nameLength;
                    //Debug.Log(keyName);
                    //for循环，长度为表长
                    for (int i = 0; i < dataLength; i++)
                    {
                        //实例化的数据结构类，用于接收数据
                        object tpObj = Activator.CreateInstance(tp);
                        //开始读取具体数据
                        foreach (FieldInfo infoa in infos)
                        {
                            if (infoa.FieldType == typeof(int))
                            {
                                infoa.SetValue(tpObj, BitConverter.ToInt32(data, index));
                                index += 4;
                            }
                            else if (infoa.FieldType == typeof(float))
                            {
                                infoa.SetValue(tpObj, BitConverter.ToSingle(data, index));
                                index += 4;
                            }
                            else if (infoa.FieldType == typeof(bool))
                            {
                                infoa.SetValue(tpObj, BitConverter.ToBoolean(data, index));
                                index += 1;
                            }
                            else if (infoa.FieldType == typeof(string))
                            {
                                int lon = BitConverter.ToInt32(data, index);
                                index += 4;
                                infoa.SetValue(tpObj, Encoding.UTF8.GetString(data, index, lon));
                                index += lon;
                            }
                            else
                            {
                                Debug.LogError($"变量类型{infoa.FieldType.Name}非法，无法读取");
                            }
                        }
                        //单条数据读完，存入容器类的对应字典内
                        //由主键名获取主键变量的值
                        object intNum = tp.GetField(keyName).GetValue(tpObj);
                        //获取容器对象的字典值
                        object dicData = infoDic.GetValue(this);
                        //通过字典对象得到其中的 Add方法
                        MethodInfo mInfo = dicData.GetType().GetMethod("Add");
                        //通过Add方法存入数据
                        mInfo.Invoke(dicData, new object[] { intNum, tpObj });
                    }
                }
            }
            
        }
        //将整个数据容器类存入数据管理器
        DataAndInitMgr.ininDataDic.Add(this.GetType().Name, this);
    }
    
    //protected void Init()
    //{
        
    //}
}
