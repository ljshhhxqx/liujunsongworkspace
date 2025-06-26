using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using OfficeOpenXml;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ConstantBuffConfig", menuName = "ScriptableObjects/ConstantBuffConfig")]
    public class ConstantBuffConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<BuffData> buffs = new List<BuffData>();
        
        public BuffData GetBuff(BuffExtraData extraData)
        {
            return GetBuffData(extraData.buffId);
        }
        
        
        public BuffData GetBuffData(int buffId, CollectObjectBuffSize collectObjectBuffSize = CollectObjectBuffSize.Small)
        {
            var buff = buffs.Find(b => b.buffId == buffId);
            for (var i = 0; i < buff.increaseDataList.Count; i++)
            {
                var buffData = buff.increaseDataList[i];
                buffData.increaseValue *= BuffDataReaderWriter.GetBuffRatioBySize(collectObjectBuffSize);
                buff.increaseDataList[i] = buffData;
            }

            return buffs.Find(x => x.buffId == buffId);
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            buffs.Clear();
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var buff = new BuffData();
                buff.buffId = int.Parse(data[0]);
                buff.propertyType = Enum.Parse<PropertyTypeEnum>(data[1]);
                buff.duration = float.Parse(data[2]);
                var json = JsonConvert.DeserializeObject<BuffIncreaseData[]>(data[3],jsonSerializerSettings);
                buff.increaseDataList = json.ToList();
                buff.sourceType = Enum.Parse<BuffSourceType>(data[4]);
                buff.mainIncreaseType = Enum.Parse<BuffIncreaseType>(data[5]);
                // else if (buff.sourceType == BuffSourceType.NoUnion)
                // {
                //     if (!_noUnionBuffs.ContainsKey(buff.propertyType))
                //     {
                //         _noUnionBuffs.Add(buff.propertyType, new List<BuffData>());
                //     }
                //     _noUnionBuffs[buff.propertyType].Add(buff);
                // }
                buffs.Add(buff);
            }
        }

        public List<BuffExtraData> GetNoUnionBuffs()
        {
            return buffs.Where(b => b.sourceType== BuffSourceType.NoUnion)
                .Select(b =>
                {
                    var buff = new BuffExtraData();
                    buff.buffId = b.buffId;
                    buff.buffType = BuffType.Constant;
                    return buff;
                }).ToList();
        }

        public BuffData GetBuffDataByProperty((PropertyTypeEnum, BuffIncreaseType, float) property)
        {
            //Debug.Log($"GetBuffDataByProperty: {property.Item1} {property.Item2}, {increaseType}");
            for (var i = 0; i < buffs.Count; i++)
            {
                var buff = buffs[i];
                // Debug.Log($"buff: {buff.buffId} {buff.propertyType} {buff.duration}");
                // for (var j = 0; j < buff.increaseDataList.Count; j++)
                // {
                //     var data = buff.increaseDataList[j];
                //     Debug.Log($"data: {data.increaseType} {data.increaseValue}");
                // }
                if (buff.propertyType == property.Item1 && buff.increaseDataList.Exists(data => data.increaseType == property.Item2 && Mathf.Approximately(data.increaseValue, property.Item3)))
                {
                    return buff;
                }
            }
            return default;
        }
        
        public int GetMaxBuffId()
        {
            return buffs.Count > 0? buffs.Max(b => b.buffId) : 0;
        }
        
#if UNITY_EDITOR
        public void AddItemBuff(BuffData newBuff)
        {
            if (buffs.Exists(b => b.buffId == newBuff.buffId))
            {
                Debug.LogWarning("Buff with buff id " + newBuff.buffId + " already exists.");
                return;
            }
            buffs.Add(newBuff);
            
            EditorUtility.SetDirty(this);
        }

        [Button]
        public void WriteBuffExtraDataToExcel()
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
                var worksheet = package.Workbook.Worksheets[0];
                var rowCount = worksheet.Dimension?.Rows ?? 1;
                const int idCol = 1; // buffId 列
                const int propTypeCol = 2;
                const int durationCol = 3;
                const int increaseDataCol = 4;

                // 现有 buff 的 ID 集合
                var existingIds = new HashSet<int>();
                for (int row = 3; row <= rowCount; row++)
                {
                    //var value = worksheet.Cells[row, idCol].GetValue<double>();
                    int buffId = (int)worksheet.Cells[row, idCol].GetValue<double>();
                    existingIds.Add(buffId);
                }

                // 追加新 buff 数据
                var newRow = rowCount + 1;
                foreach (var buff in buffs)
                {
                    if (!existingIds.Contains(buff.buffId))
                    {
                        worksheet.Cells[newRow, idCol].Value = buff.buffId;
                        worksheet.Cells[newRow, propTypeCol].Value = buff.propertyType.ToString();
                        worksheet.Cells[newRow, durationCol].Value = buff.duration;

                        var json = JsonConvert.SerializeObject(buff.increaseDataList, jsonSerializerSettings);
                        worksheet.Cells[newRow, increaseDataCol].Value = json;

                        newRow++;
                    }
                }

                // 保存文件
                package.Save();
                Debug.Log("Buff table updated successfully!");
            }
        }
#endif
    }
}