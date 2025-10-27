using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.Coroutine;
using Data;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.SecondPanel
{
    public class FriendScreenUI : ScreenUIBase
    {
        public override UIType Type => UIType.FriendScreen;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;
        private PlayFabAccountManager _accountManager;
        
        [SerializeField]
        private ContentItemList friendContentList;
        [SerializeField]
        private ContentItemList searchContentList;
        [SerializeField]
        private TMP_InputField searchFriendsInputField;
        [SerializeField]
        private TMP_InputField addFriendInputField;
        [SerializeField]
        private Button refreshFriendsButton;
        [SerializeField]
        private Button quitButton;
        private Dictionary<int, FriendItemData> _friendDic = new Dictionary<int, FriendItemData>();
        private Dictionary<int, FriendItemData> _searchDic = new Dictionary<int, FriendItemData>();

        [Inject]
        private void Init(PlayFabAccountManager accountManager, UIManager uiManager)
        {
            _accountManager = accountManager;
            _accountManager.OnRefreshFriendList += OnAutoRefreshFriendList;
            _accountManager.OnGetNonFriendList += OnAutoRefreshNonFriendList;
            _accountManager.OnFriendStatusChanged += OnFriendStatusChanged;
            searchFriendsInputField.onValueChanged.RemoveAllListeners();
            refreshFriendsButton.onClick.RemoveAllListeners();
            quitButton.onClick.RemoveAllListeners();
            addFriendInputField.onValueChanged.RemoveAllListeners();
            searchFriendsInputField.onValueChanged.AddListener(OnSearchFriends);
            addFriendInputField.onValueChanged.AddListener(OnSearchNoFriends);
            refreshFriendsButton.BindDebouncedListener(RefreshFriendList);
            quitButton.BindDebouncedListener(() =>
            {
                uiManager.CloseUI(Type);
            });
            RepeatedTask.Instance.StartRepeatingTask(RefreshFriendList, 5);
        }

        private void OnFriendStatusChanged(int id, FriendStatus status)
        {
            if (_friendDic.TryGetValue(id, out var itemData))
            {
                if (itemData.FriendStatus == FriendStatus.Friends && status == FriendStatus.None)
                {
                    _friendDic.Remove(id);
                    friendContentList.RemoveItem(id);
                    itemData.FriendStatus = status;
                    _searchDic.Add(id, itemData);
                    searchContentList.AddItem<FriendItemData, FriendItem>(id, itemData);
                }
                else
                {
                    itemData.FriendStatus = status;
                    _friendDic[id] = itemData;
                    friendContentList.SetItemList(_friendDic);
                }
            }
            else if (_searchDic.TryGetValue(id, out itemData))
            {
                if (itemData.FriendStatus == FriendStatus.None && status != FriendStatus.None)
                {
                    _searchDic.Remove(id);
                    searchContentList.RemoveItem(id);
                    itemData.FriendStatus = status;
                    _friendDic.Add(id, itemData);
                    friendContentList.AddItem<FriendItemData, FriendItem>(id, itemData);
                }
                else
                {
                    itemData.FriendStatus = status;
                    _searchDic[id] = itemData;
                    searchContentList.SetItemList(_searchDic);
                }
            }
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
            _accountManager.OnFriendStatusChanged -= OnFriendStatusChanged;
            searchFriendsInputField.onValueChanged.RemoveAllListeners();
            refreshFriendsButton.onClick.RemoveAllListeners();
            quitButton.onClick.RemoveAllListeners();
            addFriendInputField.onValueChanged.RemoveAllListeners();
            RepeatedTask.Instance.StopRepeatingTask(RefreshFriendList);
        }

        private void RefreshFriendList()
        {
            _accountManager.RefreshFriendList(false);
            _accountManager.GetNonFriendOnlinePlayers(showLoading:false);
        }

        private void OnRefreshPlayers(List<PlayerReadOnlyData> players)
        {
            _searchDic.Clear();
            if (players != null && players.Count > 0)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    var itemData = new FriendItemData();
                    itemData.Name = player.Nickname;
                    itemData.Id = player.Id;
                    itemData.PlayerId = player.PlayerId;
                    itemData.Level = player.Level;
                    itemData.OnAddFriend = AddFriend;
                    _searchDic.Add(player.Id, itemData);
                }
                searchContentList.SetItemList(_searchDic);
            }
        }

        private void OnRefreshFriendList(List<FriendData> friendList)
        {
            _friendDic.Clear();
            if (friendList != null && friendList.Count > 0)
            {
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
                    _friendDic.Add(friend.Id, itemData);
                }
                friendContentList.SetItemList(_friendDic);
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
