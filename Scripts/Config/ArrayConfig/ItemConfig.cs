using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using OfficeOpenXml;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ItemConfig", menuName = "ScriptableObjects/ItemConfig")]
    public class ItemConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<GameItemData> gameItemDatas;
        
        public GameItemData GetGameItemData(int configId)
        {
            foreach (var itemData in gameItemDatas)
            {
                if (itemData.id == configId)
                {
                    return itemData;
                }
            }

            return new GameItemData();
        }
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            gameItemDatas.Clear();
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            for (var i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var gameItemData = new GameItemData();
                gameItemData.id = int.Parse(row[0]);
                gameItemData.name = row[1];
                gameItemData.desc = row[2];
                gameItemData.price = int.Parse(row[3]);
                gameItemData.sellPriceRatio = float.Parse(row[4]);
                gameItemData.itemType = (PlayerItemType) Enum.Parse(typeof(PlayerItemType), row[5]);
                gameItemData.maxStack = int.Parse(row[6]);
                gameItemData.weight = int.Parse(row[7]);
                gameItemData.iconLocation = row[8];
                gameItemData.prefabLocation = row[9];
                gameItemData.quality = (QualityType) Enum.Parse(typeof(QualityType), row[10]);
                gameItemData.equipmentPart = (EquipmentPart) Enum.Parse(typeof(EquipmentPart), row[11]);
                gameItemData.duration = float.Parse(row[12]);
                gameItemData.propertyDesc = row[13];
                gameItemData.buffIncreaseType = JsonConvert.DeserializeObject<BuffIncreaseType[]>(row[14],jsonSerializerSettings);
                var buffExtra = JsonConvert.DeserializeObject<BuffExtraData[]>(row[15],jsonSerializerSettings);
                if (buffExtra.Length != 0)
                {
                    gameItemData.isDealWithBuffExtraData = true;
                }
                gameItemData.buffExtraData = gameItemData.isDealWithBuffExtraData ? buffExtra : null;
                gameItemDatas.Add(gameItemData);
            }
        }
        
