using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 默认资源名称相关
/// </summary>
[CreateAssetMenu(fileName ="StartResName",menuName ="自定义数据结构/场景配置数据/默认资源名称")]
public class StartRes : ScriptableObject
{
    /// <summary>
    /// 默认角色模型指向名称
    /// </summary>
    public string playerModleName = "player";
    /// <summary>
    /// 开始的默认背景音乐名
    /// </summary>
    public string BkDefaultMusicName = "BkMusic";
}
