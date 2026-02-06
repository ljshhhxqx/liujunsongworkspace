using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
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
            var totalWeight = chestConfigData.Sum(x =>
            {
                var chestRandomData = BoxingFreeSerializer.JsonConvertDeserialize<RandomItemsData>(x.randomItems);
                return (int)chestRandomData.quality;
            });
            var randomWeight = weight * totalWeight;
            var currentWeight = 0.0f;
            foreach (var chestData in chestConfigData)
            {
                var chestRandomData = BoxingFreeSerializer.JsonConvertDeserialize<RandomItemsData>(chestData.randomItems);
                
                currentWeight += (int)chestRandomData.quality;
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
                chestData.itemIds = (int[])JsonConvert.DeserializeObject(row[1], typeof(int[]));
                chestData.description = row[2];
                chestData.randomItems = row[3];
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
        public string randomItems;
    }
}