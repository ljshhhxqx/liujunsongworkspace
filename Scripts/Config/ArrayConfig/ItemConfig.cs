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
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            gameItemDatas.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var chestData = new GameItemData();
                chestData.id = int.Parse(row[0]);
                chestData.name = row[1];
                chestData.desc = row[2];
                chestData.price = int.Parse(row[3]);
                chestData.itemType = (PlayerItemType) Enum.Parse(typeof(PlayerItemType), row[4]);
                chestData.maxStack = int.Parse(row[5]);
                chestData.iconLocation = row[6];
                chestData.buffExtraData = JsonConvert.DeserializeObject<BuffExtraData>(row[7]);
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
        public int price;
        public PlayerItemType itemType;
        public int maxStack;
        public string iconLocation;
        public BuffExtraData buffExtraData;
    }
    

    public enum PlayerItemType : byte
    {
        None,
        Equipment = 1 << 0,
        Consume = 1 << 1,
        Item = 1 << 2,
    }
}