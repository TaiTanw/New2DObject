using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;
using static UnityEngine.UI.Image;
using UnityEngine.UIElements;

public class Player : MonoBehaviour
{
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (groundV == null) return;

        // 设置颜色：绿色半透明，便于观察
        Gizmos.color = new Color(0, 1, 0, 0.5f);

        // 绘制检测区域的线框矩形（位置、大小、旋转）
        Gizmos.DrawWireCube(groundV.position, size);
    }
#endif
    /// <summary>
    /// 角色图片
    /// </summary>
    public SpriteRenderer spriteRenderer;
    /// <summary>
    /// 玩家数据
    /// </summary>
    public class PlayerData
    {
        public bool isGrounded;     // 物理检测
        public float speed=15;    // 基础水平移速
        public float upSpeed = 15;//基础跳跃速度

        public float HorizontalSpeed;   //当前水平速度
        public float verticalVelocity;  //当前竖直速度
        public float gravity = -35f;    //重力速度
        public PlayerData()
        {
            isGrounded=true;
        }
    }

    /// <summary>
    /// 玩家当前数据
    /// </summary>
    PlayerData data;
    /// <summary>
    /// 玩家当前输入数据,初始化交给外部
    /// </summary>
    PlayerInputData inputData;

    #region 物理相关
    public Rigidbody2D rb; //刚体
    /// <summary>
    /// 检测中心
    /// </summary>
    [SerializeField]
    private Transform groundV;

    /// <summary>
    /// 碰撞显示范围矩形宽高
    /// </summary>
    private Vector2 size = new Vector2(0.75f, 0.1f);
    /// <summary>
    /// 检测层级
    /// </summary>
    [SerializeField]
    private LayerMask groundLayer;
    /// <summary>
    /// 当前玩家所属平台
    /// </summary>
    private Taijie nowtaijie;

    //碰撞器缓存
    RaycastHit2D hit;
    #endregion

    /// <summary>
    /// 玩家状态机对象
    /// </summary>
    PlayerStateMachine fsm;
    /// <summary>
    /// 动画状态机
    /// </summary>
    Animator animator;
    void Awake()
    {
        //玩家状态信息初始化
        data = new PlayerData();
        //玩家输入信息初始化
        //inputData =new PlayerInputData();此处会导致数据引用错误，所以初始化交给外部的输入管理系统
        //玩家主动拉取输入控制权限，调用方法保证唯一玩家控制权,且便于控制时序，保证先于状态机初始化
        InputControlMgr.Instance.BindPlayer(this);

        animator=GetComponent<Animator>();
        //玩家状态机初始化
        fsm = new PlayerStateMachine();
        fsm.InitData(data, inputData, animator);
        //刚体初始化
        rb = gameObject.GetComponent<Rigidbody2D>();
        //不使用刚体的重力
        rb.gravityScale = 0;
        //物理更新时序为1层
        MonoPublicMgr.Instance.AddPhysicalTimingUpdate(FixFun, 1);
        //spriteRenderer = GetComponent<SpriteRenderer>();
        //简单的位置设置，后续优化
        transform.position = new Vector3(-3, -1, -1);

    }
    /// <summary>
    /// 注入玩家按键数据行为
    /// </summary>
    /// <param name="data"></param>
    public void ChangeInputAsset(PlayerInputData data)
    {
        inputData = data;
    }
    //float time;
    void Update()
    {
        //速度自检
        //time += Time.deltaTime;
        //if (time > 3)
        //{
        //    print("垂直速度"+data.verticalVelocity);
        //    print("水平速度"+data.HorizontalSpeed);
        //    time = 0;
        //}
    }

    private void LateUpdate()
    {
        
    }

    /// <summary>
    /// 物理帧更新
    /// </summary>
    void FixFun()
    {
        //玩家状态机更新，经过输入响应后获取瞬间的玩家数据
        fsm.Update(data, inputData);

        //col = Physics2D.OverlapBox(groundV.position, size, 0, groundLayer);
        //检测是否在地面
        hit = Physics2D.BoxCast(groundV.position, size, 0, Vector2.down, 0f, groundLayer);

        //状态重置，避免缓存影响判断
        data.isGrounded = false;
        nowtaijie = null;

        if (hit.collider != null)
        {
            // ========判断是否在平台上方
            //if (groundV.position.y >= col.bounds.max.y - 0.05f)
            if (Vector2.Dot(hit.normal, Vector2.up) > 0.7f)
            {
                data.isGrounded = true;
                nowtaijie = hit.collider.GetComponent<Taijie>();
            }
        }

        // 水平速度判断
        if(data.HorizontalSpeed < 0)
            spriteRenderer.flipX = true;
        if (data.HorizontalSpeed > 0)
            spriteRenderer.flipX = false;
        // 重力
        if (!data.isGrounded)
        {
            data.verticalVelocity += data.gravity * Time.fixedDeltaTime;
        }
        else if (data.verticalVelocity < 0)
        {
            data.verticalVelocity = 0;
        }

        Vector2 velocity = new Vector2(data.HorizontalSpeed, data.verticalVelocity);
        Vector2 moveDelta = velocity * Time.fixedDeltaTime;

        //  平台补偿
        Vector2 platformDelta = Vector2.zero;

        if (nowtaijie != null && data.isGrounded)
        {
            platformDelta = nowtaijie.delta;
            //此处加斜率补正
            //================
            moveDelta = new Vector2(0, data.verticalVelocity * Time.fixedDeltaTime);

            float moveAmount = data.HorizontalSpeed * Time.fixedDeltaTime;

            Vector2 tangent = new Vector2(hit.normal.y, -hit.normal.x);
            //判断方向是否一致
            if (Mathf.Sign(tangent.x) != Mathf.Sign(data.HorizontalSpeed))
                tangent *= -1;

            //Debug.Log(tangent);
            //此处表示，沿斜坡移动的速度和水平速度一致
            Vector2 slopeMove = tangent.normalized * Mathf.Abs(moveAmount);

            moveDelta += slopeMove;
        }


        //  一次性统一移动
        rb.MovePosition(rb.position + moveDelta + platformDelta);
    }

    

    private void OnDestroy()
    {
        if (!MonoPublicMgr.IsQuitting)
        {
            MonoPublicMgr.Instance.RemovePhysicalTimingUpdate(FixFun, 1);

        }
    }
}

