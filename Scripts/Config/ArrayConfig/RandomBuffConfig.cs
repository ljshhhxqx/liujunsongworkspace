using System;
using System.Collections.Generic;
using AOTScripts.Tool;
using HotUpdate.Scripts.Tool;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "RandomBuffConfig", menuName = "ScriptableObjects/RandomBuffConfig")]
    public class RandomBuffConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<RandomBuffData> randomBuffs = new List<RandomBuffData>();
        private Dictionary<BuffIncreaseType, HashSet<int>> _equipmentBuffs;
        private Dictionary<PropertyTypeEnum, HashSet<int>> _randomCollectBuffs;
        private Dictionary<PropertyTypeEnum, HashSet<int>> _randomEquipmentBuffs;
        private HashSet<BuffIncreaseType> _equipmentBuffTypes = new HashSet<BuffIncreaseType>();

        public HashSet<BuffIncreaseType> EquipmentBuffTypes
        {
            get
            {
                if (_equipmentBuffTypes.Count == 0)
                {
                    foreach (var randomBuff in randomBuffs)
                    {
                        if (randomBuff.sourceType == BuffSourceType.Equipment)
                        {
                            _equipmentBuffTypes.Add(randomBuff.mainIncreaseType);
                        }
                    }
                }
                return _equipmentBuffTypes;
            }
        }

        public Dictionary<BuffIncreaseType, HashSet<int>> EquipmentBuffs
        {
            get
            {
                if (_equipmentBuffs == null || _equipmentBuffs.Count == 0)
                {
                    _equipmentBuffs = new Dictionary<BuffIncreaseType, HashSet<int>>();
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
                return _equipmentBuffs;
            }
        }
        
        public Dictionary<PropertyTypeEnum, HashSet<int>> RandomCollectBuffs
        {
            get
            {
                if (_randomCollectBuffs == null || _randomCollectBuffs.Count == 0)
                {
                    _randomCollectBuffs = new Dictionary<PropertyTypeEnum, HashSet<int>>();
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
                return _randomCollectBuffs;
            }
        }
        
        public Dictionary<PropertyTypeEnum, HashSet<int>> RandomEquipmentBuffs
        {
            get
            {
                if (_randomEquipmentBuffs == null || _randomEquipmentBuffs.Count == 0)
                {
                    _randomEquipmentBuffs = new Dictionary<PropertyTypeEnum, HashSet<int>>();
                    foreach (var randomBuff in randomBuffs)
                    {
                        if (randomBuff.sourceType == BuffSourceType.Equipment)
                        {
                            if (!_randomEquipmentBuffs.ContainsKey(randomBuff.propertyType))
                            {
                                _randomEquipmentBuffs.Add(randomBuff.propertyType, new HashSet<int>());
                            }

                            _randomEquipmentBuffs[randomBuff.propertyType].Add(randomBuff.buffId);
                        }
                    }
                }
                return _randomEquipmentBuffs;
            }
        }

        public RandomBuffData GetRandomBuffData(int buffId)
        {
            return randomBuffs.Find(buff => buff.buffId == buffId);
        }
        
        public BuffData GetBuff(BuffExtraData extraData, float weight = 1)
        {
            return GetRandomBuff(extraData.buffId, weight);
        }

        public BuffExtraData GetEquipmentBuffNoType()
        {
            var types = EquipmentBuffTypes.RandomSelect();
            while (types == BuffIncreaseType.None || types == BuffIncreaseType.Current || types == BuffIncreaseType.Max)
            {
                types = EquipmentBuffTypes.RandomSelect();
            }
            return GetEquipmentBuff(types);
        }

        public BuffExtraData GetEquipmentBuff(BuffIncreaseType buffIncreaseType)
        {
            if (!EquipmentBuffs.TryGetValue(buffIncreaseType, out HashSet<int> equipmentBuff))
            {
                Debug.LogError($"Buff Increase Type {buffIncreaseType} not found in EquipmentBuffs");
                return default;
            }
            var randomId = equipmentBuff.RandomSelect();
            return new BuffExtraData
            {
                buffId = randomId,
                buffType = BuffType.Random,
            };
        }
        
        public BuffExtraData GetCollectBuff(PropertyTypeEnum propertyType)
        {
            var propertyBuffs = RandomCollectBuffs[propertyType];
            var randomId = Random.Range(0, propertyBuffs.Count);
            if (!RandomCollectBuffs[propertyType].TryGetValue(randomId, out var buffId))
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
        
        public BuffData GetRandomBuff(int buffId, float weight)
        {
            var randomBuff = randomBuffs.Find(buff => buff.buffId == buffId);
            if (randomBuff.buffId != 0)
            {
                var buffIncreaseData = new List<BuffIncreaseData>();
                for (int i = 0; i < randomBuff.increaseDataList.Count; i++)
                {
                    var increaseData = randomBuff.increaseDataList[i];
                    float increaseValue = 0; 
                    if (Mathf.Approximately(weight, 1))
                    {
                        increaseValue = increaseData.increaseValueRange.GetRandomValue();
                    }
                    else
                    {
                        increaseValue = increaseData.increaseValueRange.GetRandomByWeight(weight);
                    }

                    buffIncreaseData.Add(new BuffIncreaseData
                    {
                        increaseType = increaseData.increaseType,
                        increaseValue = increaseValue,
                    });
                }
                var buff = new BuffData
                {
                    buffId = randomBuff.buffId,
                    propertyType = randomBuff.propertyType,
                    duration = randomBuff.duration.GetRandomByWeight(weight),
                    increaseDataList = buffIncreaseData
                };
                Debug.Log($"GetRandomBuffData: {buff.buffId} {buff.propertyType} {buff.duration} {buff.increaseDataList[0].ToString()}");
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
                var randomBuff = new RandomBuffData();
                randomBuff.buffId = int.Parse(row[0]);
                randomBuff.propertyType = (PropertyTypeEnum)Enum.Parse(typeof(PropertyTypeEnum), row[1]);
                randomBuff.duration = JsonConvert.DeserializeObject<Range>(row[2]);
                randomBuff.increaseDataList = JsonConvert.DeserializeObject<List<RandomBuffIncreaseData>>(row[3]);
                randomBuff.sourceType = (BuffSourceType)Enum.Parse(typeof(BuffSourceType), row[4]);
                randomBuff.mainIncreaseType = (BuffIncreaseType)Enum.Parse(typeof(BuffIncreaseType), row[5]);
                randomBuffs.Add(randomBuff);
            }
            
        }
    }
}