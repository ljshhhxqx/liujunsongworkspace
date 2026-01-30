using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AOTScripts.Data;
using AOTScripts.Tool;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
using UnityEngine;
using AnimationInfo = HotUpdate.Scripts.Config.ArrayConfig.AnimationInfo;
using AnimationState = AOTScripts.Data.AnimationState;
using Random = UnityEngine.Random;

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
        public WeatherConstantData WeatherConstantData => jsonConfigData.weatherData;
        public GameModeData GameModeData => jsonConfigData.gameModeData;
        public BagCommonData BagCommonData => jsonConfigData.bagCommonData;
        public PropertyValueData PropertyValueData => jsonConfigData.propertyValueData;

        // public override void Init(TextAsset asset = null)
        // {
        //     base.Init(asset);
        //     if (_animationInfos.Count != 0) return;
        // }

        protected override void ReadFromJson(TextAsset textAsset)
        {
            Debug.Log($"Read JsonDataConfig---{textAsset.text}");
            jsonConfigData = BoxingFreeSerializer.JsonDeserialize<JsonConfigData>(textAsset.text);
            Debug.Log($"JsonDataConfig---{jsonConfigData.gameConfig.fixedSpacing}");
        }

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
        
        public DamageCalculateResultData GetDamage(float attackPower, float defense, float criticalRate, float criticalDamageRatio)
        {
            var damageReduction = defense / (defense + DamageData.defenseRatio);
            criticalRate = Mathf.Max(0f, Mathf.Min(1f, criticalRate));
            var isCritical = Random.Range(0f, 1f) < criticalRate;
            var damage = attackPower * (1f - damageReduction) * (isCritical ? criticalDamageRatio : 1f);
            var damageResult = new DamageCalculateResultData
            {
                Damage = damage,
                IsCritical = isCritical,
            };
            return damageResult;
        }

        public float GetQualityWeight(QualityType quality)
        {
            var qualityData = jsonConfigData.propertyValueData.qualityRatioData.First(q => q.qualityType == quality);
            return qualityData.weight;
        
        }

        public CollectObjectType GetCollectObjectType()
        {
            var config = CollectData;
            var sum = config.spawnAttackRatio + config.spawnHiddenRatio + config.spawnMoveRatio +
                      config.spawnAttackHiddenRatio + config.spawnMoveHiddenRatio + config.spawnAttackMoveRatio +
                      config.spawnAttackMoveHiddenRatio;
            var rand = Random.Range(0f, 1f);
            if (rand < config.spawnAttackRatio)
            {
                Debug.Log("【GetCollectObjectType】 Spawn Attack : " + rand );
                return CollectObjectType.Attack;
            }
            if (rand < config.spawnAttackRatio + config.spawnHiddenRatio)
            {
                Debug.Log("【GetCollectObjectType】 Spawn Hidden : " + rand);
                return CollectObjectType.Hidden;
            }
            if (rand < config.spawnAttackRatio + config.spawnHiddenRatio + config.spawnMoveRatio)
            {
                Debug.Log("【GetCollectObjectType】 Spawn Move : " + rand);
                return CollectObjectType.Move;
            }
            if (rand < config.spawnAttackRatio + config.spawnHiddenRatio + config.spawnMoveRatio + config.spawnAttackHiddenRatio)
            {
                Debug.Log("【GetCollectObjectType】 Spawn Attack Hidden : " + rand);
                return CollectObjectType.AttackHidden;
            }
            if (rand < config.spawnAttackRatio + config.spawnHiddenRatio + config.spawnMoveRatio + config.spawnAttackHiddenRatio + config.spawnMoveHiddenRatio)
            {
                Debug.Log("【GetCollectObjectType】 Spawn Move Hidden: " + rand);
                return CollectObjectType.MoveHidden;
            }
            if (rand < config.spawnAttackRatio + config.spawnHiddenRatio + config.spawnMoveRatio + config.spawnAttackHiddenRatio + config.spawnMoveHiddenRatio + config.spawnAttackMoveRatio)
            {
                Debug.Log("【GetCollectObjectType】 Spawn Attack Move : " + rand);
                return CollectObjectType.AttackMove;
            }

            if (rand < sum)
            {
                Debug.Log("【GetCollectObjectType】 Spawn Attack Move Hidden : " + rand);
                return CollectObjectType.AttackMoveHidden;
            }
            Debug.Log("【GetCollectObjectType】 Spawn None : " + rand);

            return CollectObjectType.None;
        }

        #region 装备生成器
        public EquipmentAttributeData GenerateEquipment(
            float totalGold,
            EquipmentPart part,
            QualityType quality,
            float passiveRatio = 1f,
            bool generateMain = true,
            bool generatePassive = true)
        {
            // var equipment = new EquipmentAttributeData();
            // var config = jsonConfigData.propertyValueData;
            // float qualityFactor = GetQualityFactor(quality, config);
            // float actualValue = totalGold * qualityFactor;
            //
            // (float mainBudget, float passiveBudget) = SplitBudget(actualValue, quality, passiveRatio);
            //
            // if (generateMain)
            // {
            //     equipment.mainAttributeList = GenerateMainAttributes(
            //         mainBudget, part, config.propertyWeightData, config.propertyIncreaseValue);
            // }
            //
            // if (generatePassive && quality != QualityType.Normal)
            // {
            //     equipment.passiveAttributeList = new List<AttributeIncreaseData>();
            //     equipment.passiveAttributeList.Add(GeneratePassiveAttribute(
            //         passiveBudget, part, equipment.mainAttributeList, 
            //         config.propertyWeightData, config.propertyIncreaseValue));
            // }
            //
            // return equipment;
            return default;
        }

        private (float main, float passive) SplitBudget(float total, QualityType quality, float passiveRatio)
        {
            var propertyData = jsonConfigData.propertyValueData;
            if (quality == QualityType.Normal)
                return (total, 0);

            var qualityData = jsonConfigData.propertyValueData.qualityRatioData.First(q => q.qualityType == quality);
            var variance = Random.Range(-propertyData.valueVariance, propertyData.valueVariance);
            passiveRatio = Mathf.Clamp( (1 - qualityData.mainAttributeRatio) * passiveRatio  * (1 + variance), qualityData.minPassiveAttributeRatio, 1 - qualityData.mainAttributeRatio);
            
            return (
                main: total * (1 - passiveRatio),
                passive: total * passiveRatio
            );
        }

        private List<AttributeIncreaseData> GenerateMainAttributes(
            float budget,
            EquipmentPart part,
            PropertyWeightData[] weightConfig,
            PropertyIncreaseValue[] valueConfig)
        {
            // var attributes = new List<AttributeIncreaseData>();
            // var partWeights = GetPartWeights(part, weightConfig);
            //
            // // 必须包含最高权重属性
            // var mustHave = partWeights.propertyWeightList
            //     .OrderByDescending(w => w.weight)
            //     .First();
            //
            // attributes.Add(GenerateProperty(ref budget, mustHave, valueConfig));
            //
            // // 生成其他属性
            // while (budget > 0 && attributes.Count < 2)
            // {
            //     var candidates = GetAvailableWeights(partWeights, attributes);
            //     if (candidates.Count == 0) break;
            //
            //     var selected = candidates.SelectByWeight();
            //     attributes.Add(GenerateProperty(ref budget, selected, valueConfig));
            // }
            //
            // return attributes;
            return null;
        }

        // private AttributeIncreaseData GeneratePassiveAttribute(
        //     float budget,
        //     EquipmentPart part,
        //     List<AttributeIncreaseData> mainAttributes,
        //     PropertyWeightData[] weightConfig,
        //     PropertyIncreaseValue[] valueConfig)
        // {
        //     if (budget <= 0) return default;
        //
        //     var partWeights = GetPartWeights(part, weightConfig);
        //     var forbiddenTypes = mainAttributes.Select(p => p.propertyType).ToHashSet();
        //     var forbiddenIncreaseTypes = mainAttributes.Select(p => p.buffIncreaseType).ToHashSet();
        //
        //     var availableWeights = partWeights.propertyWeightList
        //         .Where(w => 
        //             !forbiddenTypes.Contains(w.propertyType) && 
        //             !forbiddenIncreaseTypes.Contains(w.buffIncreaseType))
        //         .ToList();
        //
        //     if (availableWeights.Count == 0) return default;
        //
        //     var selected = availableWeights
        //         .ToDictionary(w => w, w => w.weight).SelectByWeight();
        //
        //     return GenerateProperty(budget, selected, valueConfig);
        // }

        // private AttributeIncreaseData GenerateProperty(ref float budget, PropertyWeight weight, 
        //     PropertyIncreaseValue[] valueConfig)
        // {
        //     var costPerUnit = GetCostPerUnit(weight, valueConfig);
        //     float maxValue = budget / costPerUnit;
        //     float actualValue = maxValue * Random.Range(0.7f, 1.0f);
        //     
        //     budget -= actualValue * costPerUnit;
        //     return CreateProperty(weight, actualValue);
        // }
        //
        // private AttributeIncreaseData GenerateProperty(float budget, PropertyWeight weight,
        //     PropertyIncreaseValue[] valueConfig)
        // {
        //     var costPerUnit = GetCostPerUnit(weight, valueConfig);
        //     float maxValue = budget / costPerUnit;
        //     if (maxValue < GetMinEffectiveValue(weight.propertyType, weight.buffIncreaseType))
        //         return default;
        //
        //     float actualValue = maxValue * Random.Range(0.7f, 1.0f);
        //     return CreateProperty(weight, actualValue);
        // }

        // 辅助方法
        private PropertyWeightData GetPartWeights(EquipmentPart part, PropertyWeightData[] config)
        {
            var weights = config.FirstOrDefault(w => w.equipmentPart == part);
            if (weights.Equals(default(PropertyWeightData)))
                throw new ArgumentException($"No weight config for part {part}");
            return weights;
        }

        private float GetQualityFactor(QualityType quality, PropertyValueData config)
        {
            var ratioData = config.qualityRatioData.FirstOrDefault(q => q.qualityType == quality);
            if (ratioData.Equals(default(QualityRatioData)))
                throw new ArgumentException($"No quality ratio for {quality}");
            return ratioData.ratio;
        }

        // private Dictionary<PropertyWeight, float> GetAvailableWeights(
        //     PropertyWeightData partWeights, 
        //     List<AttributeIncreaseData> existing)
        // {
        //     return partWeights.propertyWeightList
        //         .Where(w => !existing.Any(p => 
        //             p.propertyType == w.propertyType && 
        //             p.buffIncreaseType == w.buffIncreaseType))
        //         .ToDictionary(w => w, w => w.weight);
        // }

        private float GetCostPerUnit(PropertyWeight weight, PropertyIncreaseValue[] config)
        {
            var valueData = config.First(v => v.propertyType == weight.propertyType);
            return valueData.propertyIncreaseValueList
                .First(d => d.buffIncreaseType == weight.buffIncreaseType).value;
        }

        // private AttributeIncreaseData CreateProperty(PropertyWeight weight, float value)
        // {
        //     return new AttributeIncreaseData
        //     {
        //         propertyType = weight.propertyType,
        //         buffIncreaseType = weight.buffIncreaseType,
        //         increaseValue = value,
        //         buffOperationType = BuffOperationType.Add
        //     };
        // }

        private float GetMinEffectiveValue(PropertyTypeEnum type, BuffIncreaseType increaseType)
        {
            return increaseType switch
            {
                BuffIncreaseType.Base => 1f,
                BuffIncreaseType.Multiplier => 0.05f,
                BuffIncreaseType.Extra => 0.01f,
                _ => 0.1f
            };
        }
        // public bool Validate(
        //     EquipmentAttributeData equipment,
        //     float originalGold,
        //     EquipmentPart part,
        //     QualityType quality,
        //     PropertyValueData config,
        //     bool checkMain = true,
        //     bool checkPassive = true)
        // {
        //     float totalCost = 0f;
        //     bool mainValid = true;
        //     bool passiveValid = true;
        //
        //     if (checkMain)
        //     {
        //         totalCost += equipment.mainAttributeList?.Sum(p => GetPropertyCost(p, config)) ?? 0;
        //         mainValid = CheckCoreAttribute(equipment.mainAttributeList, part, config);
        //     }
        //
        //     if (checkPassive && equipment.passiveAttributeList != null)
        //     {
        //         totalCost += GetPropertyCost(equipment.passiveAttributeList.First(), config);
        //         passiveValid = CheckPassiveAttribute(equipment, config);
        //     }
        //
        //     float maxAllowed = originalGold * GetQualityFactor(quality, config) * 1.1f;
        //     return totalCost <= maxAllowed && mainValid && passiveValid;
        // }

        // private bool CheckCoreAttribute(List<AttributeIncreaseData> mainAttributes, EquipmentPart part, PropertyValueData config)
        // {
        //     var partWeights = config.propertyWeightData.First(w => w.equipmentPart == part);
        //     var maxWeight = partWeights.propertyWeightList.OrderByDescending(w => w.weight).First();
        //     return mainAttributes.Any(p => 
        //         p.propertyType == maxWeight.propertyType && 
        //         p.buffIncreaseType == maxWeight.buffIncreaseType);
        // }

        // private bool CheckPassiveAttribute(EquipmentAttributeData equipment, PropertyValueData config)
        // {
        //     if (equipment.passiveAttributeList == null) return true;
        //
        //     var mainTypes = equipment.mainAttributeList.Select(p => p.propertyType);
        //     var mainIncreaseTypes = equipment.mainAttributeList.Select(p => p.buffIncreaseType);
        //
        //     return !mainTypes.Contains(equipment.passiveAttributeList[0].propertyType) 
        //         && !mainIncreaseTypes.Contains(equipment.passiveAttributeList[0].buffIncreaseType);
        // }

        // private float GetPropertyCost(AttributeIncreaseData prop, PropertyValueData config)
        // {
        //     var increaseValue = config.propertyIncreaseValue
        //         .First(v => v.propertyType == prop.propertyType)
        //         .propertyIncreaseValueList
        //         .First(d => d.buffIncreaseType == prop.buffIncreaseType);
        //     
        //     return prop.increaseValue * increaseValue.value;
        // }
        
        #endregion
    }
    
    public struct DamageCalculateResultData
    {
        public bool IsCritical;
        public float Damage;
    }

    public struct DamageResultData
    {
        public uint HitterUid;
        public uint DefenderUid;
        public int Hitter;
        public int Defender;
        public DamageCalculateResultData DamageCalculateResult;
        public DamageType DamageType;
        public DamageCastType DamageCastType;
        //本次伤害是否被闪避
        public bool IsDodged;
        //本次伤害造成了目标的多少百分比的血量损失
        public float DamageRatio;
        public float HpRemainRatio;
        public bool IsDead;
        public bool IsCritical;
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
        [Header("属性数据")]
        public PropertyValueData propertyValueData;
        [Header("其他数据")]
        public OtherData otherData;
        // [Header("配置字符串")]
        // public ConfigString configString;
    }

    [Serializable]
    public struct PropertyValueData
    {
        [Header("属性权重数据")]
        public PropertyWeightData[] propertyWeightData;
        [Header("属性数据")]
        public PropertyIncreaseValue[] propertyIncreaseValue;
        [Header("品质比例数据")]
        public QualityRatioData[] qualityRatioData;
        [Header("误差")]
        public float valueVariance;
    }
    
    [Serializable]
    public struct QualityRatioData : IEquatable<QualityRatioData>
    {
        public QualityType qualityType;
        public float ratio;
        public float mainAttributeRatio;
        public float minPassiveAttributeRatio;
        public float weight;

        public bool Equals(QualityRatioData other)
        {
            return qualityType == other.qualityType && ratio.Equals(other.ratio) && mainAttributeRatio.Equals(other.mainAttributeRatio);
        }

        public override bool Equals(object obj)
        {
            return obj is QualityRatioData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)qualityType, ratio, mainAttributeRatio);
        }
    }

    //表示该装备部位每个属性的权重(没有该属性则不会被随机到)
    [Serializable]
    public struct PropertyWeightData : IEquatable<PropertyWeightData>
    {
        public EquipmentPart equipmentPart;
        public List<PropertyWeight> propertyWeightList;

        public bool Equals(PropertyWeightData other)
        {
            return equipmentPart == other.equipmentPart && Equals(propertyWeightList, other.propertyWeightList);
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyWeightData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)equipmentPart, propertyWeightList);
        }
    }
    
    [Serializable]
    public struct PropertyWeight : IEquatable<PropertyWeight>
    {
        public PropertyTypeEnum propertyType;
        public BuffIncreaseType buffIncreaseType;
        public float weight;

        public bool Equals(PropertyWeight other)
        {
            return propertyType == other.propertyType && buffIncreaseType == other.buffIncreaseType && weight.Equals(other.weight);
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyWeight other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)propertyType, (int)buffIncreaseType, weight);
        }
    }

    //表示每个属性在不同BuffIncreaseType下提升一点需要的价值
    [Serializable]
    public struct PropertyIncreaseValue
    {
        public PropertyTypeEnum propertyType;
        public List<PropertyIncreaseValueData> propertyIncreaseValueList;
    }

    [Serializable]
    public struct PropertyIncreaseValueData
    {
        public BuffIncreaseType buffIncreaseType;
        public float value;
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
        public float stateUpdateInterval;
        public float serverInputRate;
        public float inputThreshold;
        public float maxCommandAge;
        public float uiUpdateInterval;
        public float roundInterval;
        public float maxTraceDistance;
        public float maxViewAngle;
        public float obstacleCheckRadius;
        public float screenBorderOffset;

        #region 结盟和基地的相关

        public GameBaseData gameBaseData;
        public int minUnionPlayerCount;
        public float playerHpRatioToWarning;
        public string basePrefabName;
        public float noUnionTime;
        public string playerPrefabName;

        public float GetPlayerDeathTime(int score)
        {
            return 0.002f * score + 10f;
        }

        public BasePositionData GetBasePositionData(MapType mapType)
        {
            if (gameBaseData.basePositions == null || gameBaseData.basePositions.Length == 0)
            {
                Debug.LogWarning($"没有配置基地数据 {this.ToString()}");
                Debug.LogError($"没有配置基地数据 {gameBaseData.basePositions}");
                return null;
            }
            for (int i = 0; i < gameBaseData.basePositions.Length; i++)
            {
                var basePosition = gameBaseData.basePositions[i];
                if (basePosition.mapType == (int)mapType)
                {
                    return basePosition;
                }
            }
            Debug.LogError($"没有找到对应的基地数据 {mapType}");
            return default;
        }

        public Vector3 GetPlayerSpawnPosition(MapType mapType, Vector3[] existPositions)
        {
            var poss = GetBasePositionData(mapType).basePositions;
            if (existPositions.Length == 0) return poss[0];
            for (int i = 0; i < poss.Length; i++)
            {
                var pos = poss[i];
                if (existPositions.Contains(pos))
                {
                    continue;
                }
                return pos;
            }
            return default;
        }

        public CapsuleColliderConfig GetBaseColliderConfig()
        {
            return new CapsuleColliderConfig
            {
                Center = gameBaseData.baseCenter,
                Radius = gameBaseData.baseRadius,
                Height = gameBaseData.baseHeight,
                Direction = gameBaseData.baseDirection
            };
        }

        #endregion

        public Vector3 GetNearestBase(MapType mapType, Vector3 targetPosition)
        {
            var basePosition = GetBasePositionData(mapType);
            var nearestBase = basePosition.basePositions;
            if (nearestBase == null || nearestBase.Length == 0)
            {
                Debug.LogError($"没有配置基地数据 {basePosition.basePositions}");
                return default;
            }
            return nearestBase.GetNearestVector(targetPosition);
        }
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("游戏配置数据：");   
            sb.AppendLine($"groundSceneLayer: {groundSceneLayer}");
            sb.AppendLine($"syncTime: {syncTime}");
            sb.AppendLine($"gridSize: {gridSize}");
            sb.AppendLine($"safetyMargin: {safetyMargin}");
            sb.AppendLine($"fixedSpacing: {fixedSpacing}");
            sb.AppendLine($"warmupTime: {warmupTime}");
            sb.AppendLine($"developKey: {developKey}");
            sb.AppendLine($"developKeyValue: {developKeyValue}");
            sb.AppendLine($"stairSceneLayer: {stairSceneLayer}");
            sb.AppendLine($"safePosition: {safePosition}");
            sb.AppendLine($"safeHorizontalOffsetY: {safeHorizontalOffsetY}");
            sb.AppendLine($"groundMinDistance: {groundMinDistance}");
            sb.AppendLine($"groundMaxDistance: {groundMaxDistance}");
            sb.AppendLine($"maxSlopeAngle: {maxSlopeAngle}");
            sb.AppendLine($"stairsCheckDistance: {stairsCheckDistance}");
            sb.AppendLine($"tickRate: {tickRate}");
            //sb.AppendLine($"gameBaseData: {gameBaseData}");
            sb.AppendLine($"minUnionPlayerCount: {minUnionPlayerCount}");
            return sb.ToString();
        }
    }


    [Serializable]
    public struct GameBaseData
    {
        public float playerBaseHpRecoverRatioPerSec;
        public float playerBaseManaRecoverRatioPerSec;
        public BasePositionData[] basePositions;
        public Vector3 baseCenter;
        public float baseRadius;
        public float baseHeight;
        public int baseDirection;
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("游戏基地数据：");
            sb.AppendLine($"playerBaseHpRecoverRatioPerSec: {playerBaseHpRecoverRatioPerSec}");
            sb.AppendLine($"playerBaseManaRecoverRatioPerSec: {playerBaseManaRecoverRatioPerSec}");
            for (int i = 0; i < basePositions.Length; i++)
            {
                var basePosition = basePositions[i];
                sb.AppendLine($"basePosition[{i}]: {basePosition}");
            }
            sb.AppendLine($"baseCenter: {baseCenter}");
            sb.AppendLine($"baseRadius: {baseRadius}");
            sb.AppendLine($"baseHeight: {baseHeight}");
            sb.AppendLine($"baseDirection: {baseDirection}");
            return sb.ToString();
            
        }
    }

    [Serializable]
    public class BasePositionData
    {
        public int mapType;
        public Vector3[] basePositions;
        
        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine("游戏基地数据：");
            sb.AppendLine($"mapType: {mapType}");
            for (int i = 0; i < basePositions.Length; i++)
            {
                var basePositionVector = basePositions[i];
                sb.AppendLine($"basePosition[{i}]: {basePositionVector}");
            }
            return sb.ToString();
        }
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
        
        public Range rareBuffWeightRange;
        public Range legendaryBuffWeightRange;
    }
    
    [Serializable]
    public struct ChestCommonData
    {
        public float OpenSpeed;
        public Vector3 InitEulerAngles;
        public Vector3 EndEulerAngles;
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
        public LayerMask PlayerLayer;
        public float NoUnionMoreGoldRatio;
        public float NoUnionMoreScoreRatio;
        public float SpeedToVelocityRatio;
        public float MaxShopBuyDistance;
        
        #endregion
        #region Animation
        
        public float AttackComboWindow; // 连招窗口时间
        public int AttackComboMaxCount; // 连招最大次数
        
        #endregion
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