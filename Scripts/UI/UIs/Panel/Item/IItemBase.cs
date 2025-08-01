using System;
using System.Text;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.UI.UIs.Panel.Item
{
    public interface IItemBase
    {
        void SetData<T>(T data) where T : IItemBaseData, new();
    }
    
    public abstract class ItemBase : MonoBehaviour, IItemBase
    {
        public abstract void SetData<T>(T data) where T : IItemBaseData, new();
        public abstract void Clear();
    }

    public interface IItemBaseData : IUIDatabase
    {
        
    }

    public struct AnimationStateData : IItemBaseData, IEquatable<AnimationStateData>, IPoolObject
    {
        public AnimationState State;
        public float Duration;
        public float Timer;
        public float Cost;
        public int Index;
        public Sprite Icon;
        public Sprite Frame;
        
        public bool Equals(AnimationStateData other)
        {
            return this.State == other.State && Mathf.Approximately(this.Duration, other.Duration) && 
                   this.Index == other.Index && Mathf.Approximately(this.Timer, other.Timer) && 
                   this.Icon == other.Icon && this.Frame == other.Frame && Mathf.Approximately(this.Cost, other.Cost);
        }

        public void Init()
        {
            
        }

        public void Clear()
        {
            State = AnimationState.None;
            Duration = 0f;
            Timer = 0f;
            Index = 0;
            Icon = null;
            Frame = null;
            Cost = 0f;
        }

        public override string ToString()
        {
            var stringBuilder = new StringBuilder();
            stringBuilder.Append($"动画{State} 状态：");
            stringBuilder.AppendFormat("-Index:{0}", Index);
            stringBuilder.AppendFormat("-Duration:{0}", Duration);
            stringBuilder.AppendFormat("-Timer:{0}", Timer);
            stringBuilder.AppendFormat("-Cost:{0}", Cost);
            stringBuilder.AppendFormat("-Icon:{0}", Icon);
            stringBuilder.AppendFormat("-Frame:{0}", Frame);
            stringBuilder.AppendFormat("-Cost:{0}", Cost);
            return stringBuilder.ToString();
        }
    }
    
    public struct RoomMemberItemData : IItemBaseData, IEquatable<RoomMemberItemData>
    {
        public int Id;
        public string PlayerId;
        public string Name;
        public int Level;
        public bool IsFriend;
        public bool IsSelf;
        public Action<string> OnAddFriendClick;

        public bool Equals(RoomMemberItemData other)
        {
            return PlayerId == other.PlayerId && Name == other.Name && Level == other.Level && IsFriend == other.IsFriend && IsSelf == other.IsSelf && Equals(OnAddFriendClick, other.OnAddFriendClick);
        }

        public override bool Equals(object obj)
        {
            return obj is RoomMemberItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerId, Name, Level, IsFriend, IsSelf, OnAddFriendClick);
        }
    }
    
    public struct RoomInviteItemData : IItemBaseData, IEquatable<RoomInviteItemData>
    {
        public int Id;
        public string PlayerId;
        public string Name;
        public int Level;
        public Action<string> OnInviteClick;

        public bool Equals(RoomInviteItemData other)
        {
            return PlayerId == other.PlayerId && Name == other.Name && Level == other.Level && Equals(OnInviteClick, other.OnInviteClick);
        }

        public override bool Equals(object obj)
        {
            return obj is RoomInviteItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(PlayerId, Name, Level, OnInviteClick);
        }
    }

    public struct RoomListItemData : IItemBaseData,IEquatable<RoomListItemData>
    {
        public int Id;
        public string RoomId;
        public string RoomName;
        public string RoomOwnerName;
        public string RoomStatus;
        public string RoomType;
        public string HasPassword;
        public Action<string> OnJoinClick;

        public bool Equals(RoomListItemData other)
        {
            return RoomId == other.RoomId && RoomName == other.RoomName && RoomOwnerName == other.RoomOwnerName && RoomStatus == other.RoomStatus && RoomType == other.RoomType && HasPassword == other.HasPassword && Equals(OnJoinClick, other.OnJoinClick);
        }

        public override bool Equals(object obj)
        {
            return obj is RoomListItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(RoomId, RoomName, RoomOwnerName, RoomStatus, RoomType, HasPassword, OnJoinClick);
        }
    }

    public struct PropertyItemData : IItemBaseData, IEquatable<PropertyItemData>
    {
        public PropertyTypeEnum PropertyType;
        public string Name;
        public PropertyConsumeType ConsumeType;
        public float CurrentProperty;
        public float MaxProperty;

        public bool Equals(PropertyItemData other)
        {
            return PropertyType == other.PropertyType && Name == other.Name && ConsumeType == other.ConsumeType && CurrentProperty.Equals(other.CurrentProperty) && MaxProperty.Equals(other.MaxProperty);
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)PropertyType, Name, (int)ConsumeType, CurrentProperty, MaxProperty);
        }
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
        public string RandomDescription;
        //装备被动属性
        public string PassiveDescription;
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
                RandomDescription = "",
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
        public string Description;
        public string MainProperty;
        public string RandomProperty;
        public string PassiveDescription;
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

    public struct PlayerHpItemData : IItemBaseData, IEquatable<PlayerHpItemData>
    {
        public int PlayerId;
        public string Name;
        public float CurrentHp;
        public float MaxHp;
        public float CurrentMp;
        public float MaxMp;
        public Vector3 TargetPosition;
        public Vector3 PlayerPosition;
        public PropertyTypeEnum PropertyType;
        public float DiffValue;

        public bool Equals(PlayerHpItemData other)
        {
            return PlayerId == other.PlayerId && Name == other.Name && CurrentHp.Equals(other.CurrentHp) && MaxHp.Equals(other.MaxHp) && CurrentMp.Equals(other.CurrentMp) && MaxMp.Equals(other.MaxMp) && TargetPosition.Equals(other.TargetPosition) && PlayerPosition.Equals(other.PlayerPosition) && PropertyType == other.PropertyType && DiffValue.Equals(other.DiffValue);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerHpItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(PlayerId);
            hashCode.Add(Name);
            hashCode.Add(CurrentHp);
            hashCode.Add(MaxHp);
            hashCode.Add(CurrentMp);
            hashCode.Add(MaxMp);
            hashCode.Add(TargetPosition);
            hashCode.Add(PlayerPosition);
            hashCode.Add((int)PropertyType);
            hashCode.Add(DiffValue);
            return hashCode.ToHashCode();
        }
    }
}
