using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using MemoryPack;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.State
{
    [Serializable]
    public struct PlayerInputState : IPropertyState
    {
        public PlayerGameStateData PlayerGameStateData { get; private set; }
        public PlayerInputStateData PlayerInputStateData { get; private set; }
        
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

    [Serializable]
    public struct PlayerInputStateData
    {
        public Vector3 inputMovement;   // 输入的移动
        public List<AnimationState> inputAnimations; // 输入指令的动画
    }

    [Serializable]
    [MemoryPackable]
    public partial struct PlayerGameStateData
    {
        public Vector3 position;         // 位置
        public Vector3 velocity;         // rigidbody的速度
        public Quaternion rotation;      // 旋转
        public AnimationState command;   // 当前执行的命令
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