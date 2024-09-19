using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "BuffDatabase", menuName = "Buff System/Buff Database")]
    public class BuffDatabase : ScriptableObject
    {
        [Serializable]
        public struct BuffData
        {
            public BuffType buffType;
            public PropertyTypeEnum propertyTypeEnum;
            public float duration;
            public float effectStrength;
        }

        [Serializable]
        public struct RandomBuffData
        {
            public BuffType buffType;
            public PropertyTypeEnum propertyTypeEnum;
            public Range durationRange;
            public Range effectStrengthRange;
        }

        public List<BuffData> buffs = new List<BuffData>();
        public List<RandomBuffData> randomBuffs = new List<RandomBuffData>();

        public BuffData? GetBuffData(BuffType buffType)
        {
            return buffs.Find(buff => buff.buffType == buffType);
        }

        public RandomBuffData? GetRandomBuffData(BuffType buffType)
        {
            return randomBuffs.Find(buff => buff.buffType == buffType);
        }
    }
}