using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Tool.Static;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ShopConfig", menuName = "ScriptableObjects/ShopConfig")]
    public class ShopConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<ShopConfigData> shopConfigData = new List<ShopConfigData>();
        [SerializeField]
        private ShopConstantData shopConstantData;
        
        private Dictionary<QualityType, HashSet<int>> _qualityIds = new Dictionary<QualityType, HashSet<int>>();
        private HashSet<int> _consumeItems = new HashSet<int>();
        private HashSet<int> _weaponItems = new HashSet<int>();
        private HashSet<int> _armorItems = new HashSet<int>();

        public Dictionary<QualityType, HashSet<int>> QualityIds
        {
            get
            {
                if (_qualityIds.Count == 0)
                {
                    foreach (var data in shopConfigData)
                    {
                        if (!_qualityIds.TryGetValue(data.qualityType, out var ids))
                        {
                            ids = new HashSet<int>();
                            _qualityIds.Add(data.qualityType, ids);
                        }
                        ids.Add(data.id);
                    }
                }
                return _qualityIds;
            }
        }
        
        public HashSet<int> ConsumeItems
        {
            get
            {
                if (_consumeItems.Count == 0)
                {
                    foreach (var data in shopConfigData)
                    {
                        if (data.playerItemType == PlayerItemType.Consume)
                        {
                            _consumeItems.Add(data.id);
                        }
                    }
                }
                return _consumeItems;
            }
        }

        public HashSet<int> WeaponItems
        {
            get
            {
                if (_weaponItems.Count == 0)
                {
                    foreach (var data in shopConfigData)
                    {
                        if (data.playerItemType == PlayerItemType.Weapon)
                        {
                            _weaponItems.Add(data.id);
                        }
                    }
                }

                return _weaponItems;
            }
        }

        public HashSet<int> ArmorItems
        {
            get
            {
                if (_armorItems.Count == 0)
                {
                    foreach (var data in shopConfigData)
                    {
                        if (data.playerItemType == PlayerItemType.Armor)
                        {
                            _armorItems.Add(data.id);
                        }
                    }
                }
                return _armorItems;
            }    
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            shopConfigData.Clear();
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
                shopConfig.maxCount = int.Parse(row[7]);
                shopConfigData.Add(shopConfig);
            }
        }

        public HashSet<int> GetItemsByShopId(IList<int> shopIds)
        {
            var result = new HashSet<int>();
            foreach (var shopId in shopIds)
            {
                var shopConfig = GetShopConfigData(shopId);
                if (shopConfig.Equals(default)) continue;
                result.Add(shopConfig.itemId);
            }
            return result;
            
        }
        
        public ShopConfigData GetShopDataByItemId(int itemId)
        {
            return shopConfigData.FirstOrDefault(d => d.itemId == itemId);
        }
        
        public ShopConfigData GetShopConfigData(int shopId)
        {
            return shopConfigData.Find(x => x.id == shopId);
        }

        public HashSet<int> GetQualityItems(List<QualityType> qualityType, float weight)
        {
            var result = new HashSet<int>();
            foreach (var type in qualityType)
            {
                if (QualityIds.TryGetValue(type, out var ids))
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

        private int GetRandomItem(HashSet<int> source, float weight, int preShopId = -1)
        {
            var totalWeight = 0f;
            foreach (var id in source)
            {
                var shopConfig = GetShopConfigData(id);
                if (shopConfig.id == preShopId) continue;
                totalWeight += shopConfig.price;
            }
            totalWeight *= weight;
            var currentWeight = 0f;
            foreach (var id in source)
            {
                var shopConfig = GetShopConfigData(id);
                currentWeight += shopConfig.price;
                
                if (currentWeight >= totalWeight)
                {
                    if (preShopId != -1 && id != preShopId)
                        return id;
                    return id;
                }
            }
            return -1;
        }

        public int GetRandomItem(int preShopId, int otherShopId, PlayerItemType playerItemType)
        {
            var otherShopConfig = GetShopConfigData(otherShopId);
            ShopConfigData data = default;

            var id = -1;
            while (id == preShopId || data.qualityType == otherShopConfig.qualityType)
            {
                id = playerItemType switch
                {
                    PlayerItemType.Consume => ConsumeItems.RandomSelect(),
                    PlayerItemType.Weapon => WeaponItems.RandomSelect(),
                    PlayerItemType.Armor => ArmorItems.RandomSelect(),
                    _ => -1
                };
                data = GetShopConfigData(id);
            }
            return id;
        }

        public HashSet<int> RefreshShopItems(HashSet<int> preShopIds = null)
        {
            HashSet<int> result = new HashSet<int>();
    
            // 从消耗品中选2个不同品质的
            if (TryGetDistinctQualityItems(ConsumeItems, PlayerItemType.Consume, preShopIds, out var consumeIds))
                result.UnionWith(consumeIds);
    
            // 从武器中选2个不同品质的
            if (TryGetDistinctQualityItems(WeaponItems, PlayerItemType.Weapon, preShopIds, out var weaponIds))
                result.UnionWith(weaponIds);
    
            // 从护甲中选2个不同品质的
            if (TryGetDistinctQualityItems(ArmorItems, PlayerItemType.Armor, preShopIds, out var armorIds))
                result.UnionWith(armorIds);

            return result;
        }

        private bool TryGetDistinctQualityItems(HashSet<int> source, PlayerItemType type, HashSet<int> preShopIds, out HashSet<int> result)
        {
            result = new HashSet<int>();
            List<ShopConfigData> items;
            var count = shopConstantData.onceEachTypeCount;
    
            // 获取该类型所有配置数据
            if (preShopIds != null)
            {
                items = shopConfigData
                    .Where(d => source.Contains(d.itemId) && d.playerItemType == type && preShopIds.Contains(d.itemId))
                    .ToList();
            }
            else
            {
                items = shopConfigData
                    .Where(d => source.Contains(d.itemId) && d.playerItemType == type)
                    .ToList();
            }

            // 按品质分组
            var qualityGroups = items
                .GroupBy(d => d.qualityType)
                .OrderBy(g => Random.value)
                .Take(count)
                .ToList();

            // 需要至少两个不同品质组
            if (qualityGroups.Count < count) return false;

            // 从每个品质组随机选一个
            foreach (var group in qualityGroups)
            {
                var randomItem = group.OrderBy(x => Random.value).First();
                result.Add(randomItem.id);
            }

            return result.Count == count;
        }

        public bool CanRefreshShopItems(int gold)
        {
            return gold >= shopConstantData.onceCostGold;
        }
        
        public ShopConstantData GetShopConstantData()
        {
            return shopConstantData;
        }
    }

    [Serializable]
    public struct ShopConstantData
    {
        //每次每一种类刷新的数量
        public int onceEachTypeCount;
        //每次刷新花费的金币
        public int onceCostGold;
        public string shopEquipPassiveDescription;
    }

    [Serializable]
    public struct ShopConfigData : IEquatable<ShopConfigData>
    {
        public int id;
        public int itemId;
        public string name;
        public float price;
        public float sellPrice;
        public QualityType qualityType;
        public PlayerItemType playerItemType;
        public int maxCount;

        public bool Equals(ShopConfigData other)
        {
            return id == other.id && itemId == other.itemId 
                                  && name == other.name && price.Equals(other.price) && sellPrice.Equals(other.sellPrice) && qualityType == other.qualityType && playerItemType == other.playerItemType
                                  && maxCount == other.maxCount;
        }

        public override bool Equals(object obj)
        {
            return obj is ShopConfigData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(id, itemId, name, price, sellPrice, (int)qualityType, (int)playerItemType);
        }
    }
}