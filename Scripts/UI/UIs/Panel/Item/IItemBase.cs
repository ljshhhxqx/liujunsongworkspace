using System;
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

    public interface IItemBaseData
    {
        
    }
    
    public class RoomMemberItemData : IItemBaseData
    {
        public string PlayerId;
        public string Name;
        public int Level;
        public bool IsFriend;
        public bool IsSelf;
        public Action<string> OnAddFriendClick;
    }
    
    public class RoomInviteItemData : IItemBaseData
    {
        public string PlayerId;
        public string Name;
        public int Level;
        public Action<string> OnInviteClick;
    }

    public class RoomListItemData : IItemBaseData
    {
        public string RoomId;
        public string RoomName;
        public string RoomOwnerName;
        public string RoomStatus;
        public string RoomType;
        public string HasPassword;
        public Action<string> OnJoinClick;
    }

    public class PropertyItemData : IItemBaseData
    {
        public string Name;
        public PropertyConsumeType ConsumeType;
        public ReactiveProperty<PropertyType> CurrentProperty;
        public ReactiveProperty<PropertyType> MaxProperty;
    }
    
    public class BagItemData : IItemBaseData
    {
        public string ItemName;
        public Sprite Icon;
        public int Index;
        public int MaxStack; // 最大堆叠数量
        public event Action OnUseItem;
        // todo:添加更多的Action来配合实际的业务逻辑
    }
    
    public struct BagItemExchangeData
    {
        public BagItemData FromItem;
        public BagItemData ToItem;
        public int FromStack;
        public int ToStack;
        public int FromIndex;
        public int ToIndex;
    }
}
