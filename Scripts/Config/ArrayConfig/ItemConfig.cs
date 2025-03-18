using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;

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
                Formatting = Formatting.Indented,
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
                gameItemData.buffExtraData = JsonConvert.DeserializeObject<BuffExtraData[]>(row[11],jsonSerializerSettings);
                gameItemData.propertyDesc = row[12];
                gameItemDatas.Add(gameItemData);
            }
        }
        
        // [Button]
        // private void 
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
        public QualityType quality;
        public int elementId;
        public BuffExtraData[] buffExtraData;
        public string propertyDesc;
    }

    public enum EquipmentPart : byte
    {
        Head,
        Body,
        Arm,
        Leg,
        Feet,
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