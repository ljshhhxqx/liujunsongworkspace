using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using OfficeOpenXml;
using UnityEngine.Serialization;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ItemConfig", menuName = "ScriptableObjects/ItemConfig")]
    public class ItemConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<GameItemConfigData> gameItemDatas;
        [SerializeField]
        private ItemOtherData itemOtherData;
        
        public Dictionary<int, GameItemConfigData> GameItemDatasDict { get; } = new Dictionary<int, GameItemConfigData>();
        
        public int MaxBagSize => itemOtherData.maxBagSize;
        
        public GameItemConfigData GetGameItemData(int configId)
        {
            if (GameItemDatasDict.TryGetValue(configId, out GameItemConfigData gameItemConfigData))
                return gameItemConfigData;
            for (int i = 0; i < gameItemDatas.Count; i++)
            {
                var gameItemData = gameItemDatas[i];
                if (gameItemData.id == configId)
                {
                    GameItemDatasDict.Add(configId, gameItemData);
                    return gameItemData;
                }
            }
            Debug.LogError($"Can not find item config data with id {configId}");
            return default;
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
                var gameItemData = new GameItemConfigData();
                gameItemData.id = int.Parse(row[0]);
                gameItemData.name = row[1];
                gameItemData.desc = row[2];
                gameItemData.price = int.Parse(row[3]);
                gameItemData.sellPriceRatio = float.Parse(row[4]);
                gameItemData.itemType = (PlayerItemType) Enum.Parse(typeof(PlayerItemType), row[5]);
                gameItemData.maxStack = int.Parse(row[6]);
                gameItemData.iconName = row[7];
                gameItemData.prefabName = row[8];
                gameItemData.quality = (QualityType) Enum.Parse(typeof(QualityType), row[9]);
                gameItemData.equipmentPart = (EquipmentPart) Enum.Parse(typeof(EquipmentPart), row[10]);
                gameItemData.duration = float.Parse(row[11]);
                gameItemData.propertyDesc = row[12];
                var buffExtra = JsonConvert.DeserializeObject<BuffExtraData[]>(row[13],jsonSerializerSettings);
                #if UNITY_EDITOR
                if (buffExtra.Length != 0)
                {
                    gameItemData.isDealWithBuffExtraData = true;
                }
                gameItemData.buffExtraData = gameItemData.isDealWithBuffExtraData ? buffExtra : null;
                #endif
                gameItemDatas.Add(gameItemData);
            }
        }
        
#if UNITY_EDITOR
        [SerializeField]
        private PropertyConfig propertyConfig;
        [SerializeField]
        private ConstantBuffConfig constantBuffConfig;
        [SerializeField]
        private WeaponConfig equipmentConfig;
        [SerializeField]
        private ArmorConfig armorConfig;

        /// <summary>
        /// 将道具的buffExtraData数据写入Excel
        /// </summary>
        /// <returns></returns>
        [Button("将道具的buffExtraData数据写入Excel")]
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
                    
                        const int buffExtraCol = 14;
                        const int buffIncreaseTypeCol = 13;
                        if (item.buffExtraData != null && item.buffExtraData.Length > 0)
                        {
                            // 将 buffExtraData 序列化为字符串
                            var json = JsonConvert.SerializeObject(item.buffExtraData, jsonSerializerSettings);
                            worksheet.Cells[row, buffExtraCol].Value = json;
                        }
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
        [Button("自动添加buffExtraData到ConstantBuffConfig的ScriptableObject中(但不写入Excel)")]
        public void CoverBuffExtraData()
        {
            for (int i = 0; i < gameItemDatas.Count; i++)
            {
                var gameItemData = gameItemDatas[i];
                if (gameItemData.isDealWithBuffExtraData || string.IsNullOrEmpty(gameItemData.propertyDesc))
                {
                    continue;
                }
                var itemDescriptionProperties = propertyConfig.GetItemDescriptionProperties(gameItemDatas[i].propertyDesc).ToArray();
                var list = new List<BuffExtraData>();
                for (int j = 0; j < itemDescriptionProperties.Length; j++)
                {
                    var tuple = itemDescriptionProperties[j];
                    var buffExtraData = constantBuffConfig.GetBuffDataByProperty(tuple);
                    if (buffExtraData.buffId == 0)
                    {
                        //说明没有找到对应的buff数据，需要手动添加到constantBuffConfig中
                        Debug.Log($"Item {gameItemData.id}-{tuple.Item1}-{tuple.Item2} buffExtraData is null, now add {tuple.Item1}-{tuple.Item2} to ConstantBuffConfig");
                        constantBuffConfig.AddItemBuff(new BuffData
                        {
                            buffId = constantBuffConfig.GetMaxBuffId() +1,
                            propertyType = tuple.Item1,
                            duration = gameItemData.duration,
                            increaseDataList = new List<BuffIncreaseData>()
                            {
                                new BuffIncreaseData
                                {
                                    increaseType = tuple.Item2,
                                    increaseValue = tuple.Item3,
                                    operationType = BuffOperationType.Add,
                                }
                            },
                        });
                    }
                    var extraData = new BuffExtraData
                    {
                        buffId = buffExtraData.buffId,
                        buffType = BuffType.Constant,
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
    public struct GameItemConfigData : IEquatable<GameItemConfigData>
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
        public string iconName;
        public string prefabName;
        //消耗品的持续时间，装备为-1(只有脱下才消失)
        public float duration;
        public QualityType quality;
        public EquipmentPart equipmentPart;
        public BuffExtraData[] buffExtraData;
        public string propertyDesc;
#if UNITY_EDITOR
        public bool isDealWithBuffExtraData;
#endif
        public bool Equals(GameItemConfigData other)
        {
            return id == other.id && name == other.name && desc == other.desc && price.Equals(other.price) && sellPriceRatio.Equals(other.sellPriceRatio) && itemType == other.itemType && maxStack == other.maxStack && weight == other.weight && iconName == other.iconName && prefabName == other.prefabName && duration.Equals(other.duration) && quality == other.quality && equipmentPart == other.equipmentPart && buffExtraData == null && propertyDesc == other.propertyDesc;
        }

        public override bool Equals(object obj)
        {
            return obj is GameItemConfigData other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(id);
            hashCode.Add(name);
            hashCode.Add(desc);
            hashCode.Add(price);
            hashCode.Add(sellPriceRatio);
            hashCode.Add((int)itemType);
            hashCode.Add(maxStack);
            hashCode.Add(weight);
            hashCode.Add(iconName);
            hashCode.Add(prefabName);
            hashCode.Add(duration);
            hashCode.Add((int)quality);
            hashCode.Add((int)equipmentPart);
            hashCode.Add(buffExtraData);
            hashCode.Add(propertyDesc);
            return hashCode.ToHashCode();
        }
    }

    [Serializable]
    public struct ItemOtherData
    {
        public int maxBagSize;
    }

    public enum EquipmentPart : byte
    {
        None,
        //暴击率、暴击伤害
        Head,
        //生命值、防御力
        Body,
        //体力值、体力恢复速度
        Leg,
        //移动速度、攻击速度
        Feet,
        //额外攻击力、生命恢复
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
        Collect,
        Gold,
        Score,
    }
    
    public static class PlayerItemTypeExtension
    {
        public static bool IsEquipment(this PlayerItemType itemType)
        {
            return itemType == PlayerItemType.Weapon || itemType == PlayerItemType.Armor;
        }
        
        public static bool ShowProperty(this PlayerItemType itemType)
        {
            return itemType == PlayerItemType.Weapon || itemType == PlayerItemType.Armor || itemType == PlayerItemType.Consume;
        }
    }   
}