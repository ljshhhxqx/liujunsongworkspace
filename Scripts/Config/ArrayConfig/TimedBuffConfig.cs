using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "TimedBuffConfig", menuName = "ScriptableObjects/TimedBuffConfig")]
    public class TimedBuffConfig : ConfigBase
    {
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            
        }
    }

    [Serializable]
    public struct TimedBuffData
    {
        public int buffId;
        public PropertyTypeEnum propertyType;
        public Range duration;
        public BuffSourceType sourceType;
        public BuffIncreaseType increaseType;
        public Range increaseRange;
        public bool isPermanent;
    }
}