using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Newtonsoft.Json;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "CollectObjectDataConfig", menuName = "ScriptableObjects/CollectObjectDataConfig")]
    public class CollectObjectDataConfig : ConfigBase
    { 
        [ReadOnly]
        [SerializeField]
        private List<CollectObjectData> collectConfigDatas;
        public List<CollectObjectData> CollectConfigDatas => collectConfigDatas;
        
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
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var collectConfigData = new CollectObjectData();
                collectConfigData.id = int.Parse(row[0]);
                collectConfigData.weight = int.Parse(row[1]);                
                collectConfigData.buffExtraData = JsonConvert.DeserializeObject<BuffExtraData>(row[2]);
                collectConfigData.buffSize = Enum.Parse<CollectObjectBuffSize>(row[3]);
                collectConfigData.collectObjectClass = Enum.Parse<CollectObjectClass>(row[4]);
                collectConfigData.isRandomBuff = bool.Parse(row[5]);
                collectConfigDatas.Add(collectConfigData);
            }
        }
    }

    [Serializable]
    public struct CollectObjectData
    {
        public int id;
        public int weight;
        public BuffExtraData buffExtraData;
        public CollectObjectBuffSize buffSize;
        public CollectObjectClass collectObjectClass;
        public bool isRandomBuff;
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
    
    public enum CollectObjectBuffSize
    {
        None,
        Small,
        Medium,
        Large,
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
        TreasureChest,
        Score,
        Buff,
    }
}