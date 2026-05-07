using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;
using UnityEngine.UIElements;
using System.Drawing;
using UnityEngine.Playables;

/// <summary>
/// 原始输入
/// </summary>
public class PlayerInputData
{
    public float moveInput;    //水平输入
    public bool jumpPressed;   // 这一帧是否按下
}
/// <summary>
/// 动作执行
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
        //玩家输入信息初始化
        //inputData =new PlayerInputData();此处会导致数据引用错误，所以初始化交给外部的输入管理系统
        //玩家主动拉取输入控制权限，调用方法保证唯一玩家控制权,且便于控制时序，保证先于状态机初始化
        InputControlMgr.Instance.BindPlayer(this);
        //可执行动作数据初始化
        actionData = new ActionData();
        //物理状态初始化
        playPDate = gameObject.GetComponent<CharacterPhysics>();
        playPDate.Init(actionData);
        //表现层初始化
        animatorFSM=gameObject.GetComponent<PresentationLayer>();
        animatorFSM.Init(playPDate,actionData);
        //逻辑状态机初始化
        fsm = new PlayerStateMachine();
        fsm.InitData(playPDate, inputData, actionData);

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






/// <summary>
/// 玩家状态机
/// </summary>
public class PlayerStateMachine
{
    /// <summary>
    /// 当前状态
    /// </summary>
    public IBehavioralState onState;

    public IsOnGround onGround;
    public IsInAir inAir;
    /// <summary>
    /// 可跳跃次数
    /// </summary>
    public int jumpNum=1;
    /// <summary>
    /// 土狼时间
    /// </summary>
    public float coyoteTime = 0.1f;

    /// <summary>
    /// 跳跃缓冲时间
    /// </summary>
    public float jumpBuffer = 0.2f;
    public PlayerStateMachine()
    {
        onGround = new IsOnGround();
        inAir = new IsInAir();
        //当前状态信息初始化
        onState = onGround;
    }

    public void InitData(CharacterPhysics playData, PlayerInputData input, ActionData actionData )
    {
        onGround.Init(playData, input, this, actionData);
        inAir.Init(playData,input, this, actionData);
    }

    /// <summary>
    /// 状态改变
    /// </summary>
    public void ChangeState(IBehavioralState state)
    {
        onState.Exit();
        onState=state;
        onState.Enter();
    }

    /// <summary>
    /// 供玩家mono执行的玩家更新逻辑
    /// </summary>
    /// <param name="playData">玩家当前状态</param>
    /// <param name="input">当前输入信息</param>
    /// <param name="stateMachine">玩家自身状态机</param>
    public void Update(PlayerInputData input)
    {
        //传入执行，状态类负责实际功能
        onState.Update();
        //效果已经触发，则关闭，防止持续跳跃
        input.jumpPressed = false;//触发类型按键全部统一由状态机复原
    }
}
