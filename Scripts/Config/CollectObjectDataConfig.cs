using System;
using System.Collections.Generic;
using UnityEngine;

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
    }

    [Serializable]
    public class CollectData
    {
        public int MaxWeight;
        public float ItemSpacing;
    }

}