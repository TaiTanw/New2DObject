using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhyData
{
    /// <summary>
    /// 限时速度叠加效果
    /// </summary>
	public struct SpeedStackData
    {
        /// <summary>
        /// 响应计时器（控制瞬发速度何时复原（受力时间
        /// </summary>
        public float responseTimer1;

        /// <summary>
        /// 水平速度
        /// </summary>
        public float hspeed;

    }
    /// <summary>
    /// 施力类型
    /// </summary>
    public enum E_PhyForceType
    {
        apply,//施加
        balance,//平衡
        controlRecovery,//控制复原
        fadeAway,//消退
    }

    /// <summary>
    /// 动态施力参数：不包含实体已累计的速度或当前受力类型。
    /// 环境来源创建参数，实体的 ForceData 持有参数并独立维护跨帧状态。
    /// </summary>
    public readonly struct DynamicForceParameters
    {
        public readonly float Force;
        public readonly float balanceSpeed;
        public readonly float recoverySpeed;

        public DynamicForceParameters(float force, float balanceSpeed, float recoverySpeed)
        {
            Force = force;
            this.balanceSpeed = Mathf.Abs(balanceSpeed);
            this.recoverySpeed = recoverySpeed;
        }
    }

    /// <summary>
    /// 动态受力数据：施力参数与跨帧状态分开保存。
    /// </summary>
    public struct ForceData 
    {
        /// <summary>施力配置；刷新此值不覆盖下方的跨帧状态。</summary>
        public DynamicForceParameters parameters;
        public readonly float Force => parameters.Force;
        public readonly float recoverySpeed => parameters.recoverySpeed;
        public readonly float balanceSpeed => parameters.balanceSpeed;
        /// <summary>
        /// 此施力所致的速度叠加
        /// </summary>
        public float speedStacking;
        /// <summary>
        /// 当满足当前叠加速度大于平衡速度时
        /// </summary>
        public readonly bool IsBalance => Mathf.Abs(speedStacking) > Mathf.Abs(parameters.balanceSpeed);
        /// <summary>
        /// 施力类型
        /// </summary>
        public E_PhyForceType type;

        public void Init(float Force,float recoverySpeed, float balanceSpeed)
        {
            parameters = new DynamicForceParameters(Force, balanceSpeed, recoverySpeed);
            type = E_PhyForceType.apply;
        }


        /// <summary>
        /// 当类型为施加的物理帧更新逻辑
        /// </summary>
        /// <param name="quality">受力物体受环境影响程度（质量倒数）</param>
        /// <returns>速度叠加</returns>
        public float FixUpdate(float quality)
        {
            speedStacking += parameters.Force * quality * Time.fixedDeltaTime;
            if(IsBalance)
            {
                if (speedStacking < 0)
                {
                    speedStacking=-parameters.balanceSpeed;
                }
                else
                {
                    speedStacking=parameters.balanceSpeed;
                }
            }

            return speedStacking;
        }
        /// <summary>
        /// 受控速度恢复
        /// </summary>
        /// <param name="quality">质量倒数</param>
        /// <returns></returns>
        public float ControlledSpeedRecovery(float quality)
        {
            if (speedStacking > 0.2)
            {
                speedStacking -= parameters.recoverySpeed * quality * Time.fixedDeltaTime;
            }
            else if (speedStacking < -0.2)
            {
                speedStacking += parameters.recoverySpeed * quality * Time.fixedDeltaTime;
            }
            else
            {
                speedStacking = 0;
            }

            return speedStacking;
        }


    }

    #region 物理实时数据

    /// <summary>
    /// 实时物理移动效应数据
    /// </summary>
    public class PlayerPhysicsData
    {
        /// <summary>
        /// 当前自身水平速度（主动位移
        /// </summary>
        public float horizontalSpeed;
        /// <summary>
        /// 当前自身竖直速度（可主动影响
        /// </summary>
        public float verticalSpeed;
        /// <summary>
        /// 水平物理影响速度（被动位移
        /// </summary>
        public float phyHSpeed;
        /// <summary>
        /// 垂直物理影响速度（被动
        /// </summary>
        public float phyVSpeed;
        /// <summary>
        /// 位移偏置（旧草稿，正式管线改用 EntitySolutionResult.displacementOffset）
        /// </summary>
        //public float displacementBias;
        /// <summary>
        /// 位移嵌入（旧草稿，预测重叠在解算器内用 AABB v4 计算，不写回此字段）
        /// </summary>
        //public float positionalEmbedding;
        /// <summary>
        /// 当前环境物理约束数据,移动速度（粘滞力
        /// </summary>
        public float nowPhyNum;

    }
    /// <summary>
    /// 相位 1 记下本帧碰到了什么：脚下、左右墙、是否顶头、是否着地。
    /// 例如侧面碰到墙，这里只留 Collider 和 onLeftWall，还不表示能贴墙下滑。
    /// 职能：几何接触事实；能力是否有效在 PhysicalFunctionData，环境效果在 Context。
    /// </summary>
    public class GeometryPhysicsData
    {
        // 真实命中与脚本能力分别保存；无脚本地面/墙也保留 Collider 几何身份。
        public Collider2D groundCollider;
        public Collider2D leftWallCollider;
        public Collider2D rightWallCollider;
        public bool istop;//几何：头顶方向碰到实心碰撞体
        public bool isGrounded;     // 几何：法线判定后的着地，不表示表面能力
        public bool onLeftWall; // 几何：左侧接触，不表示墙滑能力
        public bool onRightWall; // 几何：右侧接触，不表示墙滑能力

        /// <summary>
        /// 贴地法线
        /// </summary>
        public Vector2 groundNormal;

        /// <summary>
        /// 本帧自身碰撞体重叠到的区域 Trigger。例如人碰到水的触发器，这里先记下碰撞体，还不登记减速。
        /// 职能：相位 1 感知缓冲；监测点与嵌入深度不放在本列表。
        /// </summary>
        public readonly List<Collider2D> regionOverlaps = new List<Collider2D>();
    }
    /// <summary>
    /// 物理职能基类
    /// </summary>
    public class BasePhyFunData
    {
        /// <summary>
        /// 已废弃：推墙/推箱不再用 Enter 边沿 AddForce（保留字段以免旧序列化/注释对照）
        /// </summary>
        public IForceAction nowForceThing;
        /// <summary>
        /// 已废弃：同上
        /// </summary>
        public IForceAction lastForceThing;
        // 支撑环境关系及采样已归实体 EnvironmentContext，不再保存 BaseGround 类型引用。


    }
    /// <summary>
    /// 角色在相位 2 从左右墙 Collider 解析出的墙滑能力引用。
    /// 例如左侧碰到普通墙时 canLeftWall 为空，几何上的 onLeftWall 仍可为真。
    /// 职能：物理职能结果；逻辑层只读其是否有效，乘数由执行层读取。箱体不使用本类型。
    /// </summary>
    public class PhysicalFunctionData:BasePhyFunData
    {
        public IWallSlideSurface canLeftWall;
        public IWallSlideSurface canRightWall;
    }
    /// <summary>
    /// 速度物理实时数据只读包装
    /// </summary>
    public class ReadOnly_PlayerPhysicsData
    {
        private readonly PlayerPhysicsData _data;

        public ReadOnly_PlayerPhysicsData(PlayerPhysicsData data)
        {
            _data = data;
        }

        public float horizontalSpeed => _data.horizontalSpeed;   //当前自身水平速度(主动

        public float phyHSpeed => _data.phyHSpeed;//被动水平速度
        public float verticalSpeed => _data.verticalSpeed;  //当前自身竖直速度

        public float phyVSpeed=>_data.phyVSpeed;//被动垂直速度


    }

    /// <summary>
    /// 逻辑层只读出口。着地和左右贴墙读几何接触；canLeftWall / canRightWall 读墙滑能力是否仍有效。
    /// 职能：行为层边界。不暴露墙滑乘数，也不把几何接触和能力可用性合成一个含义。
    /// </summary>
    public class ReadOnly_GeometryPhysicsData
    {
        private readonly GeometryPhysicsData _data;
        private readonly PhysicalFunctionData _data2;
        public ReadOnly_GeometryPhysicsData(GeometryPhysicsData data,PhysicalFunctionData data2)
        {
            _data = data;
            _data2 = data2;
        }

        public bool isGrounded => _data.isGrounded; // 几何接触
        public bool onLeftWall => _data.onLeftWall; // 几何接触
        public bool onRightWall => _data.onRightWall; // 几何接触

        public bool canRightWall => EnvironmentCapabilities.IsActive(_data2.canRightWall); // 职能：右侧墙滑能力可用
        public bool canLeftWall => EnvironmentCapabilities.IsActive(_data2.canLeftWall); // 职能：左侧墙滑能力可用

    }


    #endregion

    /// <summary>
    /// 实体本帧的未约束运动快照。
    /// BasicEntity 在相位 3 生成一次，预测与相位 5 提交复用。
    /// 使用只读值字段避免后续相位改写其中的分量；每帧整体替换，不保存跨帧惯性。
    /// 与 EntitySolutionResult 分工：本结构保存基础运动，后者保存相位 4 新产生的接触纠偏。
    /// </summary>
    public readonly struct EntityMotionFrame
    {
        /// <summary>motionDelta + platformDelta；预测与提交的共同基础，不含接触/静墙/引擎修正。</summary>
        public readonly Vector2 plannedWorldDelta;

        public EntityMotionFrame(Vector2 motionDelta, Vector2 platformDelta)
        {
            plannedWorldDelta = motionDelta + platformDelta;
        }
    }

    /// <summary>
    /// 提供给管理器的预测 AABB 样本；几何来自 Collider，平移来自 EntityMotionFrame。
    /// 不持有完整运动状态，接触速度读取仍沿用现有 BasicEntity.GetPlanarVelocity 接口。
    /// </summary>
    public struct PhysicalBoundingBox
    {
        /// <summary>
        /// 世界预测中心：本帧 Collider 中心 + plannedWorldDelta；已包含地面处理和平台补偿。
        /// </summary>
        public Vector2 point;
        /// <summary>
        /// 长宽的一半（x,y）
        /// </summary>
        public Vector2 size;
        /// <summary>
        /// 此投射对应的物理引用
        /// </summary>
        public BasicEntity myPhyBox;
    }

    /// <summary>
    /// 实体接触解算结果。相位 4 写入，相位 5 在同一物理帧消费；跨帧速度由动态力容器维护。
    /// 本阶段结构不变：只承载接触纠偏，不重复保存 EntityMotionFrame 的基础运动。
    /// </summary>
    public struct EntitySolutionResult
    {
        /// <summary>
        /// 位移偏置（本帧 DisplacementCorrection 加到 motionFrame.plannedWorldDelta）
        /// </summary>
        public Vector2 displacementOffset;
    }

}
