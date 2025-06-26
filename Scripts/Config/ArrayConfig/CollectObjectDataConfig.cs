using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
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
        
        public CollectObjectData GetCollectObjectData(int configId)
        {
            foreach (var collectConfigData in collectConfigDatas)
            {
                if (collectConfigData.id == configId)
                {
                    return collectConfigData;
                }
            }
            Debug.LogWarning($"Can't find collect object data for {configId}");
            return new CollectObjectData();
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
                collectConfigData.collectObjectClass = Enum.Parse<CollectObjectClass>(row[5]);
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
}