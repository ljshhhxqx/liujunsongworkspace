using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using MemoryPack;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.State
{
    [MemoryPackable]
    public partial struct PlayerInputState : IPropertyState
    {
        [MemoryPackOrder(0)] public PlayerGameStateData PlayerGameStateData;
        [MemoryPackOrder(1)] public PlayerInputStateData PlayerInputStateData;
        [MemoryPackOrder(2)] public PlayerAnimationCooldownState PlayerAnimationCooldownState;
        
        public PlayerInputState(PlayerGameStateData playerGameStateData, PlayerInputStateData playerInputStateData, PlayerAnimationCooldownState playerAnimationCooldownState)
        {
            PlayerGameStateData = playerGameStateData;
            PlayerInputStateData = playerInputStateData;
            PlayerAnimationCooldownState = playerAnimationCooldownState;
        }
        
        public bool IsEqual(IPropertyState other, float tolerance = 0.01f)
        {
            if (other is PlayerInputState playerInputState)    
            {
                return PlayerGameStateData.IsEqual(playerInputState.PlayerGameStateData);
            }
            return false;
        }
    }

    [MemoryPackable]
    public partial struct PlayerInputStateData
    {
        [MemoryPackOrder(0)] 
        public Vector3 InputMovement;   // 输入的移动
        [MemoryPackOrder(1)] 
        public AnimationState[] InputAnimations; // 输入指令的动画
        [MemoryPackOrder(2)]
        public AnimationState Command; // 指令
        [MemoryPackOrder(3)] 
        public int AttackCount; // 攻击次数
    }

    [MemoryPackable]
    public partial struct PlayerAnimationCooldownState
    {
        [MemoryPackOrder(0)]
        public List<IAnimationCooldown> AnimationCooldowns;
        
        public PlayerAnimationCooldownState(List<IAnimationCooldown> animationCooldowns)
        {
            AnimationCooldowns = animationCooldowns;
        }   
        
        public bool IsEqual(PlayerAnimationCooldownState other)
        {
            if (AnimationCooldowns.Count != other.AnimationCooldowns.Count)
            {
                return false;
            }
            for (int i = 0; i < AnimationCooldowns.Count; i++)
            {
                if (!AnimationCooldowns[i].IsEqual(other.AnimationCooldowns[i]))
                {
                    return false;
                }
            }
            return true;
        }
    }

    [MemoryPackable]
    public partial struct PlayerGameStateData
    {
        [MemoryPackOrder(0)] 
        public Vector3 Position;         // 位置
        [MemoryPackOrder(1)] 
        public Vector3 Velocity;         // rigidbody的速度
        [MemoryPackOrder(2)] 
        public Quaternion Quaternion;      // 旋转
        [MemoryPackOrder(3)] 
        public AnimationState AnimationState;   // 当前执行的命令
        [MemoryPackOrder(4)] 
        public PlayerEnvironmentState PlayerEnvironmentState; // 玩家在什么环境中

        public bool IsEqual(PlayerGameStateData other)
        {
            return Vector3.Distance(Position, other.Position) < 2f &&
                   Mathf.Abs(Velocity.magnitude - other.Velocity.magnitude) < 0.05f &&
                   Quaternion.Angle(Quaternion, other.Quaternion) < 10f &&
                   AnimationState == other.AnimationState &&
                   PlayerEnvironmentState == other.PlayerEnvironmentState;
        }
    }
}