using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;
using UnityEngine.UIElements;
using System.Drawing;
using UnityEngine.Playables;
using UnityEngine.Events;
using System.Numerics;

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

        //玩家主动拉取输入控制权限，调用方法保证唯一玩家控制权,且便于控制时序，保证先于状态机初始化
        InputControlMgr.Instance.BindPlayer(this);
        //可执行动作数据初始化
        actionData = new ActionData();

        //逻辑状态机/物理状态/表现层初始化（保证存在
        fsm = new PlayerStateMachine();
        playPDate = gameObject.GetComponent<CharacterPhysics>();
        animatorFSM = gameObject.GetComponent<PresentationLayer>();
        //保证可用
        fsm.InitData(playPDate, inputData, actionData);
        playPDate.Init(actionData, fsm);
        animatorFSM.Init(playPDate, actionData);

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
    #region 内部事件系统

    public enum E_playEvent
    {
        move,
        jump,

    }

    public abstract class BasePlayerEventData { }
    public class EventData<T> : BasePlayerEventData
    {
        public UnityAction<T> action;
    }
    public class EventData : BasePlayerEventData
    {
        public UnityAction action;
    }
    /// <summary>
    /// 事件引用
    /// </summary>
    Dictionary<E_playEvent, BasePlayerEventData> dicEvent = new Dictionary<E_playEvent, BasePlayerEventData>();

    public void EventTigger(E_playEvent e_Play)
    {
        if (dicEvent.ContainsKey(e_Play))
        {
            (dicEvent[e_Play] as EventData).action?.Invoke();
        }
    }
    public void EventTigger<T>(E_playEvent e_Play,T value)
    {
        if (dicEvent.ContainsKey(e_Play))
        {
            (dicEvent[e_Play] as EventData<T>).action?.Invoke(value);
        }
    }

    public void AddEventListener(E_playEvent e_Play,UnityAction action)
    {
        if (!dicEvent.ContainsKey(e_Play))
        {
            dicEvent.Add(e_Play, new EventData());
        }
        (dicEvent[e_Play] as EventData).action += action;
    }
    public void AddEventListener<T>(E_playEvent e_Play, UnityAction<T> action)
    {
        if (!dicEvent.ContainsKey(e_Play))
        {
            dicEvent.Add(e_Play, new EventData<T>());
        }
        (dicEvent[e_Play] as EventData<T>).action += action;
    }

    //清空所有事件
    public void ClearAll()
    {
        dicEvent.Clear();
    }
    //此处暂时不做事件注销，主要原因是各状态类保证在玩家存在时一定存在且示例不变
    //后续若有状态引用切换等需求，则开发注销逻辑，并在状态类生命周期结束后调用
    #endregion

    /// <summary>
    /// 当前状态
    /// </summary>
    public IBehavioralState onState;

    public IsOnGround onGround;
    public IsInAir inAir;
    #region 配置数据
    /// <summary>
    /// 在空中可跳跃跳跃数
    /// </summary>
    public int jumpNum=0;
    /// <summary>
    /// 土狼时间
    /// </summary>
    public float coyoteTime = 0.1f;

    /// <summary>
    /// 跳跃缓冲时间
    /// </summary>
    public float jumpBuffer = 0.2f;
    #endregion

    #region 运行时数据
    /// <summary>
    /// 当前空中可跳跃次数
    /// </summary>
    public int jumpCount=0;
    /// <summary>
    /// 跳跃缓冲开关
    /// </summary>
    public bool jbtB;
    /// <summary>
    /// 是否可使用地面跳跃
    /// </summary>
    public bool groundJump;
    #endregion

    public PlayerStateMachine()
    {
        onGround = new IsOnGround();
        inAir = new IsInAir();
        //当前状态信息初始化
        onState = onGround;

        jumpCount=jumpNum;
    }

    public void InitData(CharacterPhysics playData, PlayerInputData input, ActionData actionData )
    {
        onGround.Init(playData, input, this, actionData);
        inAir.Init(playData,input, this, actionData);
        onState.Enter();
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
    /// <summary>
    /// 重置可跳次数
    /// </summary>
    public void JumpCountToNum()
    {
        jumpCount = jumpNum;
    }
    /// <summary>
    /// 执行跳跃并判断是否可跳跃
    /// </summary>
    /// <returns></returns>
    public bool JumpCan()
    {
        jumpCount--;
        if ( jumpCount < 0)
        {
            jumpCount = 0;
            return false;
        }
        return true;
            
    }
}
