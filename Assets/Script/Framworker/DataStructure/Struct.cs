using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhyData
{
    /// <summary>
    /// 物理引用接口
    /// </summary>
    public interface IPhyBaseI { }

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
    /// 状态受力数据
    /// </summary>
    public struct ForceData 
    {
        /// <summary>
        /// 受力大小
        /// </summary>
        public float Force;
        /// <summary>
        /// 受控复原速度
        /// </summary>
        public float recoverySpeed;
        /// <summary>
        /// 此施力所致的速度叠加
        /// </summary>
        public float speedStacking;
        /// <summary>
        /// 平衡所致条件(必须为正数
        /// </summary>
        public float balanceSpeed;
        /// <summary>
        /// 当满足当前叠加速度大于平衡速度时
        /// </summary>
        public readonly bool IsBalance => Mathf.Abs(speedStacking) > Mathf.Abs(balanceSpeed);
        /// <summary>
        /// 施力类型
        /// </summary>
        public E_PhyForceType type;

        public void Init(float Force,float recoverySpeed, float balanceSpeed)
        {
            this.Force = Force;
            this.recoverySpeed = recoverySpeed;
            this.balanceSpeed = Mathf.Abs(balanceSpeed);
            type = E_PhyForceType.apply;
        }


        /// <summary>
        /// 当类型为施加的物理帧更新逻辑
        /// </summary>
        /// <param name="quality">受力物体质量</param>
        /// <returns></returns>
        public float FixUpdate(float quality)
        {
            speedStacking += Force * quality * Time.fixedDeltaTime;
            if(IsBalance)
            {
                if (speedStacking < 0)
                {
                    speedStacking=-balanceSpeed;
                }
                else
                {
                    speedStacking=balanceSpeed;
                }
            }

            return speedStacking;
        }

        public float ControlledSpeedRecovery(float quality)
        {
            if (speedStacking > 0.2)
            {
                speedStacking -= recoverySpeed * quality * Time.fixedDeltaTime;
            }
            else if (speedStacking < -0.2)
            {
                speedStacking += recoverySpeed * quality * Time.fixedDeltaTime;
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
    /// 速度实时物理数据
    /// </summary>
    public class PlayerPhysicsData
    {
        public float horizontalSpeed;   //当前自身水平速度（主动位移
        public float verticalSpeed;  //当前自身竖直速度（可主动影响
        public float phyHSpeed;//水平物理影响速度（被动位移
        public float phyVSpeed;//垂直物理影响速度（被动

        /// <summary>
        /// 当前环境物理约束数据,移动速度（粘滞力
        /// </summary>
        public float nowPhyNum;

    }
    /// <summary>
    /// 几何物理检测结果数据
    /// </summary>
    public class GeometryPhysicsData
    {
        /// <summary>
        /// 当前玩家所属平台
        /// </summary>
        public IPhyBaseI nowtaijie;
        public bool istop;//是否顶头
        public bool isGrounded;     // 物理检测
        public bool onLeftWall; //左右墙布尔，表示受墙的影响因素 
        public bool onRightWall;
        public IPhyBaseI canLeftWall;
        public IPhyBaseI canRightWall;
        /// <summary>
        /// 当前倚靠的墙
        /// </summary>
        public IPhyBaseI nowWall;
        /// <summary>
        /// 贴地法线
        /// </summary>
        public Vector2 groundNormal;
    }
    /// <summary>
    /// 物理职能基类
    /// </summary>
    public class BasePhyFunData
    {
        public BaseGround nowGround;
    }
    /// <summary>
    /// 角色物理职能数据
    /// </summary>
    public class PhysicalFunctionData:BasePhyFunData
    {
        public Wall canLeftWall;
        public Wall canRightWall;
        /// <summary>
        /// 当前倚靠的墙
        /// </summary>
        public Wall nowWall;
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

    public class ReadOnly_GeometryPhysicsData
    {
        private readonly GeometryPhysicsData _data;
        private readonly PhysicalFunctionData _data2;
        public ReadOnly_GeometryPhysicsData(GeometryPhysicsData data,PhysicalFunctionData data2)
        {
            _data = data;
            _data2 = data2;
        }

        /// <summary>
        /// 当前玩家所属平台
        /// </summary>
        public IPhyBaseI nowtaijie => _data.nowtaijie;
        public bool isGrounded => _data.isGrounded;     // 物理检测
        public bool onLeftWall => _data.onLeftWall; //左右墙布尔，后续可替换为墙接口，表示受墙的影响因素 
        public bool onRightWall => _data.onRightWall;

        public Wall canRightWall => _data2.canRightWall;
        public Wall canLeftWall => _data2.canLeftWall;

        public Wall nowKWall => _data2.nowWall;
    }


    #endregion
}
