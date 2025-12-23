using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "CollectObjectDataConfig", menuName = "ScriptableObjects/CollectObjectDataConfig")]
    public class CollectObjectDataConfig : ConfigBase
    { 
        [ReadOnly]
        [SerializeField]
        private List<CollectObjectData> collectConfigDatas;
        public Dictionary<int, CollectObjectData> CollectObjectDataDict { get; } = new Dictionary<int, CollectObjectData>();
        
        public CollectObjectData GetCollectObjectData(int configId)
        {
            if (CollectObjectDataDict.TryGetValue(configId, out var collectObjectData))
            {
                return collectObjectData;
            }

            foreach (var collectConfigData in collectConfigDatas)
            {
                if (collectConfigData.id == configId)
                {
                    CollectObjectDataDict.Add(configId, collectConfigData);
                    return collectConfigData;
                }
            }
            Debug.LogWarning($"Can't find collect object data for {configId}");
            return default;
        }

        public IEnumerable<CollectObjectData> GetCollectObjectDataWithCondition(Func<CollectObjectData, bool> predicate)
        {
            foreach (var collectConfigData in collectConfigDatas)
            {
                if (predicate(collectConfigData))
                {
                    yield return collectConfigData;
                }
            }
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            collectConfigDatas.Clear();
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var collectConfigData = new CollectObjectData();
                collectConfigData.id = int.Parse(row[0]);
                collectConfigData.itemId = int.Parse(row[1]);
                collectConfigData.weight = int.Parse(row[3]);    
                collectConfigData.description = row[2];
                collectConfigData.buffExtraData = JsonConvert.DeserializeObject<BuffExtraData[]>(row[4], jsonSerializerSettings)[0];
                collectConfigData.collectObjectClass = (CollectObjectClass)Enum.Parse(typeof(CollectObjectClass), row[5]);
                // collectConfigData.randomItems = JsonConvert.DeserializeObject<RandomItemsData>(row[6]);
                collectConfigDatas.Add(collectConfigData);
            }
        }

        public int GetItemId(int configId)
        {
            return GetCollectObjectData(configId).itemId;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct CollectObjectData
    {
        public int id;
        public int itemId;
        public string description;
        public int weight;
        public BuffExtraData buffExtraData;
        public CollectObjectClass collectObjectClass;
        public QualityType qualityType;
        //public RandomItemsData randomItems;
    }

    public enum MoveType
    {
        None,
        Bounced,
        Evasive,
        Periodic,
    }

    // 运动配置基类
    public interface IMovementConfig
    {
        MoveType MoveType { get; }
    }
    public interface IItemMovement
    {
        void Initialize(Transform ts, IColliderConfig colliderConfig, Func<Vector3, bool> insideMapCheck, Func<Vector3, IColliderConfig, bool> obstacleCheck);
        void UpdateMovement(float deltaTime);
        void ResetMovement();
        Vector3 GetPredictedPosition(float timeAhead);
    }
    [Serializable]
    public struct BouncingMovementConfig : IMovementConfig, IEquatable<BouncingMovementConfig>
    {
        [Header("弹跳参数")]
        public float bounceHeight;
        public float bounceSpeed;
        public float bounceDecay; // 每次弹跳高度衰减
        public float minBounceHeight;
        public Vector3 bounceDirection;
        public float groundLevel ;
        
        public MoveType MoveType => MoveType.Bounced;

        public bool Equals(BouncingMovementConfig other)
        {
            return bounceHeight.Equals(other.bounceHeight) && bounceSpeed.Equals(other.bounceSpeed) && bounceDecay.Equals(other.bounceDecay) && minBounceHeight.Equals(other.minBounceHeight) && bounceDirection.Equals(other.bounceDirection) && groundLevel.Equals(other.groundLevel);
        }

        public override bool Equals(object obj)
        {
            return obj is BouncingMovementConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(bounceHeight, bounceSpeed, bounceDecay, minBounceHeight, bounceDirection, groundLevel);
        }

        public static bool operator ==(BouncingMovementConfig left, BouncingMovementConfig right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(BouncingMovementConfig left, BouncingMovementConfig right)
        {
            return !left.Equals(right);
        }
    }
    [Serializable]
    public struct EvasiveMovementConfig : IMovementConfig, IEquatable<EvasiveMovementConfig>
    {
        [Header("逃避参数")]
        public float detectionRadius;
        public float escapeSpeed;
        public float wanderSpeed;
        public float minSafeDistance;
        public float directionChangeInterval;
        public float playerPredictionFactor; // 玩家移动预测
        public Vector3 wanderTarget;
    
        [Header("逃避行为")]
        public bool useObstacleAvoidance;
        public float avoidanceWeight;
        public bool canJump;
        public float jumpChance;
        public MoveType MoveType => MoveType.Evasive;

        public bool Equals(EvasiveMovementConfig other)
        {
            return detectionRadius.Equals(other.detectionRadius) && escapeSpeed.Equals(other.escapeSpeed) && wanderSpeed.Equals(other.wanderSpeed) && minSafeDistance.Equals(other.minSafeDistance) && directionChangeInterval.Equals(other.directionChangeInterval) && playerPredictionFactor.Equals(other.playerPredictionFactor) && wanderTarget.Equals(other.wanderTarget) && useObstacleAvoidance == other.useObstacleAvoidance && avoidanceWeight.Equals(other.avoidanceWeight) && canJump == other.canJump && jumpChance.Equals(other.jumpChance);
        }

        public override bool Equals(object obj)
        {
            return obj is EvasiveMovementConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(detectionRadius);
            hashCode.Add(escapeSpeed);
            hashCode.Add(wanderSpeed);
            hashCode.Add(minSafeDistance);
            hashCode.Add(directionChangeInterval);
            hashCode.Add(playerPredictionFactor);
            hashCode.Add(wanderTarget);
            hashCode.Add(useObstacleAvoidance);
            hashCode.Add(avoidanceWeight);
            hashCode.Add(canJump);
            hashCode.Add(jumpChance);
            return hashCode.ToHashCode();
        }

        public static bool operator ==(EvasiveMovementConfig left, EvasiveMovementConfig right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(EvasiveMovementConfig left, EvasiveMovementConfig right)
        {
            return !left.Equals(right);
        }
    }
    [Serializable]
    public struct PeriodicMovementConfig : IMovementConfig, IEquatable<PeriodicMovementConfig>
    {
        public PathType pathType;
        public float moveSpeed;
        public float amplitude;
        public float frequency;
        public Vector3 axisMultiplier;
        public Vector3[] waypoints;
        public bool loopWaypoints;
        public float timeCounter;
        
        public MoveType MoveType => MoveType.Periodic;

        public bool Equals(PeriodicMovementConfig other)
        {
            return pathType == other.pathType && moveSpeed.Equals(other.moveSpeed) && amplitude.Equals(other.amplitude) && frequency.Equals(other.frequency) && axisMultiplier.Equals(other.axisMultiplier) && Equals(waypoints, other.waypoints) && loopWaypoints == other.loopWaypoints && timeCounter.Equals(other.timeCounter);
        }

        public override bool Equals(object obj)
        {
            return obj is PeriodicMovementConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)pathType, moveSpeed, amplitude, frequency, axisMultiplier, waypoints, loopWaypoints, timeCounter);
        }

        public static bool operator ==(PeriodicMovementConfig left, PeriodicMovementConfig right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PeriodicMovementConfig left, PeriodicMovementConfig right)
        {
            return !left.Equals(right);
        }
    }
    public enum PathType
    {
        Horizontal,
        Vertical,
        Circular,
        Lissajous,
        Square,
        CustomWaypoints
    }

    [Serializable]
    public struct CollectData
    {
        public float itemSpacing;
        public int maxGridItems;
        public float itemHeight;
        public float gridSize;
        public int onceSpawnCount;
        public int onceSpawnWeight;

        public float spawnAttackRatio;
        public float spawnMoveRatio;
        public float spawnHiddenRatio;
        public float spawnAttackMoveRatio;
        public float spawnAttackHiddenRatio;
        public float spawnMoveHiddenRatio;
        public float spawnAttackMoveHiddenRatio;
        
        [Header("AttackItem")]
        public Range healthRange;
        public Range attackPowerRange;
        public Range attackCooldown;
        public Range bulletSpeedRange;
        public Range bulletLifeTimeRange;
        public Range attackRange;
        public Range criticalRateRange;
        public Range criticalDamageRatioRange;
        public Range defenseRange;
        public Range lifeTimeRange;
        public KeyframeData bulletFrameData;
        public KeyframeData attackFrameData;
        [Header("MoveItem")]
        public Range speedRange;
        public Range rotateSpeedRange;
        public Range patternAmplitudeRange;
        public Range patternFrequencyRange;
        public PeriodicMovementConfig commonPeriodicMovementConfig;
        public EvasiveMovementConfig commonEvasiveMovementConfig;
        public BouncingMovementConfig commonBouncingMovementConfig;
        [Header("HiddenItem")]
        public Range translucenceRange;
        public Range mysteryTimeRange;
        public Range translucenceTimeRange;
        [Header("ExplodeItem")]
        public Range explodeRange;
        public float explodeCriticalRate;
        public float explodeCriticalDamageRatio;
    }
    
    public class MovementConfigLink
    {
        public IMovementConfig MovementConfig;
        public IItemMovement ItemMovement;
    }

    /// <summary>
    /// 拾取者枚举
    /// </summary>
    public enum PickerType
    {
        Player,
        Computer,
    }

    public enum CollectObjectClass
    {
        Score,
        Gold,
        Buff,
    }

    public enum CollectObjectType
    {
        None,
        Attack,
        Move,
        Hidden,
        
        AttackMove,
        AttackHidden,
        MoveHidden,
        AttackMoveHidden,
    }
}