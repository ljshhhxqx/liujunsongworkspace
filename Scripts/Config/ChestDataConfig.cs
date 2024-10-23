using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Config
{
    [CreateAssetMenu(fileName = "ChestDataConfig", menuName = "ScriptableObjects/ChestDataConfig")]
    public class ChestDataConfig : ConfigBase
    { 
        [SerializeField]
        private List<ChestConfigData> chestConfigData;
        public List<ChestConfigData> ChestConfigData => chestConfigData;
        
        public ChestCommonData GetChestCommonData()
        {
            var data = chestConfigData.FirstOrDefault();
            if (data == null)
            {
                return default;
            }
            return data.ChestCommonData;
        }
        
        public ChestConfigData GetChestConfigData(ChestType chestType)
        {
            foreach (var data in chestConfigData)
            {
                if (data.ChestPropertyData.ChestType == chestType)
                {
                    return data;
                }
            }
            return null;    
        }
        
    }
    
    [Serializable]
    public struct ChestCommonData
    {
        public float OpenSpeed;
        public Vector3 InitEulerAngles;
    }

    [Serializable]
    public class ChestConfigData
    {
        public ChestCommonData ChestCommonData;
        public ChestPropertyData ChestPropertyData;
    }

    [Serializable]
    public struct ChestPropertyData
    {
        public ChestType ChestType;
        public PropertyType PropertyType;
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