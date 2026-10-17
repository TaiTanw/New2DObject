using PhyData;
using System.Collections;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;


/// <summary>
/// 输入解析结果
/// </summary>
public class PlayerInputData
{
    public float moveInput;     //水平输入
    public float verticalInput; //竖直输入
    public bool jumpPressed;     // 这一帧是否按下
    public bool jumpRelease;       //跳跃是否长按中
}
/// <summary>
/// 四向移动原因；飞行由外部开关选择，游泳由水体事实解析
/// </summary>
public enum FourWayMode { None, Swimming, SurfaceSwimming, Flying }
/// <summary>
/// 持续性动作执行数据（瞬时动作靠事件触发
/// </summary>
public class MovementData 
{
    /// <summary>
    /// 移动方向，-1到1，最终速度由物理组件计算
    /// </summary>
    public float onMove;
    /// <summary>
    /// 竖直方向，-1到1；向下超过一半时放开脚下的单向平台
    /// </summary>
    public float verticalInput;
    /// <summary>
    /// 当前状态
    /// </summary>
    public PlayerStateMachine.E_playerState nowState; 
    /// <summary>
    /// 水面跃出期间：等待物理消费冲量并上升，期间不被水面重新捕获
    /// </summary>
    public bool surfaceJumpInProgress;
}

/// <summary>
/// 只读包装类（仅包装需要读的属性）
/// </summary>
public class ReadOnly_ActionData
{
    private readonly MovementData _data;
    public ReadOnly_ActionData(MovementData data) => _data = data;
    //只读属性
    public float onMove => _data.onMove;
    public float verticalInput => _data.verticalInput;
    public PlayerStateMachine.E_playerState NowState =>_data.nowState;
    public bool isSurfaceSwimming => NowState == PlayerStateMachine.E_playerState.surfaceSwimming;
    public bool isSwimming => NowState == PlayerStateMachine.E_playerState.swimming || isSurfaceSwimming;
    public bool isFlying => NowState == PlayerStateMachine.E_playerState.flying;
    public bool surfaceJumpInProgress => _data.surfaceJumpInProgress;
    public FourWayMode FourWay => isFlying ? FourWayMode.Flying : isSurfaceSwimming ? FourWayMode.SurfaceSwimming
        : isSwimming ? FourWayMode.Swimming : FourWayMode.None;

}

public class Player : MonoBehaviour
{
    /// <summary>
    /// 物理组件
    /// </summary>
    CharacterPhysics playPDate;
    /// <summary>
    /// 物理碰撞信息
    /// </summary>
    public ReadOnly_GeometryPhysicsData NowPhyData => playPDate.ReadOnly_GeometryPhysicsData;

    public ReadOnly_PlayerPhysicsData NowSpeedData => playPDate.ReadOnly_PlayersPhysicsData;
    /// <summary>
    /// 玩家当前输入数据,初始化交给外部
    /// </summary>
    PlayerInputData inputData;
    /// <summary>
    /// 玩家当前所执行动作
    /// </summary>
    MovementData actionData;
    public ReadOnly_ActionData _ActionData;

    /// <summary>
    /// 玩家逻辑状态机对象
    /// </summary>
    PlayerStateMachine fsm;
    /// <summary>
    /// 表现层状态机
    /// </summary>
    PresentationLayer animatorFSM;
    public bool IsFlying => playPDate != null && playPDate.IsFlying;

    void Awake()
    {

        //玩家主动拉取输入控制权限，调用方法保证唯一玩家控制权,且便于控制时序，保证先于状态机初始化
        InputControlMgr.Instance.BindPlayer(this);
        //可执行动作数据初始化
        actionData = new MovementData();
        _ActionData = new ReadOnly_ActionData(actionData);
        //逻辑状态机/物理状态/表现层初始化（保证存在
        fsm = new PlayerStateMachine();
        playPDate = gameObject.GetComponentInChildren<CharacterPhysics>();
        animatorFSM = gameObject.GetComponentInChildren<PresentationLayer>();

    }
    private void Start()
    {
        //保证可用
        fsm.InitData(playPDate.ReadOnly_GeometryPhysicsData, inputData, actionData);
        playPDate.Init(_ActionData, fsm.EventSystem);
        animatorFSM.Init(playPDate.ReadOnly_PlayersPhysicsData,playPDate.ReadOnly_GeometryPhysicsData, _ActionData);
    }
    /// <summary>
    /// 注入玩家按键数据行为
    /// </summary>
    /// <param name="data"></param>
    public void ChangeInputAsset(PlayerInputData data)
    {
        inputData = data;
    }
    public void RemoveInput()
    {
        inputData = null;
    }

    /// <summary>基本运动模式开关；不携带身份、资源或关卡规则。深嵌入时拒绝恢复实心。</summary>
    public bool TrySetFlying(bool enabled)
    {
        if (playPDate == null || fsm == null || inputData == null || !playPDate.IsMotionReady) return false;
        if (IsFlying == enabled) return true;
        if (!playPDate.TrySetFlying(enabled)) return false;
        fsm.ResetMotionActions();
        inputData.jumpPressed = inputData.jumpRelease = false;
        fsm.Update(inputData, ResolveFourWay(), playPDate.HasPendingJump, playPDate.GetPlanarVelocity().y);
        return true;
    }
    /// <summary>飞行优先；非飞行时按当前水体事实选择游泳。</summary>
    FourWayMode ResolveFourWay()
    {
        if (IsFlying) return FourWayMode.Flying;
        EntityWaterFrame water = playPDate.EnvironmentContext.WaterFrame;
        if (water.submerged && water.swimmingAvailable) return FourWayMode.Swimming;
        if (!water.submerged && water.surfaceSwimmingAvailable) return FourWayMode.SurfaceSwimming;
        return FourWayMode.None;
    }
    //float time;
    void Update()
    {
        //逻辑状态机更新，接收输入信息发送动作数据
        fsm.Update(inputData, ResolveFourWay(), playPDate.HasPendingJump, playPDate.GetPlanarVelocity().y);
        // 入水后撤销等待物理消费的跳跃，避免入水又离水时补执行
        if (_ActionData.FourWay != FourWayMode.None) playPDate.ClearPendingActions();
    }

}

