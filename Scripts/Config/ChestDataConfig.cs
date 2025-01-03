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
        [SerializeField]
        private ChestCommonData chestCommonData;
        
        public ChestCommonData GetChestCommonData()
        {
            return chestCommonData;
        }
        
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

        protected override void ReadFromExcel(string filePath)
        {
        }

        protected override void ReadFromCsv(string filePath)
        {
        }
    }
    
    [Serializable]
    public struct ChestCommonData
    {
        public float OpenSpeed;
        public Vector3 InitEulerAngles;
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