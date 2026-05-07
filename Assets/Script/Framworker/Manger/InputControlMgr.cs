using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;
using UnityEngine.InputSystem;

public class InputControlMgr : BaseAutoMonoMgr<InputControlMgr>
{
    #region 玩家相关
    /// <summary>
    /// 运行时输入信息
    /// </summary>
    PlayerInputData inputData;
    public PlayerInputData PlayerInputData=>inputData;
    /// <summary>
    /// 配置时输入绑定
    /// </summary>
    PlayerInput input;
    /// <summary>
    /// 所控制玩家的实例
    /// </summary>
    Player player;
    /// <summary>
    /// 玩家美术资源所指向路径（名称）此处配置===========================================================
    /// </summary>
    string playerModle = "player";
    public string PlayerModleName=>playerModle;

    #endregion

    /// <summary>
    /// 主摄像机
    /// </summary>
    MainCamera mainCamera;
    #region 鼠标拖拽
    /// <summary>
    /// 当前鼠标点所在世界坐标
    /// </summary>
    Vector3 nowWordPos;
    /// <summary>
    /// 拖拽中与物体位置的偏移量
    /// </summary>
    Vector3 offset;

    bool isOnDrag;

    IobjDrag nowDragObj;

    InputActionMap player1;
    InputActionMap miniGame;
    #endregion
    /// <summary>
    /// 关联键位绑定数据
    /// </summary>
    public void BindInputAsset()
    {
        input.actions = DataAndInitMgr.Instance.asset;

        player1 = input.actions.FindActionMap("player");
        miniGame = input.actions.FindActionMap("miniGame");

        player1["Move"].performed += ctx =>
            inputData.moveInput = ctx.ReadValue<float>();

        player1["Move"].canceled += ctx =>
            inputData.moveInput = 0;

        player1["Jump"].started += ctx =>
        {
            inputData.jumpPressed = true;
            print("1111");
        };
            

        miniGame["Click"].started += MouseDown;
        miniGame["Click"].canceled += MouseUp;
        miniGame["Point"].performed += ctx =>
        {
            nowWordPos = Camera.main.ScreenToWorldPoint(ctx.ReadValue<Vector2>());
        };

        input.SwitchCurrentActionMap("player");
        InputOpenOrClose(false);

    }


    void MouseDown(InputAction.CallbackContext callback)
    {
        print("开始拖拽");
        //获取鼠标位置的世界坐标
        nowWordPos= Camera.main.ScreenToWorldPoint(miniGame["Point"].ReadValue<Vector2>());

        Collider2D hit = Physics2D.OverlapPoint(nowWordPos);

        if (hit == null) return;
        nowDragObj = hit.GetComponent<IobjDrag>();

        offset = hit.transform.position - nowWordPos;
        if (nowDragObj == null) return;
        //开始拖拽
        nowDragObj.DragIn(offset+nowWordPos);
        isOnDrag = true;

        //此处具体拖拽逻辑在update
    }

    void MouseUp(InputAction.CallbackContext callback)
    {
        isOnDrag= false;
        if (nowDragObj == null) return;
        nowDragObj.DragOut(offset + nowWordPos);
        nowDragObj= null;
    }
    /// <summary>
    /// Awake保证数据结构存在
    /// </summary>
    private void Awake()
    {
        // 获取或添加 PlayerInput 组件
        //input = GetComponent<PlayerInput>();

        if (input == null)
        {
            input = gameObject.AddComponent<PlayerInput>();
        }

        // 设置 Behavior 为 Invoke CSharp Events
        input.notificationBehavior = PlayerNotifications.InvokeCSharpEvents;

        //创建运行时输入信息
        inputData = new PlayerInputData();
    }
    
    /// <summary>
    /// 显式初始化，精准控制
    /// </summary>
    public void Init()
    {
        //玩家模型数据配置指向
        playerModle = DataConfigurationMgr.Instance.StartRes.playerModleName;

        BindInputAsset();//关联action唯一入口,关联数据管理器内已经初始化完成的数据

        #region 老方法
        //注册按键响应
        //input.onActionTriggered += (callBack) =>
        //{
        //    //确保是触发下响应
        //    if (callBack.phase == InputActionPhase.Performed)
        //    {
        //        //用于测试当前触发的action名称
        //        //Debug.Log("Action Triggered: " + callBack.action.name);
        //        switch (callBack.action.name)
        //        {
        //            case "Move":
        //                //print("移动开始");
        //                inputData.moveInput = callBack.ReadValue<float>();
        //                break;

        //            case "Jump":

        //                //print("跳跃");
        //                inputData.jumpPressed = true;//new Vector2(rb.velocity.x, jumpForce);
        //                break;
        //        }
        //    }
        //    //按键抬起逻辑，数据复原
        //    else if (callBack.phase == InputActionPhase.Canceled)
        //    {
        //        switch (callBack.action.name)
        //        {
        //            case "Move":
        //                //print("移动取消");
        //                //print(callBack.ReadValue<float>());
        //                inputData.moveInput = callBack.ReadValue<float>();
        //                break;

        //            case "Jump":
        //                //print("跳跃松开");
        //                break;
        //        }
        //    }
        //};
        #endregion

        input.SwitchCurrentActionMap("player");
        //绑定后关闭输入响应，等待正式游戏后开启
        InputOpenOrClose(false);
    }
    /// <summary>
    /// 输入控制开关
    /// </summary>
    /// <param name="isOpen"></param>
    public void InputOpenOrClose(bool isOpen)
    {
        if (isOpen)
        {
            input.currentActionMap.Enable();
        }
        else
        {
            input.currentActionMap.Disable();
        }
    }
    /// <summary>
    /// 关联玩家,由玩家主动拉取
    /// </summary>
    /// <param name="player"></param>
    public void BindPlayer(Player player)
    {
        this.player = player;
        this.player.ChangeInputAsset(inputData);
    }

    /// <summary>
    /// 设置玩家模型指向，选择角色时使用
    /// </summary>
    /// <param name="name"></param>
    public void SetPlayerModle(string name)
    {
        playerModle = name;
    }
    /// <summary>
    /// 设置主摄像机
    /// </summary>
    /// <param name="camera"></param>
    public void SetMainCamera(MainCamera camera)
    {
        mainCamera = camera;
    }
    void Update()
    {
        //摄像机与玩家均不为空
        if (mainCamera != null && player != null)
        {
            mainCamera.SetPoint(player.transform.position);

        }
        
        if(isOnDrag&&nowDragObj != null)
        {

            nowDragObj.OnDrag(offset + nowWordPos);

        }
    }
}
