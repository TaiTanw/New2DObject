using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
public class PlayerInputData
{
    public float moveInput;    //水平输入
    public bool jumpPressed;   // 这一帧是否按下
}
public class InputControlMgr : BaseAutoMonoMgr<InputControlMgr>
{
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
    /// <summary>
    /// 主摄像机
    /// </summary>
    MainCamera mainCamera;
    /// <summary>
    /// 关联键位绑定数据
    /// </summary>
    public void BindInputAsset()
    {
        input.actions = DataAndInitMgr.Instance.asset;
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
        BindInputAsset();//关联action唯一入口,关联数据管理器内已经初始化完成的数据

        playerModle=DataConfigurationMgr.Instance.StartRes.playerModleName;

        //注册按键响应
        input.onActionTriggered += (callBack) =>
        {
            //确保是触发下响应
            if (callBack.phase == InputActionPhase.Performed)
            {
                //用于测试当前触发的action名称
                //Debug.Log("Action Triggered: " + callBack.action.name);
                switch (callBack.action.name)
                {
                    case "Move":
                        //print("移动开始");
                        inputData.moveInput = callBack.ReadValue<float>();
                        break;

                    case "Jump":

                        //print("跳跃");
                        inputData.jumpPressed = true;//new Vector2(rb.velocity.x, jumpForce);
                        break;
                }
            }
            //按键抬起逻辑，数据复原
            else if (callBack.phase == InputActionPhase.Canceled)
            {
                switch (callBack.action.name)
                {
                    case "Move":
                        //print("移动取消");
                        //print(callBack.ReadValue<float>());
                        inputData.moveInput = callBack.ReadValue<float>();
                        break;

                    case "Jump":
                        //print("跳跃松开");
                        break;
                }
            }
        };
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
            input.actions.Enable();
        }
        else
        {
            input.actions.Disable();
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
    }
}
