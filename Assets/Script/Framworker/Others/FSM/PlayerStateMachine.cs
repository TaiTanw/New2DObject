using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 玩家状态机
/// </summary>
public class PlayerStateMachine
{
    public enum E_playEvent
    {
        move,
        jump,
        jumpRelease,//跳跃长按
    }

    LocalEventSystem<E_playEvent> eventSystem;
    public LocalEventSystem<E_playEvent> EventSystem=>eventSystem;


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
    public int jumpNum = 1;
    /// <summary>
    /// 土狼时间
    /// </summary>
    public float coyoteTime = 0.1f;

    /// <summary>
    /// 跳跃缓冲时间
    /// </summary>
    public float jumpBuffer = 0.2f;
    /// <summary>
    /// 最小起跳时间
    /// </summary>
    public float jumpUpTime = 0.1f;
    #endregion

    #region 运行时数据
    /// <summary>
    /// 当前空中可跳跃次数
    /// </summary>
    public int jumpCount = 0;
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
        eventSystem=new LocalEventSystem<E_playEvent>();
        //当前状态信息初始化
        onState = onGround;

        jumpCount = jumpNum;
    }

    public void InitData(CharacterPhysics.PlayerPhysicsData playData, PlayerInputData input, ActionData actionData)
    {
        onGround.Init(playData, input, this, actionData);
        inAir.Init(playData, input, this, actionData);
        onState.Enter();
    }

    /// <summary>
    /// 状态改变
    /// </summary>
    public void ChangeState(IBehavioralState state)
    {
        onState.Exit();
        onState = state;
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
        input.jumpRelease = false;
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
        if (jumpCount <= 0)
        {
            return false;
        }

        jumpCount--;
        return true;

    }
}
