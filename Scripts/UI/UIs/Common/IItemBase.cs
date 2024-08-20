using System;
using Data;
using UnityEngine;

namespace UI.UIs.Common
{
    public interface IItemBase
    {
        void SetData<T>(T data) where T : ItemBaseData, new();
    }
    
    public abstract class ItemBase : MonoBehaviour, IItemBase
    {
        public abstract void SetData<T>(T data) where T : ItemBaseData, new();
    }

    public class ItemBaseData
    {
        
    }
    
    public class RoomMemberItemData : ItemBaseData
    {
        public string PlayerId;
        public string Name;
        public int Level;
        public bool IsFriend;
        public bool IsSelf;
        public Action<string> OnAddFriendClick;
    }
    
    public class RoomInviteItemData : ItemBaseData
    {
        public string PlayerId;
        public string Name;
        public int Level;
        public Action<string> OnInviteClick;
    }

    public class RoomListItemData : ItemBaseData
    {
        public string RoomId;
        public string RoomName;
        public string RoomOwnerName;
        public string RoomStatus;
        public string RoomType;
        public string HasPassword;
        public Action<string> OnJoinClick;
    }
}
