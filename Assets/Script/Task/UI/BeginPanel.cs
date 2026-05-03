using System;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.UI;

public class BeginPanel : BasePanel
{
    protected override void Awake()
    {
        //注意：若初始化写在start函数内，会后于show方法调用，逻辑上有误，可能导致show在调用组件时候未初始化
        base.Awake();
        AddEventAction();
        //添加场景加载监听，获取加载进度
        //EventCenterSystem.Instance.AddEventListener<float>(E_EventEnum.E_LoadScene, LoadChange);
        //print("2，开始面板初始化成功");

    }
    void Start()
    {
        
    }
    private void LoadChange(float value)
    {
        print(value);
    }
    
    protected override void OnClickButton(string UIname)
    {
        base.OnClickButton(UIname);
        switch(UIname)
        {
            case "t1":
                //print("t1触发");
                //UIMgr.Instance.ShowOneUI<ChangePanel>();
                //UIMgr.Instance.HideOneUI<BeginPanel>();

                UIMgr.Instance.SceneUIShow(E_UI_Process.ChangePanel);
                break;
            case "t2":
                //print("t2触发");
                //UIMgr.Instance.ShowOneUI<SetPanel>();
                //UIMgr.Instance.HideOneUI<BeginPanel>();

                UIMgr.Instance.SceneUIShow(E_UI_Process.SetPanel);
                break;
            case "t3":
                //print("t3触发");
                Application.Quit();
                break;
        }
    }

  

    public override void ShowMe()
    {
        base.ShowMe();
        //this.gameObject.SetActive(true);
        //print("3，开始面板加载成功");
    }

    public override void HideMe()
    {
        base.HideMe();
        //this.gameObject.SetActive(false);
        //print("成功隐藏开始面板");
    }

    private void OnDestroy()
    {
        //事件监听增减需配对
        //EventCenterSystem.Instance.RemoveEventListener<float>(E_EventEnum.E_LoadScene, LoadChange);
    }

    public void TeskPanel()
    {
        print("获取面板。执行逻辑");
    }
    
    /// <summary>
    /// 自定义事件添加
    /// </summary>
    public void AddEventAction()
    {
        //AddEventTriggerListener<Button>("t1", UnityEngine.EventSystems.EventTriggerType.PointerEnter, (a) =>
        //{
        //    print("鼠标进入");
        //});
        //AddEventTriggerListener<Button>("t1", UnityEngine.EventSystems.EventTriggerType.PointerExit, (a) =>
        //{
        //    print("鼠标离开");
        //});
    }
}
