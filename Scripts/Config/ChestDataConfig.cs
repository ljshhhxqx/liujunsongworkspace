using System;
using System.Collections.Generic;
using System.Linq;
using Config;
using UnityEngine;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "ChestDataConfig", menuName = "ScriptableObjects/ChestDataConfig")]
    public class ChestDataConfig : ConfigBase
    { 
        [SerializeField]
        private List<ChestConfigData> chestConfigData;
        [SerializeField]
        private ChestCommonData chestCommonData;
        
        public ChestCommonData GetChestCommonData()
        {
            return chestCommonData;
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
        public ChestPropertyData ChestPropertyData;
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