#if UNITY_EDITOR
        [SerializeField]
        private ConfigManager configManager;
        private PropertyConfig _propertyConfig;
        private ConstantBuffConfig _constantBuffConfig;
        private WeaponConfig _equipmentConfig;
        private ArmorConfig _armorConfig;

        private void OnValidate()
        {
            _propertyConfig ??= configManager.GetConfig<PropertyConfig>();
            _constantBuffConfig ??= configManager.GetConfig<ConstantBuffConfig>();
            _equipmentConfig ??= configManager.GetConfig<WeaponConfig>();
            _armorConfig ??= configManager.GetConfig<ArmorConfig>();
        }

        /// <summary>
        /// 将道具的buffExtraData数据写入Excel
        /// </summary>
        /// <returns></returns>
        [Button]
        public void WriteBuffExtraDataToExcel()
        {
            // 设置 EPPlus 许可证
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
                int idCol = 1; // 假设 id 在第 1 列
                int row = 0;

                try
                {
                    // 从第 2 行开始（跳过表头）
                    for (row = 3; row <= rowCount; row++)
                    {
                        var value = worksheet.Cells[row, idCol];
                        var itemId = (int)value.GetValue<double>();
                        var item = gameItemDatas.FirstOrDefault(i => i.id == itemId);
                        if (item.id == 0) continue;
                    
                        const int buffExtraCol = 16;
                        const int buffIncreaseTypeCol = 15;
                        if (item.buffExtraData != null && item.buffExtraData.Length > 0)
                        {
                            // 将 buffExtraData 序列化为字符串
                            var json = JsonConvert.SerializeObject(item.buffExtraData, jsonSerializerSettings);
                            worksheet.Cells[row, buffExtraCol].Value = json;
                        }

                        // if (item.buffIncreaseType != null && item.buffIncreaseType.Length > 0)
                        // {
                        //     var json2 = JsonConvert.SerializeObject(item.buffIncreaseType, jsonSerializerSettings);
                        //     json2 = json2.Replace("\\n", "");
                        //     json2 = json2.Replace(" ", "");
                        //     worksheet.Cells[row, buffIncreaseTypeCol].Value = json2;
                        // }
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

        /// <summary>
        /// 自动添加buffExtraData
        /// 1. 遍历所有道具的propertyDesc，找到对应的buffExtraData
        /// 2. 如果没有找到，则手动添加到ConstantBuffConfig中
        /// 3. 找到对应的buffExtraData，并设置到道具的buffExtraData中
        /// </summary>
        [Button]
        public void CoverBuffExtraData()
        {
            // 设置 EPPlus 许可证
            for (int i = 0; i < gameItemDatas.Count; i++)
            {
                var gameItemData = gameItemDatas[i];
                if (gameItemData.isDealWithBuffExtraData)
                {
                    continue;
                }
                var itemDescriptionProperties = _propertyConfig.GetItemDescriptionProperties(gameItemDatas[i].propertyDesc).ToArray();
                var list = new List<BuffExtraData>();
                for (int j = 0; j < itemDescriptionProperties.Length; j++)
                {
                    var tuple = itemDescriptionProperties[j];
                    var buffExtraData = _constantBuffConfig.GetBuffDataByProperty(tuple, gameItemData.buffIncreaseType[j]);
                    if (buffExtraData.buffId == 0)
                    {
                        //说明没有找到对应的buff数据，需要手动添加到constantBuffConfig中
                        Debug.Log($"Item {gameItemData.id}-{tuple.Item1}-{tuple.Item2} buffExtraData is null, now add {tuple.Item1}-{tuple.Item2} to ConstantBuffConfig");
                        _constantBuffConfig.AddItemBuff(new BuffData
                        {
                            buffId = _constantBuffConfig.GetMaxBuffId() +1,
                            propertyType = tuple.Item1,
                            duration = gameItemData.duration,
                            increaseDataList = new List<BuffIncreaseData>()
                            {
                                new BuffIncreaseData
                                {
                                    increaseType = gameItemData.buffIncreaseType[j],
                                    increaseValue = tuple.Item2,
                                    operationType = BuffOperationType.Add,
                                }
                            },
                        });
                    }
                    var extraData = new BuffExtraData
                    {
                        buffId = buffExtraData.buffId,
                        buffType = BuffType.Constant,
                        collectObjectBuffSize = CollectObjectBuffSize.Small,
                    };
                    list.Add(extraData);
                }

                gameItemData.buffExtraData = list.ToArray();
                gameItemDatas[i] = gameItemData;
            }
            EditorUtility.SetDirty(this);
            CheckBuffExtraDataEmpty();
        }

        [Button]
        public void CheckBuffExtraDataEmpty()
        {
            for (int i = 0; i < gameItemDatas.Count; i++)
            {
                var gameItemData = gameItemDatas[i];
                if (gameItemData.buffExtraData.Length == 0)
                {
                    Debug.Log($"Item {gameItemData.id} is empty");
                }
            }
        }
#endif
    }

    [Serializable]
    public struct GameItemData
    {
        public int id;
        public string name;
        public string desc;
        public float price;
        //玩家卖给商店时，获取的价格为原来的价格的sellPriceRatio倍
        public float sellPriceRatio;
        public PlayerItemType itemType;
        public int maxStack;
        public int weight;
        public string iconLocation;
        public string prefabLocation;
        //消耗品的持续时间，装备为-1(只有脱下才消失)
        public float duration;
        public QualityType quality;
        public EquipmentPart equipmentPart;
        public BuffExtraData[] buffExtraData;
        public string propertyDesc;
        public BuffIncreaseType[] buffIncreaseType;
#if UNITY_EDITOR
        public bool isDealWithBuffExtraData;
#endif
    }

    public enum EquipmentPart : byte
    {
        Head,
        Body,
        Arm,
        Leg,
        Feet,
        Waist,

        Weapon,
    }

    public enum QualityType : byte
    {
        Normal,
        Rare,
        Legendary,
    }

    /// <summary>
    /// 玩家道具类型
    /// </summary>
    public enum PlayerItemType : byte
    {
        None,
        Weapon,
        Armor,
        Consume,
        Item,
    }
}