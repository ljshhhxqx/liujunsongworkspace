using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationInfo = HotUpdate.Scripts.Config.ArrayConfig.AnimationInfo;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config.JsonConfig
{
    [CreateAssetMenu(fileName = "JsonDataConfig", menuName = "ScriptableObjects/JsonDataConfig")]
    public class JsonDataConfig : ConfigBase
    {
        [SerializeField] 
        private JsonConfigData jsonConfigData;

#if UNITY_EDITOR
        [SerializeField]
        private bool canEdit;
#endif
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
        public GameModeData GameModeData => jsonConfigData.gameModeData;

        public override void Init(TextAsset asset = null)
        {
            base.Init(asset);
            if (_animationInfos.Count != 0) return;
            foreach (var animationInfo in jsonConfigData.playerConfig.AnimationInfos)
            {
                _animationInfos.Add(animationInfo.state, animationInfo);
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
        
        private Dictionary<PropertyTypeEnum, float> _maxProperties;
        private Dictionary<PropertyTypeEnum, float> _minProperties;
        private Dictionary<PropertyTypeEnum, float> _baseProperties;
        
        public Dictionary<PropertyTypeEnum, float> GetPlayerMaxProperties()
        {
            if (_maxProperties == null)
            {
                _maxProperties = new Dictionary<PropertyTypeEnum, float>();
                foreach (var propertyType in jsonConfigData.playerConfig.MaxProperties)
                {
                    _maxProperties.Add(propertyType.TypeEnum, propertyType.Value);
                }
            }
            return _maxProperties;
        }

        public Dictionary<PropertyTypeEnum, float> GetPlayerMinProperties()
        {
            if (_minProperties == null)
            {
                _minProperties = new Dictionary<PropertyTypeEnum, float>();
                foreach (var propertyType in jsonConfigData.playerConfig.MinProperties)
                {
                    _minProperties.Add(propertyType.TypeEnum, propertyType.Value);
                }
            }
            return _minProperties;
        }

        public Dictionary<PropertyTypeEnum, float> GetPlayerBaseProperties()
        {
            if (_baseProperties == null)
            {
                _baseProperties = new Dictionary<PropertyTypeEnum, float>();
                foreach (var propertyType in jsonConfigData.playerConfig.BaseProperties)
                {
                    _baseProperties.Add(propertyType.TypeEnum, propertyType.Value);
                }
            }
            return _baseProperties;
        }
        
        public float GetPlayerBaseProperty(PropertyTypeEnum propertyType)
        {
            return GetPlayerBaseProperties().GetValueOrDefault(propertyType);
        }
        
        public float GetPlayerMaxProperty(PropertyTypeEnum propertyType)
        {
            return GetPlayerMaxProperties().GetValueOrDefault(propertyType);
        }
        
        public float GetPlayerMinProperty(PropertyTypeEnum propertyType)
        {
            return GetPlayerMinProperties().GetValueOrDefault(propertyType);
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
        [Header("游戏模式通用数据")]
        public GameModeData gameModeData;
        [Header("其他数据")]
        public OtherData otherData;
    }

    [Serializable]
    public struct OtherData
    {
        public List<AnimationActionData> animationActionData;
    }

    [Serializable]
    public struct WeatherConstantData
    {
        public float weatherChangeTime;
        public float maxTransitionDuration;
        public float minTransitionDuration;
    }
    
    [Serializable]
    public struct GameModeData
    {
        public List<int> times;
        public List<int> scores;
    }
    
    [Serializable]
    public struct GameConfigData
    {
        public LayerMask groundSceneLayer;
        public float syncTime;
        public float safetyMargin;
        public float fixedSpacing;
        public float warmupTime;
        public string developKey;
        public string developKeyValue;
        public LayerMask stairSceneLayer; 
        public Vector3 safePosition;
        public float safeHorizontalOffsetY;
        public float groundMinDistance;
        public float groundMaxDistance;
        public float maxSlopeAngle;
        public float stairsCheckDistance;
        public float rotateSpeed;
        public float tickRate;
        public float inputThreshold;
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
    public struct AnimationActionData
    {
        public ActionType actionType;
        public AnimationState animationState;
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
        public List<PropertyType> MaxProperties;
        public List<PropertyType> BaseProperties;
        public List<PropertyType> MinProperties;
        public float RollForce;
        public PlayerAttackData BaseAttackData;
        public int InputBufferTick;
        
        #endregion
        #region Animation
        
        public float AttackComboWindow; // 连招窗口时间
        public int AttackComboMaxCount; // 连招最大次数
        public List<AnimationInfo> AnimationInfos;
        
        #endregion
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
        Collect,
        None
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