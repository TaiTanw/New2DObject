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
    //玩家物理状态/弃用
    //protected Player.PlayerData playerData;
    //玩家物理状态/只读
    protected CharacterPhysics playPhyData;
    //原始输入/只读
    protected PlayerInputData input;
    //过滤后的动作输出/读写
    protected ActionData playActionData;
    //依附的逻辑状态机/调用
    protected PlayerStateMachine stateMachine;

    public void Init(CharacterPhysics playPhyData, PlayerInputData input, PlayerStateMachine stateMachine,ActionData playActionData)
    {
        //this.playerData = playData;
        this.playPhyData = playPhyData;
        this.input = input;
        this.stateMachine = stateMachine;
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
            //可跳跃//重构前直接改物理
            //playerData.verticalVelocity = playerData.upSpeed;
            //动作响应
            playActionData.onJump = true;
            stateMachine.jumpNum -= 1;
            //触发状态复原，此处已经交给状态机
        }
        //若已经不在地面，则改变状态
        if (!playPhyData.IsOnGround)
        {
            //直接靠成员变量来传入，避免频繁GC
            stateMachine.ChangeState(stateMachine.inAir);
        }
        //水平速度的持续更新（重构前
        //playerData.HorizontalSpeed = input.moveInput * playerData.speed;
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
    /// 土狼时间
    /// </summary>
    float coyoteTime=0.1f;

    /// <summary>
    /// 跳跃缓冲时间
    /// </summary>
    float jumpBuffer = 0.2f;
    /// <summary>
    /// 跳跃缓冲计时器
    /// </summary>
    float jbt;
    /// <summary>
    /// 跳跃缓冲计时器开关
    /// </summary>
    bool jbtB;

    public override void Enter()
    {
        //Debug.Log("空中状态进入");
    }

    public override void Exit( )
    {
        Debug.Log("空中状态退出");
        nowTime = 0;
        jbt = 0;
    }


    public override void Update()
    {
        if (jbtB)
        {
            jbt += Time.deltaTime;

            if (jbt > jumpBuffer)
            {
                jbtB = false; // 超时自动关闭
            }
        }
        if (input.jumpPressed&& stateMachine.jumpNum <= 0)
        {
            jbt = 0;
            jbtB = true;
            Debug.Log("跳跃缓冲计时触发");
        }
 
        if (nowTime <=coyoteTime&&stateMachine.jumpNum>0)
        {
            //可跳跃
            if (input.jumpPressed)
            {
                //旧版
                //playerData.verticalVelocity = playerData.upSpeed;
                playActionData.onJump= true;
                stateMachine.jumpNum -= 1;
            }
        }
       

        //逻辑更新直接使用帧间隔更时间
        nowTime += Time.deltaTime;

        if (playPhyData.IsOnGround)
        {
            if (jbtB && jbt <= jumpBuffer)
            {
                playActionData.onJump = true;
                stateMachine.jumpNum -= 1;

                Debug.Log("消费缓冲");
                jbtB = false;
                jbt = 0;
            }
            //直接靠成员变量来传入，避免频繁GC
            stateMachine.ChangeState(stateMachine.onGround);
        }
        //水平速度的持续更新（重构前
        //playerData.HorizontalSpeed = input.moveInput * playerData.speed;
        //移动动作响应
        playActionData.onMove= input.moveInput;
    }
}