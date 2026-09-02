using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PhyData;
/// <summary>
/// 表现层组件
/// </summary>
public class PresentationLayer : MonoBehaviour
{
    /// <summary>
    /// 角色动画状态机
    /// </summary>
    Animator animator;
    /// <summary>
    /// 角色图片
    /// </summary>
    SpriteRenderer spriteRenderer;
    /// <summary>
    /// 物理速度数据引用
    /// </summary>
    ReadOnly_PlayerPhysicsData playPhyDate;
    /// <summary>
    /// 物理几何数据引用
    /// </summary>
    ReadOnly_GeometryPhysicsData playPhyDate2;
    /// <summary>
    /// 只读的可执行动作引用
    /// </summary>
    ReadOnly_ActionData playActionData;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(ReadOnly_PlayerPhysicsData phy,ReadOnly_GeometryPhysicsData phy2,ReadOnly_ActionData actionData)
    {
        playPhyDate=phy;
        playPhyDate2=phy2;
        playActionData = actionData;
    }

    // Update is called once per frame
    void Update()
    {
        //处理朝向
        if (playActionData.onMove > 0)
        {
            spriteRenderer.flipX = false;
            
        }
        else if(playActionData.onMove < 0)
        {
            spriteRenderer.flipX = true;
        }

        switch (playActionData.NowState)
        {
            case PlayerStateMachine.E_playerState.isOnGround:
                break;
            case PlayerStateMachine.E_playerState.inAir:
                break;
            case PlayerStateMachine.E_playerState.onWallSliding:
                break;
            default:
                break;
        }
    }
}
