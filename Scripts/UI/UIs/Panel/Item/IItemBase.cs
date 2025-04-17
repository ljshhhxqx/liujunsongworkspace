using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using UniRx;
using UnityEngine;

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
    
    public struct BagItemData : IItemBaseData
    {
        public string ItemName;
        public Sprite Icon;
        public int Index;
        public int Stack;
        public string Description;
        public PlayerItemType PlayerItemType;
        public bool IsEquip;
        public bool IsLock;
        public int MaxStack; // 最大堆叠数量
        public Action<int, int> OnUseItem;
        public Action<int, int> OnDropItem;
        public Action<int, int> OnExchangeItem;
        public Action<int, bool> OnLockItem;
        public Action<int, bool, PlayerItemType> OnEquipItem;
        public Action<int, int> OnSellItem;
    }

    public struct EquipItemData : IItemBaseData
    {
        public string ItemName;
        public string Description;
        public bool IsLock;
        public EquipmentPart EquipmentPartType;
        public Action<int, bool> OnLockItem;
        public Action<int, bool> OnEquipItem;
        public Action<int> OnDropItem;
    }
}
