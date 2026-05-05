using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 角色物理组件
/// </summary>
public class CharacterPhysics : MonoBehaviour
{
    Rigidbody2D rb; //刚体

    BoxCollider2D boxCollider;

    /// <summary>
    /// 碰撞检测范围矩形宽高
    /// </summary>
    private Vector2 size;

    /// <summary>
    /// 当前玩家所属平台
    /// </summary>
    Taijie nowtaijie;
    /// <summary>
    /// 物理配置数据
    /// </summary>
    [SerializeField]
    private SO_CPhysics cPhysics;
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
