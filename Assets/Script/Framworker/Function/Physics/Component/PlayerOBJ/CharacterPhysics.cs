using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PhyData;

/// <summary>
/// 角色物理组件
/// </summary>
public class CharacterPhysics : BasePhysicsEntity, ICanMove
{
    #region 玩家物理配置数据
    /// <summary>
    /// 贴墙下滑最大速度
    /// </summary>
    private float wallDownSpeed = 2f;
    /// <summary>
    /// 贴墙反跳速度倍率
    /// </summary>
    private float wallJumpV = 2f;
    /// <summary>
    /// 跳跃斩断系数
    /// </summary>
    private float jumpRelNum = 0.6f;

    public float speed = 15;    // 基础水平移速
    public float upSpeed = 15;//基础跳跃速度
    #endregion


    #region 控制流数据=================================================================
    /// <summary>
    /// 角色可执行动作
    /// </summary>
    ReadOnly_ActionData playActionData;
    //事件开关
    bool wallJump;
    // 墙跳触发时的选侧：负数为左墙，正数为右墙。物理消费前不再跟随后续输入。
    float wallJumpSide;
    bool jump;
    internal bool HasPendingJump => jump;
    //跳跃斩断按钮
    bool jumpReleaseSide;
    public float MovingDirection => playActionData.onMove;

    public float Mobility => speed;
    protected override bool MotionExecutionReady => playActionData != null;
    #endregion


    public void ClearPendingActions()
    {
        jump = wallJump = jumpReleaseSide = false;
        wallJumpSide = 0f;
    }

    protected override void OnMotionRefresh(bool relocated)
    {
        base.OnMotionRefresh(relocated);
        ClearPendingActions();
    }

    #region 瞬时触发事件
    /// <summary>
    /// 物理跳跃动作具体实现
    /// </summary>
    void Jump()
    {
        jump = true;
    }

    void JumpRelease()
    {
        jumpReleaseSide = true;
    }
    void WallJump()
    {
        wallJump = true;
        // 事件与贴墙 Update 同步。此刻 onMove 已是本帧选侧；之后输入改写不再改变这次冲量。
        wallJumpSide = playActionData.onMove < 0 ? -1f : playActionData.onMove > 0 ? 1f : 0f;
    }
    #endregion
    public void Init(ReadOnly_ActionData actionData, LocalEventSystem<PlayerStateMachine.E_playEvent> fsmEventSystem)
    {
        playActionData = actionData;
        SynchronizeMotionParticipation();
        //注册事件
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jump, Jump);
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jumpRelease, JumpRelease);
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.wallJump, WallJump);
    }

    /// <summary>
    /// 按住向下时，放开脚下或本步下落路径中命中的单向平台顶面。
    /// </summary>
    protected override bool WantsDropThroughCurrentOneWay(Collider2D support, Vector2 normal)
    {
        // 安全：动作数据还没注入时不能读上下输入。
        if (playActionData == null) return false;
        // 实际逻辑：负向超过一半才算按下。侧面和普通地面不放开。
        return playActionData.verticalInput < -0.5f
            && IsSolidOneWayTop(support, normal);
    }

    protected override float HorizontalSpeedCalculation()
    {
        // 安全：角色尚未组装动作时没有主动操作。
        if (playActionData == null) return 0f;
        // 实际逻辑：环境修饰在后面乘 nowPhyNum。
        return playActionData.onMove * speed;
    }

    protected override void PhyEventUpdate()
    {
        // 真正消费动作时才递增，碰撞顶起不触发跳跃起手。
        if (jump || wallJump) playerPhysicsData.jumpSequence++;
        //普通跳跃
        if (jump)
        {
            //受到地面影响程度
            float data = 1f + EnvironmentFrame.jumpHeightOffset;
            //当前竖直速度等于跳跃速度
            playerPhysicsData.verticalSpeed = upSpeed * data;
            //数据消费
            jump = false;
        }
        //墙跳
        if (wallJump)
        {
            //计算关键数据
            float jumpHeight;     //跳高程度
            float jumpForce;      //墙跳距离

            jumpHeight = upSpeed;
            jumpForce = wallJumpV * speed;

            playerPhysicsData.verticalSpeed = jumpHeight;
            // 离开触发时选定的墙：朝左贴墙向右跳，朝右贴墙向左跳。不读几何 onLeftWall。
            if (wallJumpSide < 0)
                AddTimeSpeed(0.2f, jumpForce);
            else if (wallJumpSide > 0)
                AddTimeSpeed(0.2f, -jumpForce);
            //消费
            wallJump = false;
            wallJumpSide = 0f;
        }
        if (jumpReleaseSide)
        {
            if (playerPhysicsData.verticalSpeed > 0)
            {
                playerPhysicsData.verticalSpeed *= jumpRelNum;
            }
            //消费
            jumpReleaseSide = false;
        }
    }

    protected override void VerticalTransmission()
    {
        if (playActionData == null) return;
        // 只有逻辑处于贴墙状态且正在下落时，才应用墙滑限速。
        if (playActionData.NowState != PlayerStateMachine.E_playerState.onWallSliding
            || playerPhysicsData.verticalSpeed >= 0) return;

        // 按动作方向选取相位 2 解析的本帧能力；松手不选择任何一侧。
        IWallSlideSurface wall = playActionData.onMove < 0 ? playphyFunData.canLeftWall
            : playActionData.onMove > 0 ? playphyFunData.canRightWall : null;
        // 安全：能力缺席、来源禁用或销毁时，不能读取乘数。
        if (!EnvironmentCapabilities.IsActive(wall)) return;

        playerPhysicsData.verticalSpeed = Mathf.Max(playerPhysicsData.verticalSpeed,
            -wallDownSpeed * wall.WallSlideMultiplier);
    }
}
