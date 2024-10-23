using System;
using System.Collections.Generic;
using Config;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "BuffDatabase", menuName = "Buff System/Buff Database")]
    public class BuffDatabase : ConfigBase
    {
        public List<BuffData> buffs = new List<BuffData>();
        public List<RandomBuffData> randomBuffs = new List<RandomBuffData>();

        public BuffData GetBuff(BuffExtraData extraData)
        {
            switch (extraData.buffType)
            {
                case BuffType.Constant:
                    return buffs.Find(buff => buff.buffId == extraData.buffId);
                case BuffType.Random:
                    return GetRandomBuff(extraData.buffId);
                default:
                    throw new Exception($"BuffType {extraData.buffType} not supported");
            }
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
            return default;
        }

        public BuffData? GetBuffData(int buffId)
        {
            return buffs.Find(buff => buff.buffId == buffId);
        }

        public RandomBuffData? GetRandomBuffData(int buffId)
        {
            return randomBuffs.Find(buff => buff.buffId == buffId);
        }
    }
}