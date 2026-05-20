using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


/// <summary>
/// 原始输入
/// </summary>
public class PlayerInputData
{
    public float moveInput;    //水平输入
    public bool jumpPressed;   // 这一帧是否按下
}
/// <summary>
/// 持续性动作执行数据
/// </summary>
public class ActionData 
{
    public float onMove;
    public bool onJump;
}

public class Player : MonoBehaviour
{

    /// <summary>
    /// 运行时物理数据
    /// </summary>
    CharacterPhysics playPDate;
    /// <summary>
    /// 玩家当前输入数据,初始化交给外部
    /// </summary>
    PlayerInputData inputData;
    /// <summary>
    /// 玩家当前所执行动作
    /// </summary>
    ActionData actionData;
    /// <summary>
    /// 玩家逻辑状态机对象
    /// </summary>
    PlayerStateMachine fsm;
    /// <summary>
    /// 表现层状态机
    /// </summary>
    PresentationLayer animatorFSM;

    void Awake()
    {

        //玩家主动拉取输入控制权限，调用方法保证唯一玩家控制权,且便于控制时序，保证先于状态机初始化
        InputControlMgr.Instance.BindPlayer(this);
        //可执行动作数据初始化
        actionData = new ActionData();

        //逻辑状态机/物理状态/表现层初始化（保证存在
        fsm = new PlayerStateMachine();
        playPDate = gameObject.GetComponent<CharacterPhysics>();
        animatorFSM = gameObject.GetComponent<PresentationLayer>();

    }
    private void Start()
    {
        //保证可用
        fsm.InitData(playPDate.PlayPhysicsData, inputData, actionData);
        playPDate.Init(actionData, fsm.EventSystem);
        animatorFSM.Init(playPDate.PlayPhysicsData, actionData);
    }
    /// <summary>
    /// 注入玩家按键数据行为
    /// </summary>
    /// <param name="data"></param>
    public void ChangeInputAsset(PlayerInputData data)
    {
        inputData = data;
    }
    //float time;
    void Update()
    {
        //逻辑状态机更新，接收输入信息发送动作数据
        fsm.Update(inputData);
    }

}

