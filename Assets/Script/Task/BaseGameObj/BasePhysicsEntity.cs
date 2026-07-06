using Cysharp.Threading.Tasks.Triggers;
using PhyData;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Text;
using UnityEngine;
using System.Runtime.InteropServices;

/// <summary>
/// 角色物理基类
/// </summary>
public abstract class BasePhysicsEntity : BasicEntity
{
    /// <summary>
    /// 编辑器画图显示碰撞检测范围
    /// </summary>
#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (groundV == null) return;
        if (leftV == null) return;
        if (rightV == null) return;
        // 设置颜色：绿色半透明，便于观察
        Gizmos.color = new UnityEngine.Color(0, 1, 0, 0.5f);

        // 绘制检测区域的线框矩形（位置、大小、旋转）
        Gizmos.DrawWireCube(groundV.position, cPhysics.boxCastH);
        Gizmos.DrawWireCube(leftV.position, cPhysics.boxCastV);
        Gizmos.DrawWireCube(rightV.position, cPhysics.boxCastV);
    }
#endif

    #region 运行时数据
    //只读数据包装
    ReadOnly_GeometryPhysicsData readOnly_GeometryPhysicsData;
    public ReadOnly_GeometryPhysicsData ReadOnly_GeometryPhysicsData => readOnly_GeometryPhysicsData;

    ReadOnly_PlayerPhysicsData readOnly_PlayerPhysicsData;
    public ReadOnly_PlayerPhysicsData ReadOnly_PlayersPhysicsData => readOnly_PlayerPhysicsData;

    BaseGround lastFrameGroundPlatform = null;  //缓存上一帧的平台

    #endregion

    protected override void Awake()
    {
        base.Awake();

        readOnly_GeometryPhysicsData = new(nowGemetry);
        readOnly_PlayerPhysicsData = new(playerPhysicsData);
    }
    protected override void GeometricQuery()
    {
        // 地面检测（只做检测，不做响应）
        RaycastHit2D hit = Physics2D.BoxCast(groundV.position, cPhysics.boxCastH, 0, Vector2.down, 0f, cPhysics.groundLayer);
        //缓存贴地法线
        nowGemetry.groundNormal = hit.normal;
        //默认数据声明
        BaseGround currentGroundPlatform = null;
        bool onGroundNow = false;
        //默认的空中阻力系数
        self_resistanceCoefficient = 1;
        //一定角度内正对碰撞才算着地
        if (hit.collider != null && Vector2.Dot(hit.normal, Vector2.up) > 0.7f)
        {
            onGroundNow = true;
            //在地面，则自身阻力增大
            self_resistanceCoefficient = 20;
            //下文需要判空处理，空表示无任何特殊逻辑的地面
            hit.collider.TryGetComponent<BaseGround>(out currentGroundPlatform);
            if (currentGroundPlatform)
                //当找到脚本时，自身阻力受到地面阻力影响
                self_resistanceCoefficient *= currentGroundPlatform.SlowingEffect;
        }
        // 比对状态，只在地面改变时调用
        if (currentGroundPlatform != lastFrameGroundPlatform)//简易理解为状态（同时只能在一种地面上）
        {
            // 离开上一个平台
            if (lastFrameGroundPlatform != null)
            {
                lastFrameGroundPlatform.OnPhyExit(this);
            }

            // 进入新平台
            if (currentGroundPlatform != null)
            {
                currentGroundPlatform.OnPhyEnter(this);
            }
            // 记录本帧状态，供下帧对比
            lastFrameGroundPlatform = currentGroundPlatform;
        }
        // 更新数据
        nowGemetry.nowtaijie = currentGroundPlatform;
        nowGemetry.isGrounded = onGroundNow;
        //检测左右靠墙
        RaycastHit2D hit1 = Physics2D.BoxCast(leftV.position, cPhysics.boxCastV, 0, Vector2.left, 0f, cPhysics.wallLayer);
        RaycastHit2D hit2 = Physics2D.BoxCast(rightV.position, cPhysics.boxCastV, 0, Vector2.right, 0f, cPhysics.wallLayer);
        //检测墙是否有特殊逻辑（无特殊逻辑则表示无法贴墙下滑
        nowGemetry.onLeftWall = false;
        nowGemetry.canLeftWall = null;
        //夹角需小于25度，内积近似0.9
        if (hit1.collider != null && Vector2.Dot(hit1.normal, Vector2.right) > 0.9f)
        {
            nowGemetry.onLeftWall = true;
            hit1.collider.TryGetComponent<Wall>(out nowGemetry.canLeftWall);
        }
        nowGemetry.onRightWall = false;
        nowGemetry.canRightWall = null;
        if (hit2.collider != null && Vector2.Dot(hit2.normal, Vector2.left) > 0.9f)
        {
            nowGemetry.onRightWall = true;
            hit2.collider.TryGetComponent<Wall>(out nowGemetry.canRightWall);
        }

    }
    protected override void HActiveSpeedOperation()
    {
        //处理基础移动（赋值操作，最先）//计算主动位移
        playerPhysicsData.horizontalSpeed = HorizontalSpeedCalculation() * (1 + playerPhysicsData.nowPhyNum);
        //统一事件触发（控制时序在物理检测和赋值操作之后
        PhyEventUpdate();
    }
    protected override void VActiveSpeedOperation()
    {
        VerticalTransmission();
    }
    
    /// <summary>
    /// 最先执行的水平速度赋值操作
    /// </summary>
    /// <param name="horizontalSpeed"></param>
    protected abstract float HorizontalSpeedCalculation();

    /// <summary>
    /// 统一消费物理事件更新逻辑
    /// </summary>
    protected abstract void PhyEventUpdate();
    
    /// <summary>
    /// 处理特殊行为的状态性垂直变速（例如贴墙下滑，攀岩
    /// </summary>
    /// <param name="v"></param>
    protected abstract void VerticalTransmission();



    protected override void OnDisable()
    {
        base.OnDisable();

        lastFrameGroundPlatform?.OnPhyExit(this);
        lastFrameGroundPlatform = null;
    }
}
