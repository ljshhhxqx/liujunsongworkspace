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
        
        public ArmorConfigData GetArmorConfigData(int armorID)
        {
            foreach (var data in armorConfigs)
            {
                if (data.armorID == armorID)
                {
                    return data;
                }
            }

            Debug.LogError("ArmorConfigData not found for armorID: " + armorID);
            return new ArmorConfigData();
        }
        
        public ArmorConfigData GetArmorConfigByItemID(int itemID)
        {
            foreach (var data in armorConfigs)
            {
                if (data.itemID == itemID)
                {
                    return data;
                }
            }

            Debug.LogError("ArmorConfigData not found for itemID: " + itemID);
            return new ArmorConfigData();
        }

        public int GetArmorBattleConditionID(int itemID)
        {
            return GetArmorConfigByItemID(itemID).battleEffectConditionId;
        }
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            armorConfigs.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var weaponConfig = new ArmorConfigData();
                weaponConfig.armorID = int.Parse(data[0]);
                weaponConfig.itemID = int.Parse(data[1]);
                weaponConfig.equipmentPart = (EquipmentPart) Enum.Parse(typeof(EquipmentPart), data[1]);
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
        public EquipmentPart equipmentPart;
        public int itemID;
        public QualityType quality;
        public int skillID;
        public int battleEffectConditionId;
        public string battleEffectConditionDescription;
    }
}