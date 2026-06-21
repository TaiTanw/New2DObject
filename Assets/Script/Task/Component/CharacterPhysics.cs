using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using PhyData;

/// <summary>
/// 角色物理组件
/// </summary>
public class CharacterPhysics : BasePhysicsEntity
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
    #endregion


    #region 控制流数据=================================================================
    //玩家状态机内部事件系统引用(一次性注册事件（因为生命周期和局部事件系统一致，所以暂时不用考虑注销物体（随物体删除一并销毁
    //LocalEventSystem<PlayerStateMachine.E_playEvent> fsmEventSystem;
    /// <summary>
    /// 玩家可执行动作
    /// </summary>
    ReadOnly_ActionData playActionData;
    //事件开关
    bool wallJump;
    bool jump;
    #endregion


    private void Start()
    {
        //简单的位置设置，后续优化=======================================================================================================
        transform.position = new Vector3(-3, -1, -1);
    }

    #region 瞬时触发事件
    /// <summary>
    /// 物理跳跃动作具体实现
    /// </summary>
    void Jump()
    {
        //print("物理跳跃触发");
        jump = true;
    }

    void JumpRelease()
    {
        //print("跳跃斩断1111111111");

        if (playerPhysicsData.verticalSpeed > 0)
        {
            playerPhysicsData.verticalSpeed *= jumpRelNum;
        }
    }
    void WallJump()
    {
        wallJump = true;
    }
    #endregion
    public void Init(ReadOnly_ActionData actionData, LocalEventSystem<PlayerStateMachine.E_playEvent> fsmEventSystem)
    {
        playActionData = actionData;
        //this.fsmEventSystem = fsmEventSystem;
        //注册事件
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jump, Jump);
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jumpRelease, JumpRelease);
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.wallJump, WallJump);
    }

    protected override float HorizontalSpeedCalculation()
    {
        return playActionData.onMove * cPhysics.speed;
    }

    protected override void PhyEventUpdate()
    {
        //playActionData.onMove=1;
        //普通跳跃
        if (jump)
        {
            //受到地面影响程度
            float data = 1f;
            if (playerPhysicsData.nowtaijie != null)
            {
                data+=playerPhysicsData.nowtaijie.JumpHeightNum;
            }
            //当前竖直速度等于跳跃速度
            playerPhysicsData.verticalSpeed = cPhysics.upSpeed*data;
            //数据消费
            jump = false;
        }
        //墙跳
        if (wallJump)
        {
            //计算关键数据
            float jumpHeight;     //跳高程度
            float jumpForce;      //墙跳距离

            jumpHeight = cPhysics.upSpeed;
            jumpForce = wallJumpV * cPhysics.speed;

            playerPhysicsData.verticalSpeed = jumpHeight;
            //速度叠加
            if (playerPhysicsData.onLeftWall)
            {

                AddSpeed(0.2f, new Vector2(jumpForce, 0));
            }
            else
            {
                AddSpeed(0.2f, new Vector2(-jumpForce, 0));
            }
            //消费
            wallJump = false;
        }
    }

    protected override void VerticalTransmission()
    {
        //在贴墙以及在下落
        //由于帧更新和物理更新时序的差异性,此处还是需要对nowWall判空
        if (playActionData.NowState==PlayerStateMachine.E_playerState.onWallSliding && playerPhysicsData.verticalSpeed < 0 && playerPhysicsData.nowWall)
        {
            playerPhysicsData.verticalSpeed =Mathf.Max(playerPhysicsData.verticalSpeed,-wallDownSpeed/playerPhysicsData.nowWall.WallFRICTION);
        }
    }
}
