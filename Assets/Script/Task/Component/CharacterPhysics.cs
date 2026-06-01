using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using Physics.Player;

/// <summary>
/// 角色物理组件
/// </summary>
public class CharacterPhysics : MonoBehaviour
{
    /// <summary>
    /// 实时物理数据类型
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
        public bool onLeftWall; //左右墙布尔，后续可替换为墙接口，表示受墙的影响因素 
        public bool onRightWall;
        /// <summary>
        /// 在贴墙
        /// </summary>
        public bool isOnWall=>onLeftWall||onRightWall;
        /// <summary>
        /// 响应计时器
        /// </summary>
        public float responseTimer1;
        /// <summary>
        /// 技能速度叠加(墙跳，冲刺等等
        /// </summary>
        public float speedStack;
    }

    /// <summary>
    /// 编辑器画图显示碰撞检测范围
    /// </summary>
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
    #region 必要组件
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
    public SO_CPhysics CPhysics=>cPhysics;

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

    //实时数据(外部修改时传入（如物理命令
    PlayerPhysicsData playerPhysicsData;
    public PlayerPhysicsData PlayPhysicsData => playerPhysicsData;//外部只读时传入
    #endregion

    #region 控制流数据
    /// <summary>
    /// 本帧物理事件触发指令队列
    /// </summary>
    Queue<IPhysicsCommand> commandQueue = new();
    /// <summary>
    /// 命令调度实例
    /// </summary>
    CommandScheduling commandScheduling;
    #endregion

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
        //检测盒范围
        size = cPhysics.boxCastH;
        size1 = cPhysics.boxCastV;
        //实时物理状态信息初始化
        playerPhysicsData = new PlayerPhysicsData();
        //命令调度器初始化（后于信息类初始化
        commandScheduling = new(playerPhysicsData, cPhysics);

    }
    private void Start()
    {
        //简单的位置设置，后续优化=======================================================================================================
        transform.position = new Vector3(-3, -1, -1);
    }

    private void OnEnable()
    {
        //物理更新时序为1层
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(FixFun, cPhysics.phyMask);
    }
    #region 瞬时触发事件
    /// <summary>
    /// 物理跳跃动作具体实现
    /// </summary>
    void Jump()
    {
        //print("物理跳跃触发");
        
        commandQueue.Enqueue(commandScheduling.CommandToQueue<JumpCommand>());
    }

    void JumpRelease()
    {
        //print("跳跃斩断1111111111");

        if (playerPhysicsData.verticalSpeed > 0)
        {
            playerPhysicsData.verticalSpeed *= 0.7f;
        }
    }
    void WallJump()
    {
        //触发事件，命令入队
        commandQueue.Enqueue(commandScheduling.CommandToQueue<WallJumpCommand>());
        //print("墙跳命令触发");
    }
    #endregion
    public void Init(ActionData actionData, LocalEventSystem<PlayerStateMachine.E_playEvent> fsmEventSystem)
    {
        playActionData = actionData;
        this.fsmEventSystem = fsmEventSystem;
        //注册事件
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jump,Jump);
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.jumpRelease,JumpRelease);
        fsmEventSystem.AddEventListener(PlayerStateMachine.E_playEvent.wallJump,WallJump);
    }
  
    /// <summary>
    /// 物理更新，传入外部控制时序
    /// </summary>
    void FixFun()
    {
        //地面检测
        RaycastHit2D hit = Physics2D.BoxCast(groundV.position, size, 0, Vector2.down, 0f, cPhysics.groundLayer);
        if (hit.collider != null && Vector2.Dot(hit.normal, Vector2.up) > 0.7f)
        {
            // ========判断是否在平台上方
            playerPhysicsData.isGrounded = true;
            playerPhysicsData.nowtaijie = hit.collider.GetComponent<Taijie>();

        }
        else
        {
            //状态重置，避免缓存影响判断
            playerPhysicsData.isGrounded = false;
            playerPhysicsData.nowtaijie = null;
        }
        //检测左右靠墙
        playerPhysicsData.onLeftWall= Physics2D.BoxCast(leftV.position, size1, 0, Vector2.left, 0f, cPhysics.wallLayer);
        playerPhysicsData.onRightWall= Physics2D.BoxCast(rightV.position, size1, 0, Vector2.right, 0f, cPhysics.wallLayer);

        //处理基础移动（赋值操作，最先）
        playerPhysicsData.horizontalSpeed = playActionData.onMove * cPhysics.speed;

        //出队操作，触发命令,获取所有动作触发数据（赋值和叠加操作，其次）
        while (commandQueue.Count > 0)
        {
            //按序执行命令
            commandQueue.Dequeue().Execute();
        }
        //数值合并，避免命令内处理时造成重复叠加
        //处理冲刺
        playerPhysicsData.horizontalSpeed +=playerPhysicsData.speedStack;
        //计时冲刺速度
        playerPhysicsData.responseTimer1-=Time.fixedDeltaTime;
        //当超过响应时间则速度恢复
        if (playerPhysicsData.responseTimer1 < 0)
            playerPhysicsData.speedStack = 0f;

        // 重力
        if (!playerPhysicsData.isGrounded)
        {
            playerPhysicsData.verticalSpeed += cPhysics.gravity * Time.fixedDeltaTime;     //纵向速度改变（增减处理
        }
        else if (playerPhysicsData.verticalSpeed < 0)
        {
            playerPhysicsData.verticalSpeed = 0;
        }
        //贴墙下滑
        if (playActionData.isWallSliding && playerPhysicsData.verticalSpeed < 0)
        {
            playerPhysicsData.verticalSpeed =
                Mathf.Max(
                    playerPhysicsData.verticalSpeed,
                    -cPhysics.wallDownSpeed);
        }

        //计算当前速度向量与移动距离
        Vector2 velocity = new Vector2(playerPhysicsData.horizontalSpeed, playerPhysicsData.verticalSpeed);

        Vector2 moveDelta = velocity * Time.fixedDeltaTime;             //计算玩家相对位移

        //计算平台补偿位移
        Vector2 platformDelta = Vector2.zero;

        if (playerPhysicsData.nowtaijie != null && playerPhysicsData.isGrounded)
        {
            platformDelta = playerPhysicsData.nowtaijie.delta;
            //此处加斜率补正
            //================
            moveDelta = new Vector2(0, playerPhysicsData.verticalSpeed * Time.fixedDeltaTime);

            float moveAmount = playerPhysicsData.horizontalSpeed * Time.fixedDeltaTime;

            Vector2 tangent = new Vector2(hit.normal.y, -hit.normal.x);
            //判断方向是否一致
            if (Mathf.Sign(tangent.x) != Mathf.Sign(playerPhysicsData.horizontalSpeed))
                tangent *= -1;

            //Debug.Log(tangent);
            //此处表示，沿斜坡移动的速度和水平速度一致
            Vector2 slopeMove = tangent.normalized * Mathf.Abs(moveAmount);

            moveDelta += slopeMove;
        }

        Vector2 wordDelta = moveDelta + platformDelta;      //计算世界绝对位移
        //计算世界物理运动受限
        if (wordDelta.x < 0 && playerPhysicsData.onLeftWall || wordDelta.x > 0 && playerPhysicsData.onRightWall)
        {
            wordDelta.x = 0;
        }
        //  一次性统一移动
        rb.MovePosition(rb.position +wordDelta);

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
