using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PhyData
{
    /// <summary>
    /// 受力效果
    /// </summary>
	public struct SpeedStackData
    {
        /// <summary>
        /// 响应计时器（控制瞬发速度何时复原（受力时间
        /// </summary>
        public float responseTimer1;
        /// <summary>
        /// 技能速度叠加(墙跳，冲刺等等，受力大小
        /// </summary>
        public Vector2 speedStack;

    }

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
        public BaseGround nowtaijie;
        public bool isGrounded;     // 物理检测
        public bool onLeftWall; //左右墙布尔，表示受墙的影响因素 
        public bool onRightWall;
        public Wall canLeftWall;
        public Wall canRightWall;
        /// <summary>
        /// 当前倚靠的墙
        /// </summary>
        public Wall nowWall;
        /// <summary>
        /// 当前环境物理约束数据,移动速度
        /// </summary>
        public float nowPhyNum;
        /// <summary>
        /// 外部速度叠加
        /// </summary>
        public Vector2 externalSpeed;

        public void Entrance()
        {

        }
    }
    /// <summary>
    /// 物理实时数据只读包装
    /// </summary>
    public class ReadOnly_PlayerPhysicsData
    {
        private readonly PlayerPhysicsData _data;

        public ReadOnly_PlayerPhysicsData(PlayerPhysicsData data)
        {
            _data = data;
        }

        public float horizontalSpeed => _data.horizontalSpeed;   //当前自身水平速度
        public float verticalSpeed => _data.verticalSpeed;  //当前自身竖直速度
        /// <summary>
        /// 当前玩家所属平台
        /// </summary>
        public BaseGround nowtaijie => _data.nowtaijie;
        public bool isGrounded => _data.isGrounded;     // 物理检测
        public bool onLeftWall => _data.onLeftWall; //左右墙布尔，后续可替换为墙接口，表示受墙的影响因素 
        public bool onRightWall => _data.onRightWall;

        public Wall canRightWall=> _data.canRightWall;
        public Wall canLeftWall=> _data.canLeftWall;

        public Wall nowKWall => _data.nowWall;

    }
}
