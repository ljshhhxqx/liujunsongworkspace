using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public interface IItemBase
    {
        void SetData<T>(T data) where T : IItemBaseData, new();
    }
    
    public abstract class ItemBase : MonoBehaviour, IItemBase
    {
        public abstract void SetData<T>(T data) where T : IItemBaseData, new();
    }

    public interface IItemBaseData : IUIDatabase
    {
        
    }
    
    public struct RoomMemberItemData : IItemBaseData
    {
        public string PlayerId;
        public string Name;
        public int Level;
        public bool IsFriend;
        public bool IsSelf;
        public Action<string> OnAddFriendClick;
    }
    
    public struct RoomInviteItemData : IItemBaseData
    {
        public string PlayerId;
        public string Name;
        public int Level;
        public Action<string> OnInviteClick;
    }

    public struct RoomListItemData : IItemBaseData
    {
        public string RoomId;
        public string RoomName;
        public string RoomOwnerName;
        public string RoomStatus;
        public string RoomType;
        public string HasPassword;
        public Action<string> OnJoinClick;
    }

    public struct PropertyItemData : IItemBaseData
    {
        public PropertyTypeEnum PropertyType;
        public string Name;
        public PropertyConsumeType ConsumeType;
        public float CurrentProperty;
        public float MaxProperty;
    }
    
    public struct BagItemData : IItemBaseData, IEquatable<BagItemData>
    {
        public string ItemName;
        public Sprite Icon;
        public int Index;
        public int Stack;
        //简要描述(非属性类的物品的描述)
        public string Description;
        //属性描述
        public string PropertyDescription;
        //装备被动描述
        public string EquipPassiveDescription;
        public PlayerItemType PlayerItemType;
        public bool IsEquip;
        public bool IsLock;
        public int MaxStack; 
        public float Price;
        public float SellRatio;
        //<售卖的格子, 数量>
        public Action<int, int> OnUseItem;
        //<丢弃的格子, 数量>
        public Action<int, int> OnDropItem;
        //<格子1, 格子2>
        public Action<int, int> OnExchangeItem;
        //<格子, 是否锁定>
        public Action<int, bool> OnLockItem;
        //<格子, 是否装备>
        public Action<int, bool> OnEquipItem;
        //<格子, 数量>
        public Action<int, int> OnSellItem;
        public Sprite QualityIcon;

        public bool Equals(BagItemData other)
        {
            return ItemName == other.ItemName && Index == other.Index && Stack == other.Stack && Description == other.Description && PlayerItemType == other.PlayerItemType && IsEquip == other.IsEquip && IsLock == other.IsLock && MaxStack == other.MaxStack;
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(ItemName);
            hashCode.Add(Index);
            hashCode.Add(Stack);
            hashCode.Add(Description);
            hashCode.Add((int)PlayerItemType);
            hashCode.Add(IsEquip);
            hashCode.Add(IsLock);
            hashCode.Add(MaxStack);
            return hashCode.ToHashCode();
        }
    }

    public struct EquipItemData : IItemBaseData, IEquatable<EquipItemData>
    {
        public string ItemName;
        public string Description;
        public bool IsLock;
        public Sprite Icon;
        public Sprite QualityIcon;
        public EquipmentPart EquipmentPartType;
        public PlayerItemType PlayerItemType;
        public Action<int, bool> OnLockItem;
        public Action<int, bool> OnEquipItem;
        public Action<int> OnDropItem;

        public bool Equals(EquipItemData other)
        {
            return ItemName == other.ItemName && Description == other.Description && IsLock == other.IsLock && EquipmentPartType == other.EquipmentPartType;
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(ItemName);
            hashCode.Add(Description);
            hashCode.Add(IsLock);
            hashCode.Add((int)EquipmentPartType);
            return hashCode.ToHashCode();
        }

        public BagItemData ToBagItemData()
        {
            return new BagItemData
            {
                ItemName = ItemName,
                Icon = Icon,
                Index = -1,
                Stack = 1,
                Description = Description,
                PropertyDescription = "",
                EquipPassiveDescription = "",
                PlayerItemType = PlayerItemType.Item,
                IsEquip = true,
                IsLock = IsLock,
                MaxStack = 1,
                Price = 0,
                SellRatio = 0,
                OnUseItem = null,
                OnDropItem = null,
                OnExchangeItem = null,
                OnLockItem = OnLockItem,
                OnEquipItem = OnEquipItem,
                OnSellItem = null,
                QualityIcon = QualityIcon
            };
        }
    }

    public struct RandomShopItemData : IItemBaseData, IEquatable<RandomShopItemData>
    {
        public int ShopId;
        public int ItemConfigId;
        public int ShopConfigId;
        public int RemainingCount;
        public float Price;
        public float SellPrice;
        public int MaxCount;
        public QualityType QualityType;
        public Sprite Icon;
        public Sprite QualityIcon;
        public string Name;
        public string MainProperty;
        public string PassiveProperty;
        public PlayerItemType ItemType;
        public Action<int, int> OnBuyItem;

        public bool Equals(RandomShopItemData other)
        {
            return ShopId == other.ShopId && RemainingCount == other.RemainingCount;
        }

        public override bool Equals(object obj)
        {
            return obj is RandomShopItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ShopId, RemainingCount);
        }
    }
}
