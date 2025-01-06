using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "RandomBuffConfig", menuName = "ScriptableObjects/RandomBuffConfig")]
    public class RandomBuffConfig : ConfigBase
    {
        [SerializeField]
        private List<RandomBuffData> randomBuffs = new List<RandomBuffData>();
        private readonly Dictionary<PropertyTypeEnum, List<RandomBuffData>> _randomCollectBuffs = new Dictionary<PropertyTypeEnum, List<RandomBuffData>>();
        
        public RandomBuffData GetRandomBuffData(int buffId)
        {
            return randomBuffs.Find(buff => buff.buffId == buffId);
        }
        
        public BuffData GetBuff(BuffExtraData extraData)
        {
            return GetRandomBuff(extraData.buffId);
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


        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            randomBuffs.Clear();

            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var randomBuff = new RandomBuffData
                {
                    buffId = int.Parse(row[0]),
                    propertyType = (PropertyTypeEnum)System.Enum.Parse(typeof(PropertyTypeEnum), row[1]),
                    duration = JsonUtility.FromJson<Range>(row[2]),
                    increaseDataList = JsonUtility.FromJson<List<RandomBuffIncreaseData>>(row[3]),
                    isCollectBuff = bool.Parse(row[4])
                };
                randomBuffs.Add(randomBuff);
            }

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
        }
    }
}