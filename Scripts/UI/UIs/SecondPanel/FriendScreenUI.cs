using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using UI.UIBase;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
{
    public class FriendScreenUI : ScreenUIBase
    {
        public override UIType Type => UIType.FriendScreen;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
        private PlayFabAccountManager _accountManager;
        private PlayFabRoomManager _roomManager;
        
        [SerializeField]
        private ContentItemList friendContentList;
        [SerializeField]
        private ContentItemList searchContentList;
        private List<FriendData> _friendList = new List<FriendData>();
        private List<PlayerReadOnlyData> _searchList = new List<PlayerReadOnlyData>();

        [Inject]
        private void Init(PlayFabAccountManager accountManager, PlayFabRoomManager roomManager)
        {
            _accountManager = accountManager;
            _roomManager = roomManager;
            _accountManager.OnRefreshFriendList += OnRefreshFriendList;
            _roomManager.OnRefreshPlayers += OnRefreshPlayers;
            _roomManager.GetInvitablePlayers();
            _accountManager.RefreshFriendList();
        }

        private void OnRefreshPlayers(PlayerReadOnlyData[] players)
        {
            _searchList.Clear();
            if (players != null && players.Length > 0)
            {
                _searchList = players.ToList();
                var dic = new Dictionary<int, FriendItemData>();
                for (int i = 0; i < players.Length; i++)
                {
                    var player = players[i];
                    var itemData = new FriendItemData();
                    itemData.Name = player.Nickname;
                    itemData.Id = player.Id;
                    itemData.PlayerId = player.PlayerId;
                    itemData.Level = player.Level;
                    itemData.OnAddFriend = AddFriend;
                    dic.Add(player.Id, itemData);
                }
                searchContentList.SetItemList(dic);
            }
        }

        private void OnRefreshFriendList(List<FriendData> friendList)
        {
            _friendList = friendList;
            if (friendList != null && friendList.Count > 0)
            {
                var dic = new Dictionary<int, FriendItemData>();
                for (int i = 0; i < friendList.Count; i++)
                {
                    var friend = friendList[i]; 
                    var itemData = new FriendItemData();
                    itemData.Name = friend.Username;
                    itemData.IconUrl = friend.IconUrl;
                    itemData.Id = friend.Id;
                    itemData.Status = friend.PlayerStatus;
                    itemData.Level = friend.Level;
                    itemData.OnRemove = RemoveFriend;
                    itemData.OnReject = RejectFriend;
                    itemData.OnAccept = AcceptFriend;
                    dic.Add(i, itemData);
                }
                friendContentList.SetItemList(dic);
            }
        }
        
        private void RemoveFriend(string playFabId)
        {
            _accountManager.RemoveFriend(playFabId);
        }
        
        private void RejectFriend(string playFabId)
        {
            _accountManager.ChangeFriendStatus(playFabId, FriendStatus.Blocked);
        }
        
        private void AcceptFriend(string playFabId)
        {
            _accountManager.ChangeFriendStatus(playFabId, FriendStatus.Friends);
        }
        
        private void AddFriend(string playFabId)
        {
            _accountManager.SendFriendRequest(playFabId);
        }
    }
}
