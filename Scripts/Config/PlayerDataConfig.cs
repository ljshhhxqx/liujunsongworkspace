using System;
using System.Collections.Generic;
using Config;
using UnityEngine;
using UnityEngine.Serialization;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "PlayerData", menuName = "ScriptableObjects/PlayerData")]
    public class PlayerDataConfig : ConfigBase
    {
        [SerializeField] 
        private PlayerConfigData playerConfigData;
        public PlayerConfigData PlayerConfigData => playerConfigData;
        private readonly Dictionary<AnimationState, AnimationInfo> _animationInfos = new Dictionary<AnimationState, AnimationInfo>();

        public override void Init()
        {
            if (_animationInfos.Count != 0) return;
            foreach (var animationInfo in playerConfigData.AnimationInfos)
            {
                _animationInfos.Add(animationInfo.State, animationInfo);
            }
        }
        
        public AnimationInfo GetAnimationInfo(AnimationState animationState)
        {
            return _animationInfos.GetValueOrDefault(animationState);
        }

        public float GetPlayerAnimationCost(AnimationState state)
        {
            return GetAnimationInfo(state).Cost;
        }
        
        public float GetPlayerAnimationCooldown(AnimationState state)
        {
            return GetAnimationInfo(state).Cooldown;
        }
    }

    [Serializable]
    public class PlayerConfigData
    {
        #region Camera

        public float TurnSpeed;
        public float MouseSpeed;
        public Vector3 Offset;

        #endregion
    
        #region Player

        public float SprintSpeedFactor = 1.5f;
        public float RotateSpeed;
        public float OnStairsSpeedRatioFactor = 0.7f;
        public float JumpSpeed;
        public float StairsJumpSpeed;
        public float GroundCheckRadius;
        public float StairsCheckDistance;
        public List<PropertyType> MaxProperties;
        public List<PropertyType> BaseProperties;
        public List<PropertyType> MinProperties;
        public float StrengthRecoveryPerSecond;
        public float RollForce;
        public float StepHeight = 0.3f; // 玩家可跨越的最大台阶高度
        public float StepCheckDistance = 0.5f; // 台阶检测距离
        
        #endregion
        #region Animation
        
        public float AttackComboWindow = 1f; // 连招窗口时间
        public int AttackComboMaxCount = 3; // 连招最大次数
        public List<AnimationInfo> AnimationInfos;
        
        #endregion

        public float RollInvincibleDuration = 0.5f;
    }

    [Serializable]
    public struct AnimationInfo
    {
        public AnimationState State;
        public float Cost;
        public float Cooldown;
        public int Priority;
        public AnimationType AnimationType;
        public bool CanBeInterrupted;
    }
    
    public enum AnimationType
    {
        Continuous,  // 持续性动画（待机、移动、奔跑）
        Single,      // 一次性动画（跳跃、翻滚、受击、死亡）
        Combo        // 连击动画（攻击）
    }

    public enum PlayerEnvironmentState
    {
        InAir,
        OnGround,
        OnStairs,
        Swimming,
    }

    public enum AnimationState
    {
        Idle,
        Move,
        Sprint,
        Jump,
        SprintJump,
        Roll,
        Falling,
        Landed,
        Attack,
        Dead,
        Hit,
        Death
    }
}