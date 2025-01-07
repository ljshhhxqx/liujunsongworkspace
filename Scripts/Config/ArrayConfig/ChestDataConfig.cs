using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ChestDataConfig", menuName = "ScriptableObjects/ChestDataConfig")]
    public class ChestDataConfig : ConfigBase
    { 
        [ReadOnly]
        [SerializeField]
        private List<ChestPropertyData> chestConfigData;
        
        public ChestPropertyData GetChestConfigData(ChestType chestType)
        {
            foreach (var data in chestConfigData)
            {
                if (data.ChestType == chestType)
                {
                    return data;
                }
            }
            Debug.LogError("Can not find chest data by chest type: " + chestType);
            return new ChestPropertyData();    
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            chestConfigData.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var chestData = new ChestPropertyData();
                chestData.ChestType = (ChestType)Enum.Parse(typeof(ChestType), row[0]);
                chestData.BuffExtraData = JsonConvert.DeserializeObject<BuffExtraData>(row[1]);
                chestConfigData.Add(chestData);
            }
        }
    }

    [Serializable]
    public struct ChestPropertyData
    {
        public ChestType ChestType;
        public BuffExtraData BuffExtraData;
    }

    public enum ChestType
    {
        None,
        Speed,
        Attack,
        Dash,
        Strength,
        Score,
    }
}