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
    public partial struct PlayerInputState : IPredictablePropertyState
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
        
        public bool IsEqual(IPredictablePropertyState other, float tolerance = 0.01f)
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
        public List<CooldownSnapshotData> AnimationCooldowns;
        
        public PlayerAnimationCooldownState(List<CooldownSnapshotData> animationCooldowns)
        {
            AnimationCooldowns = animationCooldowns;
        }

        public PlayerAnimationCooldownState Reset(PlayerAnimationCooldownState state)
        {
            for (int i = 0; i < AnimationCooldowns.Count; i++)
            {
                var cooldown = AnimationCooldowns[i];
                AnimationCooldowns[i] = cooldown.Reset(cooldown);
            }
            return state;
        }

        public bool IsEqual(PlayerAnimationCooldownState other)
        {
            if (AnimationCooldowns.Count != other.AnimationCooldowns.Count)
            {
                return false;
            }

            for (int i = 0; i < AnimationCooldowns.Count; i++)
            {
                if (!AnimationCooldowns[i].Equals(other.AnimationCooldowns[i]))
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