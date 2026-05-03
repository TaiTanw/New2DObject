using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum E_SceneMode 
{
    /// <summary>
    /// 游戏模式(默认
    /// </summary>
    Game,
    /// <summary>
    /// 剧情模式
    /// </summary>
    Plot,
}

/// <summary>
/// 场景逻辑管理器，用于管理单个场景的流程推进和返回
/// </summary>
public class SceneLogicMgr : BaseMgr<SceneLogicMgr>
{
    public void Init()
    {

    }
    /// <summary>
    /// 对话框面板引用
    /// </summary>
    DialogBoxPanel dialogBoxPanel;
    /// <summary>
    /// 当前所属剧情场景ID，
    /// </summary>
    int nowSceneID = -1;

    E_SceneMode nowsceneMode=E_SceneMode.Game;

    public void ChangeSceneMode(E_SceneMode mode)
    {
        switch (mode)
        {
            case E_SceneMode.Game:

                break;
            case E_SceneMode.Plot:
                nowsceneMode = mode;
                UIMgr.Instance.ShowOneUI<DialogBoxPanel>(E_UILayer.Middle, (T) =>
                {
                    dialogBoxPanel = T;
                });
                nowSceneID += 1;
                break;
            default:
                break;
        }

    }
    /// <summary>
    /// 开始播放文本
    /// </summary>
    public async void StartText(string text)
    {
        if(nowsceneMode==E_SceneMode.Plot)
        {
            await dialogBoxPanel.PlayText(text);
            Debug.Log("打印结束");
            await UniTask.WaitUntil(() => dialogBoxPanel.IsOK);
            Debug.Log("开始下一句");
        }
        
    }

    public void TextUpdate()
    {

    }
}
