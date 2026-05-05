using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseStartSceneUIFlow : MonoBehaviour
{
    [Header("此场景初始显示的UI面板")]
    /// <summary>
    /// 当前状态（当前最上层显示的UI）
    /// </summary>
    public E_UI_Process nowState;
    /// <summary>
    /// 状态栈，便于返回和销毁
    /// </summary>
    Stack<E_UI_Process> stateStack=new Stack<E_UI_Process>();
    [Header("过场景是否销毁此场景的UI对象（默认为隐藏）")]
    /// <summary>
    /// 过场景时是否销毁此场景所有UI面板
    /// </summary>
    public bool isDeleteUI=false;

    private void Awake()
    {
        
        UIMgr.Instance.SetSceneUIFlow(this);
        
    }

    /// <summary>
    /// 子类重写初始状态
    /// </summary>
    void Start()
    {
        //nowState = E_UI_Process.BeginPanel;
        //print("111");
        EnterState(nowState, false);
    }


    public void ChangeState(E_UI_Process state)
    {
        EnterState(state);
    }

    protected void EnterState(E_UI_Process state,bool canBack=true)
    {
        if(canBack)
        {
            stateStack.Push(nowState);
        }

        ExitState(nowState);

        nowState = state;

        switch (state)
        {
            case E_UI_Process.BeginPanel:
                UIMgr.Instance.ShowOneUI<BeginPanel>();
                break;
            case E_UI_Process.ChangePanel:
                UIMgr.Instance.ShowOneUI<ChangePanel>();
                break;
            case E_UI_Process.InputChangePanel:
                UIMgr.Instance.ShowOneUI<InputChangePanel>();
                break;
            case E_UI_Process.MusicSetPanel:
                UIMgr.Instance.ShowOneUI<MusicSetPanel>();
                break;
            case E_UI_Process.SetPanel:
                UIMgr.Instance.ShowOneUI<SetPanel>();
                break;
            default:
                print("未知状态");
                break;
        }
    }
    void ExitState(E_UI_Process state)
    {
        switch (state)
        {
            case E_UI_Process.BeginPanel:
                UIMgr.Instance.HideOneUI<BeginPanel>();
                break;
            case E_UI_Process.ChangePanel:
                UIMgr.Instance.HideOneUI<ChangePanel>();
                break;
            case E_UI_Process.InputChangePanel:
                UIMgr.Instance.HideOneUI<InputChangePanel>();
                break;
            case E_UI_Process.MusicSetPanel:
                UIMgr.Instance.HideOneUI<MusicSetPanel>();
                break;
            case E_UI_Process.SetPanel:
                UIMgr.Instance.HideOneUI<SetPanel>();
                break;
            default:
                break;
        }
    }
    void DeltaeUIPanel(E_UI_Process state)
    {
        switch (state)
        {
            case E_UI_Process.BeginPanel:
                UIMgr.Instance.DistoryOneUI<BeginPanel>();
                break;
            case E_UI_Process.ChangePanel:
                UIMgr.Instance.DistoryOneUI<ChangePanel>();
                break;
            case E_UI_Process.InputChangePanel:
                UIMgr.Instance.DistoryOneUI<InputChangePanel>();
                break;
            case E_UI_Process.MusicSetPanel:
                UIMgr.Instance.DistoryOneUI<MusicSetPanel>();
                break;
            case E_UI_Process.SetPanel:
                UIMgr.Instance.DistoryOneUI<SetPanel>();
                break;
            default:
                break;
        }
    }

    public void ToBack()
    {
        if (stateStack.Count <= 0)
        {
            print("返回步骤过多，使得状态栈小于等于0");
            return;
        }
            
        EnterState(stateStack.Pop(),false);
    }

    private void OnDestroy()
    {
        if (MonoPublicMgr.IsQuitting)
        {
            return;
        }
        if (isDeleteUI)
        {
            while (stateStack.Count > 0)
            {
                DeltaeUIPanel(stateStack.Pop());
            }
            DeltaeUIPanel(nowState);
        }
        else
        {
            while (stateStack.Count > 0)
            {
                ExitState(stateStack.Pop());
            }
            ExitState(nowState);
        }
    }
}
