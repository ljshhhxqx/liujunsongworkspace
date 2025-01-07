using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config.JsonConfig
{
    [CreateAssetMenu(fileName = "JsonDataConfig", menuName = "ScriptableObjects/JsonDataConfig")]
    public class JsonDataConfig : ConfigBase
    {
        [SerializeField] 
        private JsonConfigData jsonConfigData;
        [SerializeField]
        private bool canEdit;
        private readonly Dictionary<AnimationState, AnimationInfo> _animationInfos = new Dictionary<AnimationState, AnimationInfo>();

        public JsonConfigData JsonConfigData
        {
            set
            {
                if (canEdit)
                {
                    jsonConfigData = value;
                }
            }
        }

        public PlayerConfigData PlayerConfig => jsonConfigData.playerConfig;
        public CollectData CollectData => jsonConfigData.collectData;
        public DamageData DamageData => jsonConfigData.damageData;
        public BuffConstantData BuffConstantData => jsonConfigData.buffConstantData;
        public ChestCommonData ChestCommonData => jsonConfigData.chestCommonData;
        public GameConfigData GameConfig => jsonConfigData.gameConfig;
        public DayNightCycleData DayNightCycleData => jsonConfigData.dayNightCycleData;
        public WeatherConstantData WeatherConstantData => jsonConfigData.weatherData;

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
        
        public float GetDamage(float attackPower, float defense, float criticalRate, float criticalDamageRatio)
        {
            var damageReduction = defense / (defense + DamageData.defenseRatio);
            criticalRate = Mathf.Max(0f, Mathf.Min(1f, criticalRate));
            var isCritical = Random.Range(0f, 1f) < criticalRate;
            var damage = attackPower * (1f - damageReduction) * (isCritical? criticalDamageRatio : 1f);
            return damage;
        }
    }

    [Serializable]
    public struct JsonConfigData
    {
        [Header("玩家通用数据")]
        public PlayerConfigData playerConfig;
        [Header("收集品通用数据")]
        public CollectData collectData;
        [Header("伤害通用数据")]
        public DamageData damageData;
        [Header("增益通用数据")]
        public BuffConstantData buffConstantData;
        [Header("游戏通用数据")]
        public GameConfigData gameConfig;
        [Header("昼夜通用数据")]
        public DayNightCycleData dayNightCycleData;
        [Header("宝箱通用数据")]
        public ChestCommonData chestCommonData;
        [Header("天气通用数据")]
        public WeatherConstantData weatherData;
    }

    [Serializable]
    public struct WeatherConstantData
    {
        public float weatherChangeTime;
        public float maxTransitionDuration;
        public float minTransitionDuration;
    }

    [Serializable]
    public struct GameConfigData
    {
        [FormerlySerializedAs("GroundSceneLayer")] public LayerMask groundSceneLayer;
        [FormerlySerializedAs("SyncTime")] public float syncTime;
        [FormerlySerializedAs("SafetyMargin")] public float safetyMargin;
        [FormerlySerializedAs("FixedSpacing")] public float fixedSpacing;
        [FormerlySerializedAs("WarmupTime")] public float warmupTime;
        [FormerlySerializedAs("DevelopKey")] public string developKey;
        [FormerlySerializedAs("DevelopKeyValue")] public string developKeyValue;
        [FormerlySerializedAs("StairSceneLayer")] public LayerMask stairSceneLayer; 
        [FormerlySerializedAs("SafePosition")] public Vector3 safePosition;
        [FormerlySerializedAs("SafeHorizontalOffsetY")] public float safeHorizontalOffsetY;
    }
    

    [Serializable]
    public struct DamageData
    {
        //防御减伤比率
        public float defenseRatio;
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