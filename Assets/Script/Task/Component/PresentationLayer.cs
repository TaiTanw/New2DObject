using System.Collections;
using System.Collections.Generic;
using UnityEngine;
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
    /// 物理数据引用
    /// </summary>
    CharacterPhysics.PlayerPhysicsData playPhyDate;
    /// <summary>
    /// 可执行动作引用
    /// </summary>
    ActionData playActionData;
    private void Awake()
    {
        animator = GetComponent<Animator>();
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    public void Init(CharacterPhysics.PlayerPhysicsData phy,ActionData actionData)
    {
        playPhyDate=phy;
        playActionData = actionData;
    }

    // Update is called once per frame
    void Update()
    {
        if (playActionData.onMove > 0)
        {
            spriteRenderer.flipX = false;
                
        }
        else if(playActionData.onMove < 0)
        {
            spriteRenderer.flipX = true;
        }
    }
}
