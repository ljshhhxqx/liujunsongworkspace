using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
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
        
        public Range attackPowerRange;
        public Range speedRange;
        public Range attackRange;
        public Range criticalRateRange;
        public Range criticalDamageRatioRange;
        public Range defenseRange;
        public Range explodeRange;
        public float explodeCriticalRate;
        public float explodeCriticalDamageRatio;
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
        Attack,
        Move,
        Hidden,
        
        AttackMove,
        AttackHidden,
        MoveHidden,
        AttackMoveHidden,
    }
}