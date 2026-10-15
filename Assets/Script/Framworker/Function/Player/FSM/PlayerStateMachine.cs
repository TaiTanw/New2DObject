using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using PhyData;

/// <summary>
/// 玩家状态机
/// </summary>
public class PlayerStateMachine
{
    /// <summary>
    /// 触发性动作枚举（用于局部事件系统
    /// </summary>
    public enum E_playEvent
    {
        jump,
        jumpRelease,//跳跃松开（处理长按跳高）
        wallJump,//墙跳
    }
    /// <summary>
    /// 状态机内部状态枚举（显式声明避免新增状态而外部未处理新逻辑分支
    /// </summary>
    public enum E_playerState
    {
        isOnGround,
        inAir,
        onWallSliding,
        swimming,
        surfaceSwimming
    }

    LocalEventSystem<E_playEvent> eventSystem;
    /// <summary>
    /// 内部事件系统，由状态机保持唯一实例
    /// </summary>
    public LocalEventSystem<E_playEvent> EventSystem=>eventSystem;

    /// <summary>
    /// 当前状态实例
    /// </summary>
    private IBehavioralState onState;
    /// <summary>
    /// 当前动作
    /// </summary>
    MovementData nowMovement;

    //私有状态类实例，外部通过传递枚举转换指定状态
     IsOnGround onGround;
     IsInAir inAir;
     OnWallSliding onWallSliding;
    private readonly FourWayMovementState fourWay = new FourWayMovementState();
    private readonly SurfaceSwimmingState surfaceSwimming = new SurfaceSwimmingState();
    private ReadOnly_GeometryPhysicsData geometry;
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
        onWallSliding = new OnWallSliding();
        eventSystem = new LocalEventSystem<E_playEvent>();
        //当前状态信息初始化
        onState = onGround;

        jumpCount = jumpNum;
    }

    public void InitData(ReadOnly_GeometryPhysicsData playData, PlayerInputData input, MovementData actionData)
    {
        nowMovement = actionData;
        geometry = playData;
        onGround.Init(playData, input, this, actionData);
        inAir.Init(playData, input, this, actionData);
        onWallSliding.Init(playData, input, this, actionData);
        fourWay.Init(playData, input, this, actionData);
        surfaceSwimming.Init(playData, input, this, actionData);
        onState.Enter();
    }

    /// <summary>
    /// 状态改变
    /// </summary>
    public void ChangeState(E_playerState state)
    {
        nowMovement.nowState = state;
        IBehavioralState nowState;
        switch (state)
        {
            case E_playerState.isOnGround:
                nowState=onGround;
                break;
            case E_playerState.inAir:
                nowState=inAir;
                break;
            case E_playerState.onWallSliding:
                nowState=onWallSliding;
                break;
            case E_playerState.swimming:
                nowState = fourWay;
                break;
            case E_playerState.surfaceSwimming:
                nowState = surfaceSwimming;
                break;
            default:
                nowState=null;
                Debug.LogError("有多余枚举未处理状态的逻辑分支");
                break;
        }
        onState.Exit();
        onState = nowState;
        onState.Enter();
    }

    /// <summary>
    /// 供玩家mono执行的玩家更新逻辑
    /// </summary>
    /// <param name="playData">玩家当前状态</param>
    /// <param name="input">当前输入信息</param>
    /// <param name="mode">本帧按水体事实解析的四向原因</param>
    /// <param name="jumpPending">物理层是否还有未消费的跳跃</param>
    /// <param name="totalVerticalSpeed">实体当前实际竖直速度</param>
    public void Update(PlayerInputData input, FourWayMode mode = FourWayMode.None,
        bool jumpPending = false, float totalVerticalSpeed = 0f)
    {
        // 竖直方向透传给执行层；四向状态会在自身更新里改写为归一化方向。
        nowMovement.verticalInput = input.verticalInput;
        // 跃出边界：冲量消费前和上升阶段不被水面重新捕获；完整淹没立即交回游泳。
        if (nowMovement.surfaceJumpInProgress)
        {
            if (mode == FourWayMode.Swimming || (!jumpPending && totalVerticalSpeed <= 0f))
                nowMovement.surfaceJumpInProgress = false;
            else if (mode == FourWayMode.SurfaceSwimming) mode = FourWayMode.None;
        }
        SelectFourWayState(mode);
        //传入执行，状态类负责实际功能
        onState.Update();
        //效果已经触发，则关闭，防止持续响应
        input.jumpPressed = false;//触发类型按键全部统一由状态机复原
        input.jumpRelease = false;
        //Debug.Log(nowMovement.onMove);
    }

    private void SelectFourWayState(FourWayMode mode)
    {
        // 已解析的四向原因优先于着地和墙滑。
        if (mode != FourWayMode.None)
        {
            E_playerState desired = mode == FourWayMode.SurfaceSwimming ? E_playerState.surfaceSwimming : E_playerState.swimming;
            if (nowMovement.nowState != desired) ChangeState(desired);
            return;
        }
        // 离水按当前几何恢复，不恢复入水前的状态。
        if (nowMovement.nowState == E_playerState.swimming || nowMovement.nowState == E_playerState.surfaceSwimming)
            ChangeState(geometry.isGrounded ? E_playerState.isOnGround : E_playerState.inAir);
    }

    /// <summary>
    /// 清除跳跃缓冲与跳跃斩断等待；重新进入水面行为时连同跃出保护一起重置
    /// </summary>
    internal void ResetMotionActions()
    {
        jbtB = false;
        inAir.ClearPendingInput();
        if (nowMovement != null) nowMovement.surfaceJumpInProgress = false;
    }

    internal void BeginSurfaceJump(bool released)
    {
        // 先切空中再发原跳跃事件，执行层沿已有冲量和短跳规则消费。
        ChangeState(E_playerState.inAir);
        inAir.BeginJumpReleaseWindow(released);
        nowMovement.surfaceJumpInProgress = true;
        groundJump = jbtB = false;
        eventSystem.EventTigger(E_playEvent.jump);
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
