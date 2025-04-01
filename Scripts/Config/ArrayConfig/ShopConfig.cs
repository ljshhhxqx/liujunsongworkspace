using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Tool.Static;
using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "ScriptableObjects/ShopConfig")]
    public class ShopConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<ShopConfigData> shopConfigData = new List<ShopConfigData>();
        
        private readonly Dictionary<QualityType, HashSet<int>> _qualityIds = new Dictionary<QualityType, HashSet<int>>();
        private readonly HashSet<int> _consumeItems = new HashSet<int>();
        private readonly HashSet<int> _weaponItems = new HashSet<int>();
        private readonly HashSet<int> _armorItems = new HashSet<int>();
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            shopConfigData.Clear();
            _consumeItems.Clear();
            _weaponItems.Clear();
            _armorItems.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];

                var shopConfig = new ShopConfigData();
                shopConfig.id = int.Parse(row[0]);
                shopConfig.itemId = int.Parse(row[1]);
                shopConfig.name = row[2];
                shopConfig.price = float.Parse(row[3]);
                shopConfig.sellPrice = float.Parse(row[4]);
                shopConfig.qualityType = (QualityType) Enum.Parse(typeof(QualityType), row[5]);
                shopConfig.playerItemType = (PlayerItemType) Enum.Parse(typeof(PlayerItemType), row[6]);
                switch (shopConfig.playerItemType)
                {
                    case PlayerItemType.Consume:
                        _consumeItems.Add(shopConfig.itemId);
                        break;
                    case PlayerItemType.Weapon:
                        _weaponItems.Add(shopConfig.itemId);
                        break;
                    case PlayerItemType.Armor:
                        _armorItems.Add(shopConfig.itemId);
                        break;
                }
                if (!_qualityIds.ContainsKey(shopConfig.qualityType))
                    _qualityIds.Add(shopConfig.qualityType, new HashSet<int>());
                _qualityIds[shopConfig.qualityType].Add(shopConfig.itemId);
                shopConfigData.Add(shopConfig);
            }
        }

        public HashSet<int> GetQualityItems(List<QualityType> qualityType, float weight)
        {
            var result = new HashSet<int>();
            foreach (var type in qualityType)
            {
                if (_qualityIds.TryGetValue(type, out var ids))
                {
                    var id = -1;
                    while (result.Contains(id))
                    {
                        id = GetRandomItem(ids, weight);
                    }
                    
                    result.Add(GetRandomItem(ids, weight));
                }
            }
            return result;
        }

        private int GetRandomItem(HashSet<int> source, float weight)
        {
            var totalWeight = 0f;
            foreach (var id in source)
            {
                totalWeight += shopConfigData[id].price;
            }
            totalWeight *= weight;
            var currentWeight = 0f;
            foreach (var id in source)
            {
                currentWeight += shopConfigData[id].price;
                if (currentWeight >= totalWeight)
                {
                    return id;
                }
            }
            return -1;
        }


        public HashSet<int> RefreshShopItems()
        {
            HashSet<int> result = new HashSet<int>();
    
            // 从消耗品中选2个不同品质的
            if (TryGetDistinctQualityItems(_consumeItems, PlayerItemType.Consume, out var consumeIds))
                result.UnionWith(consumeIds);
    
            // 从武器中选2个不同品质的
            if (TryGetDistinctQualityItems(_weaponItems, PlayerItemType.Weapon, out var weaponIds))
                result.UnionWith(weaponIds);
    
            // 从护甲中选2个不同品质的
            if (TryGetDistinctQualityItems(_armorItems, PlayerItemType.Armor, out var armorIds))
                result.UnionWith(armorIds);

            return result;
        }

        private bool TryGetDistinctQualityItems(HashSet<int> source, PlayerItemType type, out HashSet<int> result)
        {
            result = new HashSet<int>();
    
            // 获取该类型所有配置数据
            var items = shopConfigData
                .Where(d => source.Contains(d.itemId) && d.playerItemType == type)
                .ToList();

            // 按品质分组
            var qualityGroups = items
                .GroupBy(d => d.qualityType)
                .OrderBy(g => UnityEngine.Random.value)
                .Take(2)
                .ToList();

            // 需要至少两个不同品质组
            if (qualityGroups.Count < 2) return false;

            // 从每个品质组随机选一个
            foreach (var group in qualityGroups)
            {
                var randomItem = group.OrderBy(x => UnityEngine.Random.value).First();
                result.Add(randomItem.id);
            }

            return result.Count == 2;
        }

    }

    [Serializable]
    public struct ShopConfigData
    {
        public int id;
        public int itemId;
        public string name;
        public float price;
        public float sellPrice;
        public QualityType qualityType;
        public PlayerItemType playerItemType;
    }
}