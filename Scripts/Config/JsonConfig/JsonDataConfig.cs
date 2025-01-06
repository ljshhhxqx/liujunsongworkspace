using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace HotUpdate.Scripts.Config.JsonConfig
{
    [CreateAssetMenu(fileName = "JsonDataConfig", menuName = "ScriptableObjects/JsonDataConfig")]
    public class JsonDataConfig : ConfigBase
    {
        [SerializeField] 
        private JsonConfigData jsonConfigData;
        private readonly Dictionary<AnimationState, AnimationInfo> _animationInfos = new Dictionary<AnimationState, AnimationInfo>();
        
        public PlayerConfigData PlayerConfig => jsonConfigData.playerConfig;
        public CollectData CollectData => jsonConfigData.collectData;
        public DamageData DamageData => jsonConfigData.damageData;
        public BuffConstantData BuffConstantData => jsonConfigData.buffConstantData;
        public ChestCommonData ChestCommonData => jsonConfigData.chestCommonData;
        public GameConfigData GameConfig => jsonConfigData.gameConfig;
        public DayNightCycleData DayNightCycleData => jsonConfigData.dayNightCycleData;
        
        public override void Init(TextAsset asset = null)
        {
            base.Init(asset);
            if (_animationInfos.Count != 0) return;
            foreach (var animationInfo in jsonConfigData.playerConfig.AnimationInfos)
            {
                _animationInfos.Add(animationInfo.State, animationInfo);
            }
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            Debug.Log("JsonDataConfig is not support read from csv");
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
        
        public float GetBuffSize(CollectObjectBuffSize collectObjectBuffSize)
        {
            foreach (var buffSizeData in jsonConfigData.buffConstantData.buffSizeDataList)
            {
                if (buffSizeData.collectObjectBuffSize == collectObjectBuffSize)
                {
                    return buffSizeData.ratio;
                }
            }
            return 1f;
        }
    }

    [Serializable]
    public struct JsonConfigData
    {
        public PlayerConfigData playerConfig;
        public CollectData collectData;
        public DamageData damageData;
        public BuffConstantData buffConstantData;
        public GameConfigData gameConfig;
        public DayNightCycleData dayNightCycleData;
        public ChestCommonData chestCommonData;
    }

    
    [Serializable]
    public struct ChestCommonData
    {
        public float OpenSpeed;
        public Vector3 InitEulerAngles;
    }

    [Serializable]
    public struct PlayerConfigData
    {
        #region Camera

        public float TurnSpeed;
        public float MouseSpeed;
        public Vector3 Offset;

        #endregion
    
        #region Player

        public float SprintSpeedFactor;
        public float RotateSpeed;
        public float OnStairsSpeedRatioFactor;
        public float JumpSpeed;
        public float StairsJumpSpeed;
        public float GroundCheckRadius;
        public float StairsCheckDistance;
        public List<PropertyType> MaxProperties;
        public List<PropertyType> BaseProperties;
        public List<PropertyType> MinProperties;
        public float StrengthRecoveryPerSecond;
        public float RollForce;
        public float StepHeight; // 玩家可跨越的最大台阶高度
        public float StepCheckDistance; // 台阶检测距离
        public PlayerAttackData BaseAttackData;
        
        #endregion
        #region Animation
        
        public float AttackComboWindow; // 连招窗口时间
        public int AttackComboMaxCount; // 连招最大次数
        public List<AnimationInfo> AnimationInfos;
        
        #endregion
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

    [Serializable]
    public struct PlayerAttackData
    {
        public float attackAngle;
        public float attackRadius;
        public float minAttackHeight;
    }
    
    [Serializable]
    public struct BuffConstantData
    {
        public List<BuffSizeData> buffSizeDataList;
    }

    [Serializable]
    public struct BuffSizeData
    {
        public CollectObjectBuffSize collectObjectBuffSize;
        public float ratio;
    }
}