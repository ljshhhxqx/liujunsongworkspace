using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "ChestDataConfig", menuName = "ScriptableObjects/ChestDataConfig")]
    public class ChestDataConfig : ConfigBase
    { 
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
            for (int i = 1; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var chestData = new ChestPropertyData
                {
                    ChestType = (ChestType)Enum.Parse(typeof(ChestType), row[0]),
                    BuffExtraData = JsonUtility.FromJson<BuffExtraData>(row[1])
                };
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