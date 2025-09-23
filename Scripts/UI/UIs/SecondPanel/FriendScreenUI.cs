using System.Collections.Generic;
using System.Linq;
using Data;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using TMPro;
using UI.UIBase;
using UnityEngine;
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
        [SerializeField]
        private TMP_InputField searchFriendsInputField;
        [SerializeField]
        private TMP_InputField addFriendInputField;
        private List<FriendData> _friendList = new List<FriendData>();
        private List<PlayerReadOnlyData> _searchList = new List<PlayerReadOnlyData>();

        [Inject]
        private void Init(PlayFabAccountManager accountManager, PlayFabRoomManager roomManager)
        {
            _accountManager = accountManager;
            _roomManager = roomManager;
            _accountManager.OnRefreshFriendList += OnAutoRefreshFriendList;
            _accountManager.OnGetNonFriendList += OnAutoRefreshNonFriendList;
            searchFriendsInputField.onValueChanged.RemoveAllListeners();
            addFriendInputField.onValueChanged.RemoveAllListeners();
            searchFriendsInputField.onValueChanged.AddListener(OnSearchFriends);
            addFriendInputField.onValueChanged.AddListener(OnSearchNoFriends);
            RepeatedTask.Instance.StartRepeatingTask(RefreshFriendList, 3);
        }

        private void OnAutoRefreshNonFriendList(List<PlayerReadOnlyData> list)
        {
            OnSearchNoFriends(addFriendInputField.text);
        }

        private void OnAutoRefreshFriendList(List<FriendData> friendList)
        {
            OnSearchFriends(searchFriendsInputField.text);
        }

        private void OnSearchNoFriends(string str)
        {
            var list = _accountManager.GetFilteredPlayer(str).ToList();
            OnRefreshPlayers(list);
        }

        private void OnSearchFriends(string str)
        {
            var list = _accountManager.GetFilteredFriend(str).ToList();
            OnRefreshFriendList(list);
        }

        private void OnDestroy()
        {
            _accountManager.OnRefreshFriendList -= OnAutoRefreshFriendList;
            _accountManager.OnGetNonFriendList -= OnAutoRefreshNonFriendList;
            RepeatedTask.Instance.StopRepeatingTask(RefreshFriendList);
        }

        private void RefreshFriendList()
        {
            _roomManager.GetInvitablePlayers();
            _accountManager.GetNonFriendOnlinePlayers();
        }

        private void OnRefreshPlayers(List<PlayerReadOnlyData> players)
        {
            _searchList.Clear();
            if (players != null && players.Count > 0)
            {
                var dic = new Dictionary<int, FriendItemData>();
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    _searchList.Add(player);
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
                    itemData.PlayerId = friend.PlayFabId;
                    itemData.LastLoginTime = friend.LastOnline;
                    itemData.Status = friend.PlayerStatus;
                    itemData.FriendStatus = friend.FriendStatus;
                    itemData.Level = friend.Level;
                    itemData.OnRemove = RemoveFriend;
                    itemData.OnReject = RejectFriend;
                    itemData.OnAccept = AcceptFriend;
                    dic.Add(i, itemData);
                    _friendList.Add(friend);
                }
                friendContentList.SetItemList(dic);
            }
        }
        
        private void RemoveFriend(int id, string playFabId)
        {
            _accountManager.RemoveFriend(id, playFabId);
        }
        
        private void RejectFriend(int id,string playFabId)
        {
            _accountManager.RejectFriendRequest(id, playFabId);
        }
        
        private void AcceptFriend(int id,string playFabId)
        {
            _accountManager.AcceptFriendRequest(id, playFabId);
        }
        
        private void AddFriend(int id,string playFabId)
        {
            _accountManager.SendFriendRequest(id,playFabId);
        }
    }
}
