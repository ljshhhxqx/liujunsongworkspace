using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial class PlayerShopState : ISyncPropertyState
    {
        [MemoryPackOrder(0)]
        public MemoryDictionary<int, ShopItemData> RandomShopItems;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerShop;
        
        public bool IsEqual(ISyncPropertyState other, float tolerance = 0.01f)
        {
            return true;
        }
    }

    [MemoryPackable]
    public partial struct ShopItemData : IEquatable<ShopItemData>
    {
        [MemoryPackOrder(0)]
        public int ShopId;
        [MemoryPackOrder(1)]
        public int ItemConfigId;
        [MemoryPackOrder(2)]
        public float Price;
        [MemoryPackOrder(3)]
        public QualityType Quality;
        [MemoryPackOrder(4)]
        public int RemainingCount;
        [MemoryPackOrder(5)]
        public int MaxCount;
        [MemoryPackOrder(6)]
        public int ShopConfigId;
        [MemoryPackOrder(7)]
        public PlayerItemType ItemType;
        [MemoryPackOrder(8)]
        public float SellPrice;
        //消耗品：显示确定的属性增益
        //装备：显示主要属性增益
        [MemoryPackOrder(9)]
        public MemoryList<AttributeIncreaseData> MainIncreaseDatas;
        //消耗品：显示随机属性增益(精确到数值的范围最大值和最小值)
        //装备：不显示(只有进入玩家背包才会有)
        [MemoryPackOrder(10)]
        public MemoryList<RandomAttributeIncreaseData> PassiveIncreaseDatas;

        public bool Equals(ShopItemData other)
        {
            return ShopId == other.ShopId && ItemConfigId == other.ItemConfigId && Price.Equals(other.Price) && Quality == other.Quality && RemainingCount == other.RemainingCount && MaxCount == other.MaxCount && ShopConfigId == other.ShopConfigId && ItemType == other.ItemType && SellPrice.Equals(other.SellPrice);
        }

        public override bool Equals(object obj)
        {
            return obj is ShopItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(ShopId);
            hashCode.Add(ItemConfigId);
            hashCode.Add(Price);
            hashCode.Add((int)Quality);
            hashCode.Add(RemainingCount);
            hashCode.Add(MaxCount);
            hashCode.Add(ShopConfigId);
            hashCode.Add((int)ItemType);
            hashCode.Add(SellPrice);
            return hashCode.ToHashCode();
        }
    }
}