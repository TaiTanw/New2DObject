using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 角色行为状态接口
/// </summary>
public interface IBehavioralState
{
    /// <summary>
    /// 状态进入
    /// </summary>
    /// <param name="p"></param>
    public void Enter();

    /// <summary>
    /// 状态离开
    /// </summary>
    /// <param name="p"></param>
    public void Exit();
    /// <summary>
    /// 状态更新
    /// </summary>
    /// <param name="p"></param>
    public void Update();


    public void FixUpdate();

}

/// <summary>
/// 玩家状态机基类
/// </summary>
public class BasePlayerState : IBehavioralState
{
    //玩家物理状态/只读
    protected CharacterPhysics.PlayerPhysicsData playPhyData;
    //原始输入/只读
    protected PlayerInputData input;
    //过滤后的动作输出/读写
    protected ActionData playActionData;
    //依附的逻辑状态机/调用
    protected PlayerStateMachine stateMachine;
    //此状态机内部的事件系统
    protected LocalEventSystem<PlayerStateMachine.E_playEvent> localEventSystem;

    public void Init(CharacterPhysics.PlayerPhysicsData playPhyData, PlayerInputData input, PlayerStateMachine stateMachine,ActionData playActionData)
    {
        this.playPhyData = playPhyData;
        this.input = input;
        this.stateMachine = stateMachine;
        localEventSystem =this.stateMachine.EventSystem;
        this.playActionData = playActionData;
    }

    public virtual void Enter()
    {
        
    }


    public virtual void Exit()
    {
        
    }

    public virtual void Update()
    {
        
    }

    public virtual void FixUpdate()
    {
        
    }
}
/// <summary>
/// 是否在地面
/// </summary>
public class IsOnGround : BasePlayerState
{
    public override void Enter()
    {
        //Debug.Log("地面状态进入");
        stateMachine.JumpCountToNum();
        stateMachine.groundJump=true;
    }

    public override void Exit()
    {
        //Debug.Log("地面状态退出");
    }



    public override void Update()
    {
        //处理跳跃缓冲
        if (stateMachine.jbtB && stateMachine.groundJump)
        {
            //playActionData.onJump = true;
            localEventSystem.EventTigger(PlayerStateMachine.E_playEvent.jump);
            //Debug.Log("消费缓冲");
            stateMachine.jbtB = false;
            stateMachine.groundJump=false;
        }
        if (input.jumpPressed && stateMachine.groundJump)
        {
            //动作响应
            //playActionData.onJump = true;
            localEventSystem.EventTigger(PlayerStateMachine.E_playEvent.jump);
            //消耗地面跳跃
            stateMachine.groundJump = false;
            //触发状态复原，此处已经交给状态机
        }
        //若已经不在地面，则改变状态
        if (!playPhyData.isGrounded)
        {
            //直接靠成员变量来传入，避免频繁GC
            stateMachine.ChangeState(stateMachine.inAir);
        }
        //移动动作响应
        playActionData.onMove = input.moveInput;
    }
}

public class IsInAir : BasePlayerState
{
    /// <summary>
    /// 当前此状态时间
    /// </summary>
    float nowTime;
    /// <summary>
    /// 跳跃缓冲计时器
    /// </summary>
    float jbt;
    /// <summary>
    /// 起跳窗口时间
    /// </summary>
    float jumpWtime;
    /// <summary>
    /// 跳跃斩断开关
    /// </summary>
    bool canJumpCut;
    public override void Enter()
    {
        //Debug.Log("空中状态进入");
        nowTime = 0;
        jbt = 0;
        jumpWtime = 0;
    }

    public override void Exit( )
    {
        //Debug.Log("空中状态退出");
        nowTime = 0;
        jbt = 0;
    }


    public override void Update()
    {

        if (stateMachine.jbtB)
        {
            jbt += Time.deltaTime;

            if (jbt >stateMachine.jumpBuffer)
            {
                stateMachine.jbtB = false; // 超时自动关闭
            }
        }

        //响应跳跃键输入
        if (input.jumpPressed)
        {
            if(nowTime < stateMachine.coyoteTime && stateMachine.groundJump)//是否可使用地面跳
            {
                localEventSystem.EventTigger(PlayerStateMachine.E_playEvent.jump);
                jumpWtime=nowTime;
                canJumpCut = false;
                //避免同一窗口重复消费
                stateMachine.groundJump = false;
            }
            else if (stateMachine.JumpCan())//是否可使用空中跳
            {
                localEventSystem.EventTigger(PlayerStateMachine.E_playEvent.jump);
                jumpWtime = nowTime;
                canJumpCut=false;
            }
            else
            {
                //触发跳跃缓冲计时
                jbt = 0;
                stateMachine.jbtB = true;
                //Debug.Log("跳跃缓冲计时触发");
            }
        }
        if (input.jumpRelease)
        {
            if (nowTime - jumpWtime >= stateMachine.jumpUpTime)//超时斩断
            {
                localEventSystem.EventTigger(PlayerStateMachine.E_playEvent.jumpRelease);
            }
            else//未达到最小时间，延迟执行
            {
                canJumpCut=true;
            }

        }


        //逻辑更新直接使用帧间隔更新时间
        nowTime += Time.deltaTime;

        if (canJumpCut)
        {
            if (nowTime - jumpWtime >= stateMachine.jumpUpTime)
            {
                canJumpCut = false;
                localEventSystem.EventTigger(PlayerStateMachine.E_playEvent.jumpRelease);
            }
        }
        if (playPhyData.isGrounded)
        {
            //直接靠成员变量来传入，避免频繁GC
            stateMachine.ChangeState(stateMachine.onGround);
        }

        //移动动作响应
        playActionData.onMove= input.moveInput;
    }
}