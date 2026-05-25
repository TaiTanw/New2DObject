using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using static Player;

/// <summary>
/// 角色物理组件
/// </summary>
public class CharacterPhysics : MonoBehaviour
{
    /// <summary>
    /// 实时物理数据
    /// </summary>
    public class PlayerPhysicsData
    {
        public float horizontalSpeed;   //当前自身水平速度
        public float verticalSpeed;  //当前自身竖直速度
        /// <summary>
        /// 当前玩家所属平台
        /// </summary>
        public Taijie nowtaijie;
        public bool isGrounded;     // 物理检测
        public bool onLeftWall;
        public bool onRightWall;
    }
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (groundV == null) return;
        if(leftV == null) return ;
        if(rightV == null) return ;
        // 设置颜色：绿色半透明，便于观察
        Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.5f);

        // 绘制检测区域的线框矩形（位置、大小、旋转）
        Gizmos.DrawWireCube(groundV.position, size);
        Gizmos.DrawWireCube(leftV.position, size1);
        Gizmos.DrawWireCube(rightV.position, size1);
    }
#endif
    //必要组件
    Rigidbody2D rb; //刚体

    //玩家状态机内部事件系统引用
    LocalEventSystem<PlayerStateMachine.E_playEvent> fsmEventSystem;

    BoxCollider2D boxCollider;

    /// <summary>
    /// 碰撞检测范围矩形宽高
    /// </summary>
    private Vector2 size;
    private Vector2 size1;

    /// 物理配置数据
    /// </summary>
    [SerializeField]
    private SO_CPhysics cPhysics;
    /// <summary>
    /// 地面检测中心（外部拖拽关联
    /// </summary>
    [SerializeField]
    private Transform groundV;
    /// <summary>
    /// 左墙检测
    /// </summary>
    [SerializeField]
    private Transform leftV;
    /// <summary>
    /// 右墙检测
    /// </summary>
    [SerializeField]
    private Transform rightV;
    /// <summary>
    /// 玩家可执行动作
    /// </summary>
    ActionData playActionData;

    //实时数据
    PlayerPhysicsData playerPhysicsData;
    public PlayerPhysicsData PlayPhysicsData => playerPhysicsData;

    private bool isWallSliding;

    private void Awake()
    {
        if (cPhysics == null)
        {
            print("SO_玩家物理配置数据为空，请拖拽引用");
        }
        //必要组件关联
        rb = GetComponent<Rigidbody2D>();
        boxCollider = GetComponent<BoxCollider2D>();
        //不使用刚体的重力
        rb.gravityScale = 0;
        //地面检测盒范围
        size = cPhysics.boxCastH;
        size1 = cPhysics.boxCastV;
        playerPhysicsData = new PlayerPhysicsData();

    }
    private void Start()
    {
        //简单的位置设置，后续优化
        transform.position = new Vector3(-3, -1, -1);
    }

    private void OnEnable()
    {
        //物理更新时序为1层
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(FixFun, cPhysics.phyMask);
    }

    public void Init(ActionData actionData, LocalEventSystem<PlayerStateMachine.E_playEvent> fsmEventSystem)
    {
        playActionData = actionData;
        this.fsmEventSystem = fsmEventSystem;
        //注册事件
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jump,Jump);
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jumpRelease,JumpRelease);
    }
    #region 瞬时触发事件
    /// <summary>
    /// 物理跳跃动作具体实现
    /// </summary>
    void Jump()
    {
        //print("物理跳跃触发");
        //当前竖直速度等于跳跃速度
        playerPhysicsData.verticalSpeed = cPhysics.upSpeed;
    }

    void JumpRelease()
    {
        //print("跳跃斩断1111111111");

        if (playerPhysicsData.verticalSpeed > 0)
        {
            playerPhysicsData.verticalSpeed *= 0.7f;
        }
    }
    #endregion
    /// <summary>
    /// 物理更新，传入外部控制时序
    /// </summary>
    void FixFun()
    {
        //数据读取
        float horizontalSpeed=playerPhysicsData.horizontalSpeed;
        float verticalSpeed=playerPhysicsData.verticalSpeed;
        bool isGrounded=playerPhysicsData.isGrounded;
        Taijie nowtaijie=playerPhysicsData.nowtaijie;


        //检测是否在地面
        RaycastHit2D hit = Physics2D.BoxCast(groundV.position, size, 0, Vector2.down, 0f, cPhysics.groundLayer);
        //检测左右靠墙
        playerPhysicsData.onLeftWall= Physics2D.BoxCast(leftV.position, size1, 0, Vector2.left, 0f, cPhysics.wallLayer);
        playerPhysicsData.onRightWall= Physics2D.BoxCast(rightV.position, size1, 0, Vector2.right, 0f, cPhysics.wallLayer);

        if(playActionData.onMove<0&& playerPhysicsData.onLeftWall|| playActionData.onMove > 0 && playerPhysicsData.onRightWall)
        {
            horizontalSpeed = 0;
            isWallSliding = true;
        }
        else
        {
            isWallSliding = false;
            //处理移动
            horizontalSpeed = playActionData.onMove * cPhysics.speed;   //横向速度变化(赋值处理
        }


        //状态重置，避免缓存影响判断
        isGrounded = false;
        nowtaijie = null;

        if (hit.collider != null)
        {
            // ========判断是否在平台上方
            if (Vector2.Dot(hit.normal, Vector2.up) > 0.7f)
            {
                isGrounded = true;
                nowtaijie = hit.collider.GetComponent<Taijie>();
            }
        }
        // 重力
        if (!isGrounded)
        {
            verticalSpeed += cPhysics.gravity * Time.fixedDeltaTime;     //纵向速度改变（增减处理
        }
        else if (verticalSpeed < 0)
        {
            verticalSpeed = 0;
        }
        if (isWallSliding && verticalSpeed < 0)
        {
            verticalSpeed =
                Mathf.Max(
                    verticalSpeed,
                    -cPhysics.wallDownSpeed);
        }

        //计算当前速度向量与移动距离
        Vector2 velocity = new Vector2(horizontalSpeed, verticalSpeed);

        Vector2 moveDelta = velocity * Time.fixedDeltaTime;             //计算玩家相对位移

        //计算平台补偿位移
        Vector2 platformDelta = Vector2.zero;

        if (nowtaijie != null && isGrounded)
        {
            platformDelta = nowtaijie.delta;
            //此处加斜率补正
            //================
            moveDelta = new Vector2(0, verticalSpeed * Time.fixedDeltaTime);

            float moveAmount = horizontalSpeed * Time.fixedDeltaTime;

            Vector2 tangent = new Vector2(hit.normal.y, -hit.normal.x);
            //判断方向是否一致
            if (Mathf.Sign(tangent.x) != Mathf.Sign(horizontalSpeed))
                tangent *= -1;

            //Debug.Log(tangent);
            //此处表示，沿斜坡移动的速度和水平速度一致
            Vector2 slopeMove = tangent.normalized * Mathf.Abs(moveAmount);

            moveDelta += slopeMove;
        }

        //  一次性统一移动
        rb.MovePosition(rb.position + moveDelta + platformDelta);
        //数据写回
        playerPhysicsData.horizontalSpeed=horizontalSpeed;
        playerPhysicsData.verticalSpeed=verticalSpeed;
        playerPhysicsData.isGrounded=isGrounded;
        playerPhysicsData.nowtaijie = nowtaijie; 
    }

    private void OnDisable()
    {
        //事件注销
        if (!MonoPublicMgr.IsQuitting)
        {
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(FixFun, cPhysics.phyMask);
        }
    }
}