/// <summary>
/// 玩家状态机
/// </summary>
public class PlayerStateMachine
{
    /// <summary>
    /// 当前状态
    /// </summary>
    public IBehavioralState onState;

    public IsOnGround onGround;
    public IsInAir inAir;
    /// <summary>
    /// 可跳跃次数
    /// </summary>
    public int jumpNum=1;
    public PlayerStateMachine()
    {
        onGround = new IsOnGround();
        inAir = new IsInAir();
        //当前状态信息初始化
        onState = onGround;
    }

    public void InitData(Player.PlayerData playData, PlayerInputData input, Animator animator)
    {
        onGround.Init(playData, input, this, animator);
        inAir.Init(playData,input, this, animator);
    }

    /// <summary>
    /// 状态改变
    /// </summary>
    public void ChangeState(IBehavioralState state)
    {
        onState.Exit();
        onState=state;
        onState.Enter();
    }



    /// <summary>
    /// 供玩家mono执行的玩家更新逻辑
    /// </summary>
    /// <param name="playData">玩家当前状态</param>
    /// <param name="input">当前输入信息</param>
    /// <param name="stateMachine">玩家自身状态机</param>
    public void Update(Player.PlayerData playData,PlayerInputData input)
    {
        //传入执行，状态类负责实际功能
        onState.Update();
        //效果已经触发，则关闭，防止持续跳跃
        input.jumpPressed = false;//触发类型按键全部统一由状态机复原
    }
}
