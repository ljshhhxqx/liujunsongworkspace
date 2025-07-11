using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using MemoryPack;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using CooldownSnapshotData = HotUpdate.Scripts.Network.PredictSystem.Data.CooldownSnapshotData;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial struct PlayerInputState : ISyncPropertyState
    {
        [MemoryPackOrder(0)] public PlayerGameStateData PlayerGameStateData;
        [MemoryPackOrder(1)] public PlayerAnimationCooldownState PlayerAnimationCooldownState;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerInput;
        
        [MemoryPackConstructor]
        public PlayerInputState(PlayerGameStateData playerGameStateData, PlayerAnimationCooldownState playerAnimationCooldownState)
        {
            PlayerGameStateData = playerGameStateData;
            PlayerAnimationCooldownState = playerAnimationCooldownState;
        }
        
        public bool IsEqual(ISyncPropertyState other, float tolerance = 0.01f)
        {
            if (other is PlayerInputState playerInputState)    
            {
                return PlayerGameStateData.IsEqual(playerInputState.PlayerGameStateData) &&
                       PlayerAnimationCooldownState.IsEqual(playerInputState.PlayerAnimationCooldownState);
            }
            return false;
        }
    }

    public struct PlayerInputStateData
    {
        public Vector3 InputMovement;   // 输入的移动
        public AnimationState InputAnimations; // 输入指令的动画
        public AnimationState Command; // 指令
    }

    [MemoryPackable]
    public partial struct PlayerAnimationCooldownState
    {
        [MemoryPackOrder(0)]
        public MemoryDictionary<AnimationState, CooldownSnapshotData> AnimationCooldowns;
        
        public PlayerAnimationCooldownState(MemoryDictionary<AnimationState, CooldownSnapshotData> animationCooldowns)
        {
            AnimationCooldowns = animationCooldowns;
        }

        public PlayerAnimationCooldownState Reset(PlayerAnimationCooldownState state)
        {
            foreach (var key in state.AnimationCooldowns.Keys)
            {
                if (AnimationCooldowns.ContainsKey(key))
                {
                    AnimationCooldowns[key] = state.AnimationCooldowns[key];
                    continue;
                }
                AnimationCooldowns.Add(key, state.AnimationCooldowns[key]);
            }
            return state;
        }

        public bool IsEqual(PlayerAnimationCooldownState other)
        {
            if (AnimationCooldowns.Count != other.AnimationCooldowns.Count)
            {
                return false;
            }
            
            foreach (var key in AnimationCooldowns.Keys)
            {
                if (!other.AnimationCooldowns.ContainsKey(key) || !AnimationCooldowns[key].Equals(other.AnimationCooldowns[key]))
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
        public CompressedVector3 Position;         // 位置
        [MemoryPackOrder(1)] 
        public CompressedVector3 Velocity;         // rigidbody的速度
        [MemoryPackOrder(2)] 
        public CompressedQuaternion Quaternion;      // 旋转
        [MemoryPackOrder(3)] 
        public AnimationState AnimationState;   // 当前执行的命令
        [MemoryPackOrder(4)] 
        public PlayerEnvironmentState PlayerEnvironmentState; // 玩家在什么环境中

        public bool IsEqual(PlayerGameStateData other)
        {
            return Vector3.Distance(Position, other.Position) < 2f &&
                   Mathf.Abs(Velocity.ToVector3().magnitude - other.Velocity.ToVector3().magnitude) < 0.05f &&
                   UnityEngine.Quaternion.Angle(Quaternion.ToQuaternion(), other.Quaternion.ToQuaternion()) < 10f &&
                   AnimationState == other.AnimationState &&
                   PlayerEnvironmentState == other.PlayerEnvironmentState;
        }
    }
}