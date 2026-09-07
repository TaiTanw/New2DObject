using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.UI;

public class InputChangePanel : BasePanel
{

    private Text leftT;
    private Text rightT;
    private Text FT;



    void Start()
    {
        //找到文本控件

        leftT = FindUIObj<Text>("leftT");
        rightT = FindUIObj<Text>("rightT");
        FT = FindUIObj<Text>("FT");
        //初始化文本面板显示
        UpdateUIvalue();


    }
    public override void HideMe()
    {
        base.HideMe();
        //print("隐藏面板");
        this.gameObject.SetActive(false);
    }

    protected override void OnClickButton(string UIname)
    {
        base.OnClickButton(UIname);
        switch (UIname)
        {

            case "left":
                print("左键修改");
                ChangeInputAtionI(E_InputActioon.left);

                break;

            case "right":
                print("右键修改");
                ChangeInputAtionI(E_InputActioon.right);

                break;

            case "ff":
                print("跳跃修改");
                ChangeInputAtionI(E_InputActioon.jump);

                break;
            case "out":
                //print("退出改建面板");
                //存入数据(可放入面板退出按钮）
                //DataAndInitMgr.Instance.SeveInputValue();
                //隐藏
                UIMgr.Instance.UIBack();
                break;
        }
       
    }
    
    private void ChangeInputAtionI(E_InputActioon type)
    {
        DataAndInitMgr.Instance.nowInputType = type;
        InputSystem.onAnyButtonPress.CallOnce(ChangeBtnReally);

    }
    private void ChangeBtnReally(InputControl control)//注意时序问题，按键修改都写在函数内部！！！
    {
        //拆分获得可替换字符
        string[] nowPaths = control.path.Split('/');
        string nowpath = $"<{nowPaths[1]}>/{nowPaths[2]}";
        //构建类用于赋值,从管理器获取，避免多次修改的数据被覆盖
        DataAndInitMgr.InputInfo nowInfo= DataAndInitMgr.Instance.info;
        //根据当前类型判断修改项
        switch (DataAndInitMgr.Instance.nowInputType)
        {

            case E_InputActioon.left:
                nowInfo.left=nowpath;
                break;
            case E_InputActioon.right:
                nowInfo.right=nowpath;
                break;
            case E_InputActioon.jump:
                nowInfo.jump=nowpath;
                break;
        }
        //重新加载
        DataAndInitMgr.Instance.SeveInputValue();
        //更新面板
        UpdateUIvalue();
        //更改键位
        InputControlMgr.Instance.BindInputAsset();
    }

    /// <summary>
    /// 面板更新
    /// </summary>
    public void UpdateUIvalue()
    {

        leftT.text = DataAndInitMgr.Instance.asset.FindAction("Move").bindings[1].path;
        rightT.text = DataAndInitMgr.Instance.asset.FindAction("Move").bindings[2].path;
        FT.text = DataAndInitMgr.Instance.asset.FindAction("Jump").bindings[0].path;
    }   
}
