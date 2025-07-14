using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.CustomAttribute;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = System.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ChestDataConfig", menuName = "ScriptableObjects/ChestDataConfig")]
    public class ChestDataConfig : ConfigBase
    { 
        [ReadOnly]
        [SerializeField]
        private List<ChestPropertyData> chestConfigData;


        public ChestPropertyData RandomOne(float weight)
        {
            var totalWeight = chestConfigData.Sum(x => (int)x.randomItems.quality);
            var randomWeight = weight * totalWeight;
            var currentWeight = 0.0f;
            foreach (var chestData in chestConfigData)
            {
                currentWeight += (int)chestData.randomItems.quality;
                if (randomWeight <= currentWeight)
                {
                    return chestData;
                }
            }

            return default;
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            chestConfigData.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var chestData = new ChestPropertyData();
                chestData.chestId = int.Parse(row[0]);
                chestData.itemIds = JsonConvert.DeserializeObject<int[]>(row[1]);
                chestData.description = row[2];
                chestData.randomItems = JsonConvert.DeserializeObject<RandomItemsData>(row[3]);
                chestConfigData.Add(chestData);
            }
        }

        public ChestPropertyData GetChestConfigData(int chestDataChestId)
        {
            return chestConfigData.FirstOrDefault(x => x.chestId == chestDataChestId);
        }
    }

    [Serializable]
    public struct ChestPropertyData
    {
        public int chestId;
        public int[] itemIds;
        public string description;
        public RandomItemsData randomItems;
    }

    [Serializable]
    [JsonSerializable]
    public struct RandomItemsData
    {
        public int count;
        public QualityType quality;

        public static RandomItemsData Create(int count, QualityType quality)
        {
            return new RandomItemsData
            {
                count = count,
                quality = quality
            };
        }

        public static List<QualityType> GenerateQualityItems(RandomItemsData randomItems, float weight)
        {
            var results = new List<QualityType>();

            // 处理 count=1 的简单情况
            if (randomItems.count == 1)
            {
                results.Add(randomItems.quality);
                return results;
            }

            // 检查是否存在更低品质
            bool hasLowerQuality = randomItems.quality > QualityType.Normal;
    
            // 没有更低品质时直接填充当前品质
            if (!hasLowerQuality)
            {
                return Enumerable.Repeat(randomItems.quality, randomItems.count).ToList();
            }

            // 获取低一级品质并初始化随机数生成器
            QualityType lowerQuality = randomItems.quality - 1;
            Random random = new Random();
            bool containsOriginal = false;

            // 生成所有物品
            for (int i = 0; i < randomItems.count; i++)
            {
                // 根据权重随机选择品质
                QualityType selected = random.NextDouble() <= weight
                    ? randomItems.quality 
                    : lowerQuality;

                if (selected == randomItems.quality) containsOriginal = true;
                results.Add(selected);
            }

            // 强制保证至少包含一个原始品质
            if (!containsOriginal)
            {
                int replaceIndex = random.Next(0, randomItems.count);
                results[replaceIndex] = randomItems.quality;
            }

            return results;
        }
    }
}