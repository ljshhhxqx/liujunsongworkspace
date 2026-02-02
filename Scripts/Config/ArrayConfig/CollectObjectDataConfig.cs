using System;
using System.Collections;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using AOTScripts.Tool;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Map;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

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
    public interface IItemMovement
    {
        void Initialize(Transform ts, IColliderConfig colliderConfig, Func<Vector3, bool> insideMapCheck, Func<Vector3, IColliderConfig, bool> obstacleCheck);
        void UpdateMovement(float deltaTime);
        void ResetMovement();
        Vector3 GetPredictedPosition(float timeAhead);
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
        [Header("TouchItem")]
        public MapElementData mapElementData;
    }

    [Serializable]
    public struct MapElementData 
    {
        [Header("TouchItem")] 
        public Range touchWellRecoverHp;
        public Range touchRocketGainScore;
        public Range touchTrainGainScore;
        [Header("TouchItemTime")]
        public float touchWellTime;
        public float touchRocketTime;
        public float touchTrainTime;
        public float touchChestTime;
        [Header("WellExtraData")] 
        public float wellCount;
        public float wellCd;
        [Header("Position")]
        public Vector3 spawnRockerPosition;
        public Vector3 spawnTrainPosition;
        public MultiVector3[] rocketPositions;
        public MultiVector3[] trainPositions;
        public Range durationRange;

        public float GetTouchTime(ObjectType objectType)
        {
            return objectType switch
            {
                ObjectType.Well => touchWellTime,
                ObjectType.Rocket => touchRocketTime,
                ObjectType.Train => touchTrainTime,
                ObjectType.Chest => touchChestTime,
                _ => 0,
            };
        }
    }

    [Serializable]
    public struct MultiVector3
    {
        public Vector3[] vectors;
        public Quaternion rotation;
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