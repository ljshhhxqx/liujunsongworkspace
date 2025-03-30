using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;

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
                var armorConfigData = new ArmorConfigData();
                armorConfigData.armorID = int.Parse(data[0]);
                armorConfigData.itemID = int.Parse(data[1]);
                armorConfigData.armorName = data[2];
                armorConfigData.equipmentPart = Enum.Parse<EquipmentPart>(data[3]);
                armorConfigData.skillID = int.Parse(data[4]);
                armorConfigData.quality = Enum.Parse<QualityType>(data[5]);
                //weaponConfig.battleEffectConditionId = int.Parse(data[6]);
                armorConfigData.battleEffectConditionDescription = data[7];
                armorConfigs.Add(armorConfigData);
            }
        }
        
#if UNITY_EDITOR
        [SerializeField]
        private ConstantBuffConfig constantBuffConfig;
        [SerializeField]
        private BattleEffectConditionConfig battleEffectConditionConfig;


        [Button("将armor的条件加入到BattleEffectConditionConfig")]
        public void GenerateWeaponConditionExcel()
        {
            for (int i = 0; i < armorConfigs.Count; i++)
            {
                var data = armorConfigs[i];
                var condition = battleEffectConditionConfig.AnalysisDataString(data.battleEffectConditionDescription);
                if (condition.id == 0)
                {
                    Debug.Log("battleEffectConditionId not found for weaponID: " + data.armorID + "Start to generate a new one");
                    condition.id = battleEffectConditionConfig.GetConditionMaxId() + 1;
                    data.battleEffectConditionId = condition.id;
                    battleEffectConditionConfig.AddConditionData(condition);
                    armorConfigs[i] = data;
                    EditorUtility.SetDirty(this);
                }
            }
        }
#endif
    }
    
    [Serializable]
    public struct ArmorConfigData
    {
        public int armorID;
        public string armorName;
        public EquipmentPart equipmentPart;
        public int itemID;
        public QualityType quality;
        public int skillID;
        public int battleEffectConditionId;
        public string battleEffectConditionDescription;
    }
}