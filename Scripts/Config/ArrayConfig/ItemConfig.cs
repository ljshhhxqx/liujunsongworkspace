using System;
using System.Collections.Generic;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ChestDataConfig", menuName = "ScriptableObjects/ChestDataConfig")]
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
            for (var i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var chestData = new GameItemData();
                chestData.id = int.Parse(row[0]);
                chestData.name = row[1];
                chestData.desc = row[2];
                chestData.price = int.Parse(row[3]);
                chestData.itemType = (PlayerItemType) Enum.Parse(typeof(PlayerItemType), row[4]);
                chestData.maxStack = int.Parse(row[5]);
                chestData.weight = int.Parse(row[6]);
                chestData.iconLocation = row[7];
                chestData.prefabLocation = row[8];
                chestData.quality = (QualityType) Enum.Parse(typeof(QualityType), row[9]);
                chestData.elementId = int.Parse(row[10]);
                chestData.sellPriceRatio = float.Parse(row[11]);
                chestData.buffExtraData = JsonConvert.DeserializeObject<BuffExtraData>(row[12]);
                gameItemDatas.Add(chestData);
            }
        }
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
        public BuffExtraData buffExtraData;
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