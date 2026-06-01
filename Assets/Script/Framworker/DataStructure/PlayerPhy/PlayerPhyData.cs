using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

namespace Physics.Player
{
    /// <summary>
    /// 命令数据调度器类型
    /// </summary>
    public class CommandScheduling
    {
        Dictionary<Type, IPhysicsCommand> commandExampleDic = new();

        public CommandScheduling(CharacterPhysics.PlayerPhysicsData physicsData, SO_CPhysics cPhysics)
        {
            commandExampleDic.Add(typeof(WallJumpCommand), new WallJumpCommand(physicsData, cPhysics));
            commandExampleDic.Add(typeof(JumpCommand), new JumpCommand(physicsData, cPhysics));

        }

        /// <summary>
        /// 获取命令引用
        /// </summary>
        /// <typeparam name="T">指令类型</typeparam>
        /// <returns></returns>
        public IPhysicsCommand CommandToQueue<T>() where T : IPhysicsCommand
        {
            return commandExampleDic[typeof(T)];
        }
    }
    /// <summary>
    /// 玩家物理命令接口 , 所有影响物理的事件都实现这个
    /// </summary>
    public interface IPhysicsCommand
    {
        /// <summary>
        /// 行为触发
        /// </summary>
        void Execute();
    }
    /// <summary>
    /// 物理命令基类
    /// </summary>
    public abstract class BasePhyCommand : IPhysicsCommand
    {
        //实时数据引用
        protected CharacterPhysics.PlayerPhysicsData physicsData;
        //配置物理数据引用
        protected SO_CPhysics cPhysics;
        public BasePhyCommand(CharacterPhysics.PlayerPhysicsData physicsData, SO_CPhysics cPhysics)
        {
            this.physicsData = physicsData;
            this.cPhysics = cPhysics;
        }

        public virtual void Execute()
        {

        }

    }

    /// <summary>
    /// 墙跳命令 - 采集墙体参数后，延迟到物理帧执行
    /// </summary>
    public class WallJumpCommand : BasePhyCommand
    {
        public WallJumpCommand(CharacterPhysics.PlayerPhysicsData physicsData, SO_CPhysics cPhysics) :
            base(physicsData, cPhysics)
        { }
        public override void Execute()
        {
            //计算关键数据
            float jumpHeight;     //跳高程度
            float jumpForce;      //墙跳距离

            jumpHeight = cPhysics.upSpeed;
            jumpForce = cPhysics.wallJumpV * cPhysics.speed;

            physicsData.verticalSpeed = jumpHeight;

            if (physicsData.onLeftWall)
            {
                // speedStack 在这里应用
                physicsData.speedStack += jumpForce;
            }
            else
            {
                physicsData.speedStack -= jumpForce;
            }
            //开始计时
            physicsData.responseTimer1 = cPhysics.toTime;
        }

    }
    /// <summary>
    /// 普通跳跃命令
    /// </summary>
    public class JumpCommand : BasePhyCommand
    {
        public JumpCommand(CharacterPhysics.PlayerPhysicsData physicsData, SO_CPhysics cPhysics) :
        base(physicsData, cPhysics)
        { }

        public override void Execute()
        {
            base.Execute();
            //当前竖直速度等于跳跃速度
            physicsData.verticalSpeed = cPhysics.upSpeed;
        }
    }
}

