using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "BuffDatabase", menuName = "Buff System/Buff Database")]
    public class BuffDatabase : ConfigBase
    {
        [SerializeField]
        private List<BuffData> buffs = new List<BuffData>();
        [SerializeField]
        private List<RandomBuffData> randomBuffs = new List<RandomBuffData>();
        [SerializeField]
        private BuffConstantData constantData;
        private readonly Dictionary<PropertyTypeEnum, List<RandomBuffData>> _randomCollectBuffs = new Dictionary<PropertyTypeEnum, List<RandomBuffData>>();

        public float GetBuffSize(CollectObjectBuffSize collectObjectBuffSize)
        {
            foreach (var buffSizeData in constantData.buffSizeDataList)
            {
                if (buffSizeData.collectObjectBuffSize == collectObjectBuffSize)
                {
                    return buffSizeData.ratio;
                }
            }
            return 1f;
        }

        public RandomBuffData GetRandomBuffData(int buffId)
        {
            return randomBuffs.Find(buff => buff.buffId == buffId);
        }
        
        public BuffData GetBuff(BuffExtraData extraData)
        {
            switch (extraData.buffType)
            {
                case BuffType.Constant:
                    return GetBuffData(extraData.buffId);
                case BuffType.Random:
                    return GetRandomBuff(extraData.buffId);
                default:
                    throw new Exception($"BuffType {extraData.buffType} not supported");
            }
        }

        public BuffExtraData GetCollectBuff(PropertyTypeEnum propertyType)
        {
            if (_randomCollectBuffs.Count == 0)
            {
                foreach (var randomBuff in randomBuffs)
                {
                    if (randomBuff.isCollectBuff)
                    {
                        if (!_randomCollectBuffs.ContainsKey(randomBuff.propertyType))
                        {
                            _randomCollectBuffs.Add(randomBuff.propertyType, new List<RandomBuffData>());
                        }

                        _randomCollectBuffs[randomBuff.propertyType].Add(randomBuff);
                    }
                }
            }
            var randomBuffDatas = _randomCollectBuffs[propertyType];
            var randomId = Random.Range(0, randomBuffDatas.Count);
            return new BuffExtraData
            {
                buffId = randomId,
                buffType = BuffType.Random,
            };
        }

        public BuffData GetRandomBuff(int buffId)
        {
            var randomBuff = randomBuffs.Find(buff => buff.buffId == buffId);
            if (randomBuff.buffId != 0)
            {
                var buffIncreaseData = new List<BuffIncreaseData>();
                for (int i = 0; i < randomBuff.increaseDataList.Count; i++)
                {
                    var increaseData = randomBuff.increaseDataList[i];
                    var increaseValue = increaseData.increaseValueRange.GetRandomValue();
                    buffIncreaseData.Add(new BuffIncreaseData
                    {
                        increaseType = increaseData.increaseType,
                        increaseValue = increaseValue
                    });
                }
                var buff = new BuffData
                {
                    buffId = randomBuff.buffId,
                    propertyType = randomBuff.propertyType,
                    duration = randomBuff.duration.GetRandomValue(),
                    increaseDataList = buffIncreaseData
                };
                return buff;
            }
            return new BuffData();
        }

        public BuffData GetBuffData(int buffId)
        {
            return buffs.Find(buff => buff.buffId == buffId);
        }

        protected override void ReadFromExcel(string filePath)
        {
        }

        protected override void ReadFromCsv(string filePath)
        {
        }
    }

    [Serializable]
    public struct BuffConstantData
    {
        public List<BuffSizeData> buffSizeDataList;
    }

    [Serializable]
    public struct BuffSizeData
    {
        public CollectObjectBuffSize collectObjectBuffSize;
        public float ratio;
    }
}