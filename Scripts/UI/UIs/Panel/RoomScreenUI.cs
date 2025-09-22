using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using Network.Data;
using Network.Server.PlayFab;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class RoomScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabRoomManager _playFabRoomManager;
        private PlayFabAccountManager _playFabAccountManager;
        private RepeatedTask _refreshTask;
        [SerializeField]
        private TextMeshProUGUI roomNameText;
        [SerializeField]
        private Button quitButton;
        [SerializeField]
        private Button startButton;
        [SerializeField]
        private Button refreshButton;
        [SerializeField]
        private ContentItemList playerContentListPrefab;
        [SerializeField]
        private ContentItemList roomContentListPrefab;
        
        public override UIType Type => UIType.RoomScreen;
        public override UICanvasType CanvasType => UICanvasType.SecondPanel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabRoomManager playFabRoomManager, PlayFabAccountManager playFabAccountManager)
        {
            // TODO: Implement RoomScreenUI
            _uiManager = uiManager;
            _playFabAccountManager = playFabAccountManager;
            _playFabRoomManager = playFabRoomManager;
            _refreshTask = RepeatedTask.Instance;
            _playFabRoomManager.OnPlayerJoined += OnSetRoomInfo;
            _playFabRoomManager.OnRefreshPlayers += OnRefreshPlayers;
            _playFabRoomManager.OnCreateRoom += OnSetRoomInfo;
            _playFabRoomManager.OnJoinRoom += OnSetRoomInfo;
            quitButton.BindDebouncedListener(() => _playFabRoomManager.LeaveRoom());
            startButton.BindDebouncedListener(() => _playFabRoomManager.StartGame());
            refreshButton.BindDebouncedListener(() => _playFabRoomManager.GetInvitablePlayers());
            _refreshTask.StartRepeatingTask(AutoRefresh, 3f);
            
        }
        
        private void AutoRefresh()
        {
            _playFabRoomManager.RefreshRoomData();
        }

        private void OnRefreshPlayers(PlayerReadOnlyData[] playersData)
        {
            if (playersData is { Length: > 0 })
            {
                var list = playersData.Select(player => new RoomMemberItemData
                {
                    Id = player.Id,
                    Name = player.Nickname,
                    PlayerId = player.PlayerId,
                    Level = player.Level,
                    IsFriend = false,
                    IsSelf = player.PlayerId == PlayFabData.PlayFabId.Value,
                    OnInviteClick = playerId => _playFabRoomManager.InvitePlayer(playerId),
                    OnAddFriendClick = playerId => _playFabAccountManager.SendFriendRequest(playerId),
                }).ToDictionary(x => x.Id, x => x);
                playerContentListPrefab.SetItemList(list);
                return;
            }
            Debug.LogError("Failed to set players data");
        }

        private void OnSetRoomInfo(RoomData roomInfo)
        {
            if (roomInfo.PlayersInfo != null)
            {
                roomNameText.text = roomInfo.RoomCustomInfo.RoomName;
                if (roomInfo.PlayersInfo is { Length: > 0 })
                {
                    var dic = new Dictionary<int, RoomMemberItemData>();
                    for (int i = 0; i < roomInfo.PlayersInfo.Length; i++)
                    {
                        var player = roomInfo.PlayersInfo[i];
                        var playerInfo = JsonUtility.FromJson<PlayerReadOnlyData>(player);
                        var itemData = new RoomMemberItemData();
                        itemData.Id = playerInfo.Id;
                        itemData.Name = playerInfo.Nickname;
                        itemData.PlayerId = playerInfo.PlayerId;
                        itemData.Level = playerInfo.Level;
                        itemData.IsFriend = false;
                        itemData.IsSelf = playerInfo.PlayerId == PlayFabData.PlayFabId.Value;
                        itemData.OnInviteClick = playerId => _playFabRoomManager.InvitePlayer(playerId);
                        itemData.OnAddFriendClick = playerId => _playFabAccountManager.SendFriendRequest(playerId);
                        dic.Add(itemData.Id, itemData);
                    }
                    roomContentListPrefab.SetItemList(dic);
                }
                return;
            }
            
            Debug.LogError("Failed to set RoomInfo data");
        }

        private void OnDestroy()
        {
            _playFabRoomManager.OnPlayerJoined -= OnSetRoomInfo;
            _playFabRoomManager.OnRefreshPlayers -= OnRefreshPlayers;
            _playFabRoomManager.OnCreateRoom -= OnSetRoomInfo;
            _playFabRoomManager.OnJoinRoom -= OnSetRoomInfo;
            _refreshTask.StopRepeatingTask(AutoRefresh);
        }
    }
}