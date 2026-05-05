using System.Collections;
using System.Collections.Generic;
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
    protected Player.PlayerData playerData;

    protected PlayerInputData input;

    protected PlayerStateMachine stateMachine;

    Animator animator;
    public void Init(Player.PlayerData playData, PlayerInputData input, PlayerStateMachine stateMachine,Animator animator)
    {
        this.playerData = playData;
        this.input = input;
        this.stateMachine = stateMachine;
        this.animator = animator;
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
        stateMachine.jumpNum = 1;
    }

    public override void Exit()
    {
        //Debug.Log("地面状态退出");
    }



    public override void Update()
    {
        if(input.jumpPressed)
        {
            //可跳跃
            //playData.rb.velocity=new Vector2(playData.rb.velocity.x,playData.upSpeed);
            playerData.verticalVelocity = playerData.upSpeed;
            stateMachine.jumpNum -= 1;
            //触发状态复原，此处已经交给状态机
        }
        //若已经不在地面，则改变状态
        if (!playerData.isGrounded)
        {
            //因为此类型状态没有成员变量，所以，直接靠成员变量来传入，避免频繁GC
            stateMachine.ChangeState(stateMachine.inAir);
        }
        //水平速度的持续更新
        playerData.HorizontalSpeed = input.moveInput * playerData.speed;

    }
}

public class IsInAir : BasePlayerState
{
    /// <summary>
    /// 当前时间
    /// </summary>
    float nowTime;
    /// <summary>
    /// 跳跃缓冲时间
    /// </summary>
    float maxTime=0.1f;
    public override void Enter()
    {
        //Debug.Log("空中状态进入");
        nowTime = maxTime;
    }

    public override void Exit( )
    {
        //Debug.Log("空中状态退出");
        nowTime = 0;
    }


    public override void Update()
    {
        if (nowTime > 0&&stateMachine.jumpNum>0)
        {
            //可跳跃
            if (input.jumpPressed)
            {
                playerData.verticalVelocity = playerData.upSpeed;
                stateMachine.jumpNum -= 1;
                nowTime = 0;
            }
            //此处放在了物理更新中，时间也需修改
            //后续若有需求，需要分离逻辑更新和物理更新=================================
            nowTime -= Time.fixedDeltaTime;

        }
        if (playerData.isGrounded)
        {
            //因为此类型状态没有成员变量，所以，直接靠成员变量来传入，避免频繁GC
            stateMachine.ChangeState(stateMachine.onGround);
        }
        //水平速度的持续更新
        playerData.HorizontalSpeed = input.moveInput * playerData.speed;
    }
}