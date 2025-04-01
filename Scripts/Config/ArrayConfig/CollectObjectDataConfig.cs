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
        private readonly Dictionary<CollectObjectClass, HashSet<int>> _collectObjectDatas = new Dictionary<CollectObjectClass, HashSet<int>>();
        
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
            _collectObjectDatas.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var collectConfigData = new CollectObjectData();
                collectConfigData.id = int.Parse(row[0]);
                collectConfigData.itemId = int.Parse(row[1]);
                collectConfigData.weight = int.Parse(row[2]);    
                collectConfigData.description = row[3];
                collectConfigData.buffExtraData = JsonConvert.DeserializeObject<BuffExtraData>(row[4]);
                collectConfigData.collectObjectClass = Enum.Parse<CollectObjectClass>(row[5]);
                // collectConfigData.randomItems = JsonConvert.DeserializeObject<RandomItemsData>(row[6]);
                collectConfigDatas.Add(collectConfigData);
                if (!_collectObjectDatas.ContainsKey(collectConfigData.collectObjectClass))
                {
                    _collectObjectDatas.Add(collectConfigData.collectObjectClass, new HashSet<int>());
                }
                _collectObjectDatas[collectConfigData.collectObjectClass].Add(collectConfigData.id);
            }
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