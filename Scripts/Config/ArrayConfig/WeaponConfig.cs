using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using OfficeOpenXml;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "WeaponConfig", menuName = "ScriptableObjects/WeaponConfig")]
    public class WeaponConfig : ConfigBase
    {
        //[ReadOnly]
        [SerializeField]
        private List<WeaponConfigData> weaponConfigData;

        public Dictionary<int, WeaponConfigData> WeaponConfigDataDict { get; } = new Dictionary<int, WeaponConfigData>();

        public WeaponConfigData GetWeaponConfigData(int weaponID)
        {
            if (WeaponConfigDataDict.TryGetValue(weaponID, out var configData))
                return configData;
            foreach (var data in weaponConfigData)
            {
                if (data.weaponID == weaponID)
                {
                    WeaponConfigDataDict.Add(weaponID, data);
                    return data;
                }
            }

            Debug.LogError("WeaponConfigData not found for weaponID: " + weaponID);
            return new WeaponConfigData();
        }

        public List<WeaponConfigData> GetRandomWeapons(WeaponType type)
        {
            var weapons = weaponConfigData.FindAll(data => data.weaponType == type);
            if (weapons.Count != 0)
            {
                return weapons;
            }
            Debug.LogError("WeaponConfigData not found for WeaponType: " + type);
            return new List<WeaponConfigData>();
        }
        
        public WeaponConfigData GetWeaponConfigByItemID(int itemID)
        {
            foreach (var data in weaponConfigData)
            {
                if (data.itemID == itemID)
                {
                    return data;
                }
            }

            Debug.LogError("ArmorConfigData not found for itemID: " + itemID);
            return new WeaponConfigData();
        }

        public int GetWeaponBattleConditionID(int itemID)
        {
            return GetWeaponConfigByItemID(itemID).battleEffectConditionId;
        }

        public WeaponConfigData GetRandomWeapon(WeaponType type)
        {
            var weapons = GetRandomWeapons(type);
            if (weapons.Count == 0)
            {
                return new WeaponConfigData();
            }

            return weapons[Random.Range(0, weapons.Count)];
        }
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            weaponConfigData.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var weaponConfig = new WeaponConfigData();
                weaponConfig.weaponID = int.Parse(data[0]);
                weaponConfig.itemID = int.Parse(data[1]);
                weaponConfig.weaponName = data[2];
                weaponConfig.weaponType = Enum.Parse<WeaponType>(data[3]);
                weaponConfig.skillID = int.Parse(data[4]);
                weaponConfig.quality = Enum.Parse<QualityType>(data[5]);
                weaponConfig.battleEffectConditionId = int.Parse(data[6]);
                weaponConfig.battleEffectConditionDescription = data[7];
                weaponConfigData.Add(weaponConfig);
            }
        }
        
        #if UNITY_EDITOR
        
        [SerializeField]
        private ConstantBuffConfig constantBuffConfig;
        [SerializeField]
        private BattleEffectConditionConfig battleEffectConditionConfig;

        [Button("将Weapon的条件加入到BattleEffectConditionConfig")]
        public void GenerateWeaponConditionExcel()
        {
            for (int i = 0; i < weaponConfigData.Count; i++)
            {
                var data = weaponConfigData[i];
                if (data.battleEffectConditionDescription == "0")
                {
                    continue;
                }
                var isTrueParam = battleEffectConditionConfig.AnalysisDataString(data.battleEffectConditionDescription, out var conditionConfigData);
                if (isTrueParam)
                {
                    Debug.Log("battleEffectConditionId not found for weaponID: " + data.weaponID+ "Start to generate a new one");
                    conditionConfigData.id = battleEffectConditionConfig.GetConditionMaxId() + 1;
                    Debug.Log("New battleEffectConditionId: " + conditionConfigData.id);
                    data.battleEffectConditionId = conditionConfigData.id;
                    battleEffectConditionConfig.AddConditionData(conditionConfigData);
                    weaponConfigData[i] = data;
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
            jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            var excel = Path.Combine(excelAssetReference.Path, $"{configName}.xlsx");
            using (var package = new ExcelPackage(new FileInfo(excel)))
            {
                var worksheet = package.Workbook.Worksheets[0]; // 假设数据在第一个工作表
                int rowCount = worksheet.Dimension.Rows;
                
                const int idCol = 1;
                const int itemIdCol = 2;
                const int weaponNameCol = 3;
                const int weaponTypeCol = 4;
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
                    foreach (var configData in weaponConfigData)
                    {
                        worksheet.Cells[newRow, idCol].Value = configData.weaponID;
                        worksheet.Cells[newRow, itemIdCol].Value = configData.itemID;
                        worksheet.Cells[newRow, weaponNameCol].Value = configData.weaponName;
                        worksheet.Cells[newRow, weaponTypeCol].Value = configData.weaponType.ToString();
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
    public struct WeaponConfigData
    {
        public int weaponID;
        public string weaponName;
        public int itemID;
        public QualityType quality;
        public WeaponType weaponType;
        public int skillID;
        public int battleEffectConditionId;
        public string battleEffectConditionDescription;
    }

    //默认值
    public struct AttackConfigData
    {
        //攻击半径
        public float AttackRadius;
        //攻击角度
        public float AttackRange;
        //攻击高度
        public float AttackHeight;
        
        public AttackConfigData(float attackRadius, float attackRange, float attackHeight)
        {
            AttackRadius = attackRadius;
            AttackRange = attackRange;
            AttackHeight = attackHeight;
        }
    }

    public enum WeaponType
    {
        None,
        Sword1,
        Sword2,
        Sword3,
        Sword4,
        Sword5,
        Sword6,
        Sword7,
        Sword8,
        Sword9,
        Sword10,
    }
}
