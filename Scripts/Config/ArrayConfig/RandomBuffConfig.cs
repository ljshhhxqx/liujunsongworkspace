using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "RandomBuffConfig", menuName = "ScriptableObjects/RandomBuffConfig")]
    public class RandomBuffConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<RandomBuffData> randomBuffs = new List<RandomBuffData>();
        private readonly Dictionary<BuffIncreaseType, HashSet<int>> _equipmentBuffs = new Dictionary<BuffIncreaseType, HashSet<int>>();
        private readonly Dictionary<PropertyTypeEnum, HashSet<int>> _randomCollectBuffs = new Dictionary<PropertyTypeEnum, HashSet<int>>();
        private readonly Dictionary<PropertyTypeEnum, HashSet<int>> _randomEquipmentBuffs = new Dictionary<PropertyTypeEnum, HashSet<int>>();
        public RandomBuffData GetRandomBuffData(int buffId)
        {
            return randomBuffs.Find(buff => buff.buffId == buffId);
        }
        
        public BuffData GetBuff(BuffExtraData extraData)
        {
            return GetRandomBuff(extraData.buffId);
        }
        
        public BuffExtraData GetEquipmentBuff(BuffIncreaseType buffIncreaseType)
        {
            if (_equipmentBuffs.Count == 0)
            {
                foreach (var randomBuff in randomBuffs)
                {
                    if (randomBuff.sourceType == BuffSourceType.Equipment)
                    {
                        if (!_equipmentBuffs.ContainsKey(randomBuff.mainIncreaseType))
                        {
                            _equipmentBuffs.Add(randomBuff.mainIncreaseType, new HashSet<int>());
                        }

                        _equipmentBuffs[randomBuff.mainIncreaseType].Add(randomBuff.buffId);
                    }
                }
            }
            var equipmentBuff = _equipmentBuffs[buffIncreaseType];
            var randomId = Random.Range(0, equipmentBuff.Count);
            if (!equipmentBuff.TryGetValue(randomId, out var buffId))
            {
                Debug.LogError($"Buff Id {randomId} not found");
                return default;
            }
            return new BuffExtraData
            {
                buffId = buffId,
                buffType = BuffType.Random,
            };
        }
        
        public BuffExtraData GetCollectBuff(PropertyTypeEnum propertyType)
        {
            if (_randomCollectBuffs.Count == 0)
            {
                foreach (var randomBuff in randomBuffs)
                {
                    if (randomBuff.sourceType == BuffSourceType.Collect)
                    {
                        if (!_randomCollectBuffs.ContainsKey(randomBuff.propertyType))
                        {
                            _randomCollectBuffs.Add(randomBuff.propertyType, new HashSet<int>());
                        }

                        _randomCollectBuffs[randomBuff.propertyType].Add(randomBuff.buffId);
                    }
                }
            }
            var propertyBuffs = _randomCollectBuffs[propertyType];
            var randomId = Random.Range(0, propertyBuffs.Count);
            if (!_randomCollectBuffs[propertyType].TryGetValue(randomId, out var buffId))
            {
                Debug.LogError($"Buff Id {randomId} not found");
                return default;
            }
            return new BuffExtraData
            {
                buffId = buffId,
                buffType = BuffType.Random,
            };
        }
        
        public BuffData GetRandomBuff(int buffId, CollectObjectBuffSize collectObjectBuffSize = CollectObjectBuffSize.Small)
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
                        increaseValue = increaseValue * BuffDataReaderWriter.GetBuffRatioBySize(collectObjectBuffSize),
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
            _randomEquipmentBuffs.Clear();
            _randomCollectBuffs.Clear();

            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var randomBuff = new RandomBuffData();
                randomBuff.buffId = int.Parse(row[0]);
                randomBuff.propertyType = (PropertyTypeEnum)System.Enum.Parse(typeof(PropertyTypeEnum), row[1]);
                randomBuff.duration = JsonConvert.DeserializeObject<Range>(row[2]);
                randomBuff.increaseDataList = JsonConvert.DeserializeObject<List<RandomBuffIncreaseData>>(row[3]);
                randomBuff.sourceType = (BuffSourceType)System.Enum.Parse(typeof(BuffSourceType), row[4]);
                randomBuff.mainIncreaseType = (BuffIncreaseType)System.Enum.Parse(typeof(BuffIncreaseType), row[5]);
                randomBuffs.Add(randomBuff);
            }

            foreach (var randomBuff in randomBuffs)
            {
                if (randomBuff.sourceType == BuffSourceType.Collect)
                {
                    if (!_randomCollectBuffs.ContainsKey(randomBuff.propertyType))
                    {
                        _randomCollectBuffs.Add(randomBuff.propertyType, new HashSet<int>());
                        
                    }

                    _randomCollectBuffs[randomBuff.propertyType].Add(randomBuff.buffId);
                }
                else if (randomBuff.sourceType == BuffSourceType.Equipment)
                {
                    if (!_randomEquipmentBuffs.ContainsKey(randomBuff.propertyType))
                    {
                        _randomEquipmentBuffs.Add(randomBuff.propertyType, new HashSet<int>());
                    }

                    if (!_equipmentBuffs.ContainsKey(randomBuff.mainIncreaseType))
                    {
                        _equipmentBuffs.Add(randomBuff.mainIncreaseType, new HashSet<int>());
                    }

                    _randomEquipmentBuffs[randomBuff.propertyType].Add(randomBuff.buffId);
                    _equipmentBuffs[randomBuff.mainIncreaseType].Add(randomBuff.buffId);
                }
            }
        
            EditorUtility.SetDirty(this);
            
        }
    }
}