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
        public float Weight;
        
    }

}