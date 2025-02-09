using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
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
        
        public PlayerInputState(PlayerGameStateData playerGameStateData, PlayerInputStateData playerInputStateData)
        {
            PlayerGameStateData = playerGameStateData;
            PlayerInputStateData = playerInputStateData;
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
    }

    [MemoryPackable]
    public partial struct PlayerGameStateData
    {
        [MemoryPackOrder(0)] 
        public Vector3 position;         // 位置
        [MemoryPackOrder(1)] 
        public Vector3 velocity;         // rigidbody的速度
        [MemoryPackOrder(2)] 
        public Quaternion rotation;      // 旋转
        [MemoryPackOrder(3)] 
        public AnimationState command;   // 当前执行的命令
        [MemoryPackOrder(4)] 
        public PlayerEnvironmentState environmentState; // 玩家在什么环境中

        public bool IsEqual(PlayerGameStateData other)
        {
            return Vector3.Distance(position, other.position) < 2f && 
                   Mathf.Abs(velocity.magnitude - other.velocity.magnitude) < 0.05f &&
                   Quaternion.Angle(rotation, other.rotation) < 10f && 
                   command == other.command && 
                   environmentState == other.environmentState;
        }
    }
}