using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using OfficeOpenXml;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ArmorConfig", menuName = "ScriptableObjects/ArmorConfig")]
    public class ArmorConfig : ConfigBase
    {
        //[ReadOnly]
        [SerializeField]
        private List<ArmorConfigData> armorConfigs = new List<ArmorConfigData>();

        public Dictionary<int, ArmorConfigData> ArmorConfigs { get; } = new Dictionary<int, ArmorConfigData>();

        public ArmorConfigData GetWeaponConfigData(int armorID)
        {
            if (ArmorConfigs.TryGetValue(armorID, out var armorConfigData))
            {
                return armorConfigData;
            }
            foreach (var data in armorConfigs)
            {
                if (data.armorID == armorID)
                {
                    ArmorConfigs.Add(armorID, data);
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
                armorConfigData.battleEffectConditionId = int.Parse(data[6]);
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
                if (data.battleEffectConditionDescription == "0")
                {
                    continue;
                }
                var isTrueData = battleEffectConditionConfig.AnalysisDataString(data.battleEffectConditionDescription, out var conditionConfigData);
                if (isTrueData)
                {
                    Debug.Log("battleEffectConditionId not found for weaponID: " + data.armorID + "Start to generate a new one");
                    conditionConfigData.id = battleEffectConditionConfig.GetConditionMaxId() + 1;
                    data.battleEffectConditionId = conditionConfigData.id;
                    battleEffectConditionConfig.AddConditionData(conditionConfigData);
                    armorConfigs[i] = data;
                }
            }
            EditorUtility.SetDirty(this);
        }
        
        [Button("将scriptable对象写入excel")]
        public void WriteToExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            jsonSerializerSettings.Converters.Add(new StringEnumConverter());
            var excel = Path.Combine(excelAssetReference.Path, $"{configName}.xlsx");
            using (var package = new ExcelPackage(new FileInfo(excel)))
            {
                var worksheet = package.Workbook.Worksheets[0]; // 假设数据在第一个工作表
                int rowCount = worksheet.Dimension.Rows;
                
                const int idCol = 1;
                const int itemIdCol = 2;
                const int armorNameCol = 3;
                const int equipmentPartCol = 4;
                const int skillIdCol = 5;
                const int qualityCol = 6;
                const int battleEffectConditionIdCol = 7;
                const int battleEffectConditionDescriptionCol = 8;
                int row = 0;
                var existingIds = new HashSet<int>();
                for (row = 3; row <= rowCount; row++)
                {
                    //var value = worksheet.Cells[row, idCol].GetValue<double>();
                    int buffId = (int)worksheet.Cells[row, idCol].GetValue<double>();
                    existingIds.Add(buffId);
                }

                try
                {
                    // 从第 2 行开始（跳过表头）
                    var newRow = 3;
                    foreach (var configData in armorConfigs)
                    {
                        worksheet.Cells[newRow, idCol].Value = configData.armorID;
                        worksheet.Cells[newRow, itemIdCol].Value = configData.itemID;
                        worksheet.Cells[newRow, armorNameCol].Value = configData.armorName;
                        worksheet.Cells[newRow, equipmentPartCol].Value = configData.equipmentPart.ToString();
                        worksheet.Cells[newRow, skillIdCol].Value = configData.skillID;
                        worksheet.Cells[newRow, qualityCol].Value = configData.quality.ToString();
                        worksheet.Cells[newRow, battleEffectConditionIdCol].Value = configData.battleEffectConditionId;
                        worksheet.Cells[newRow, battleEffectConditionDescriptionCol].Value = configData.battleEffectConditionDescription;

                        newRow++;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error in {row} ");
                    throw;
                }

                // 保存文件
                package.Save();
                Debug.Log("Equipment table updated successfully!");
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