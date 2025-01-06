using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "CollectObjectDataConfig", menuName = "ScriptableObjects/CollectObjectDataConfig")]
    public class CollectObjectDataConfig : ConfigBase
    { 
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