using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Serialization;

namespace Config
{
    [CreateAssetMenu(fileName = "CollectObjectDataConfig", menuName = "ScriptableObjects/CollectObjectDataConfig")]
    public class CollectObjectDataConfig : ConfigBase
    { 
        [SerializeField]
        private List<CollectObjectData> collectConfigDatas;
        [SerializeField]
        private CollectData collectData;
        public CollectData CollectData => collectData;
        public List<CollectObjectData> CollectConfigDatas => collectConfigDatas;
        
        public CollectObjectData GetCollectObjectData(CollectType collectType)
        {
            foreach (var collectConfigData in collectConfigDatas)
            {
                if (collectConfigData.CollectType == collectType)
                {
                    return collectConfigData;
                }
            }
            Debug.LogWarning($"Can't find collect object data for {collectType}");
            return null;
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
    }

    [Serializable]
    public class CollectObjectData
    {
        public CollectType CollectType;
        public CollectObjectClass CollectObjectClass;
        public int Weight;
        public BuffExtraData BuffExtraData;
    }

    [Serializable]
    public struct CollectData
    {
        public float ItemSpacing;
        public int MaxGridItems;
        public float ItemHeight;
        public float GridSize;
        public int OnceSpawnCount;
        public int OnceSpawnWeight;
    }

}