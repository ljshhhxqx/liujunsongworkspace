using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;
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
        public BagCommonData BagCommonData => jsonConfigData.bagCommonData;

        // public override void Init(TextAsset asset = null)
        // {
        //     base.Init(asset);
        //     if (_animationInfos.Count != 0) return;
        // }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            Debug.Log("JsonDataConfig is not support read from csv");
        }

        public AnimationInfo GetAnimationInfo(AnimationState animationState)
        {
            return _animationInfos.GetValueOrDefault(animationState);
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
        [Header("背包数据")]
        public BagCommonData bagCommonData;
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
        public float gridSize;
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
        public float tickRate;
        public float inputThreshold;
        public float maxCommandAge;
        public float uiUpdateInterval;
    }

    [Serializable]
    public struct DamageData
    {
        //防御减伤比率
        public float defenseRatio;
        // 增幅反应 = 其他伤害 * ((amplificationRatio × 元素精通) / (元素精通 + elementalMasteryBuff) + 1)
        public float amplificationRatio;
        public float elementalMasteryBuff;
        // 剧变反应 = 反应基础伤害 * 等级系数 * (1 + (16*元素精通/(元素精通+2000)))

        public float playerGaugeUnitRatio;
        public float enemyGaugeUnitRatio;
        public float environmentGaugeUnitRatio;
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
        public float RollForce;
        public int InputBufferTick;
        
        #endregion
        #region Animation
        
        public float AttackComboWindow; // 连招窗口时间
        public int AttackComboMaxCount; // 连招最大次数
        
        #endregion
    }

    public enum PlayerEnvironmentState
    {
        InAir,
        OnGround,
        OnStairs,
        Swimming,
    }

    [Flags]
    public enum AnimationState : short
    {
        Idle = 1 << 0,
        Move = 1 << 1,
        Sprint = 1 << 2,
        Jump = 1 << 3,
        SprintJump = 1 << 4,
        Roll = 1 << 5,
        Falling = 1 << 6,
        Landed = 1 << 7,
        Attack = 1 << 8,
        Dead = 1 << 9,
        Hit = 1 << 10,
        Collect = 1 << 11,
        MoveBlend = Idle | Move | Sprint,
        JumpBlend = Jump | Sprint,
        None = 0,
    }
    
    [Serializable]
    public struct BuffConstantData
    {
        public List<BuffSizeData> buffSizeDataList;
    }

    [Serializable]
    public struct BagCommonData
    {
        public int maxBagCount;
        public int maxStack;
    }

    [Serializable]
    public struct BuffSizeData
    {
        public CollectObjectBuffSize collectObjectBuffSize;
        public float ratio;
    }
}