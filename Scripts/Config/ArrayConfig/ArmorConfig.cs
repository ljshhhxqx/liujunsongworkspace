using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ArmorConfig", menuName = "ScriptableObjects/ArmorConfig")]
    public class ArmorConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<ArmorConfigData> armorConfigs = new List<ArmorConfigData>();
        
        public ArmorConfigData GetWeaponConfigData(int armorID)
        {
            foreach (var data in armorConfigs)
            {
                if (data.armorID == armorID)
                {
                    return data;
                }
            }

            Debug.LogError("WeaponConfigData not found for weaponID: " + armorID);
            return new ArmorConfigData();
        }

        public List<ArmorConfigData> GetRandomWeapons(ArmorType type)
        {
            var weapons = armorConfigs.FindAll(data => data.armorType == type);
            if (weapons.Count != 0)
            {
                return weapons;
            }
            Debug.LogError("WeaponConfigData not found for WeaponType: " + type);
            return new List<ArmorConfigData>();
        }

        public ArmorConfigData GetRandomWeapon(ArmorType type)
        {
            var weapons = GetRandomWeapons(type);
            if (weapons.Count == 0)
            {
                return new ArmorConfigData();
            }

            return weapons[UnityEngine.Random.Range(0, weapons.Count)];
        }
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            armorConfigs.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var weaponConfig = new ArmorConfigData();
                weaponConfig.armorID = int.Parse(data[0]);
                weaponConfig.armorType = (ArmorType) Enum.Parse(typeof(ArmorType), data[1]);
                weaponConfig.itemID = int.Parse(data[2]);
                weaponConfig.skillID = int.Parse(data[3]);
                weaponConfig.quality = (QualityType) Enum.Parse(typeof(QualityType), data[4]);
                armorConfigs.Add(weaponConfig);
            }
        }
    }
    
    [Serializable]
    public struct ArmorConfigData
    {
        public int armorID;
        public ArmorType armorType;
        public int itemID;
        public QualityType quality;
        public int skillID;
    }

    public enum ArmorType
    {
        None,
        Leather1,
        Leather2,
        Leather3,
        Light1,
        Light2,
        Light3,
        Light4,
        Light5,
        Heavy1,
        Heavy2,
        Heavy3,
        Heavy4,
        Heavy5,
    }
}