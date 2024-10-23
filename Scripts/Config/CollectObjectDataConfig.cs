using System;
using System.Collections.Generic;
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
    }

    [Serializable]
    public class CollectObjectData
    {
        public CollectType CollectType;
        public CollectObjectClass CollectObjectClass;
        public int Weight;
        public float PropertyValue;
        public BuffExtraData BuffExtraData;
    }

    [Serializable]
    public class CollectData
    {
        public int MaxWeight;
        public float ItemSpacing = 0.5f;
        public int MaxGridItems = 10;
        public float ItemHeight = 1f;
        public float GridSize = 10f;
        public int OnceSpawnCount = 10;
    }

}