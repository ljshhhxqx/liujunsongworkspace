using System;
using System.Linq;
using AOTScripts.Tool;
using Data;
using Network.Data;
using Network.Server.PlayFab;
using TMPro;
using Tool.Coroutine;
using UI.UIBase;
using UI.UIs.Common;
using UI.UIs.Panel.ItemList;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.Panel
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
        public override UICanvasType CanvasType => UICanvasType.Panel;

        [Inject]
        private void Init(UIManager uiManager, PlayFabRoomManager playFabRoomManager, PlayFabAccountManager playFabAccountManager, RepeatedTask repeatedTask)
        {
            // TODO: Implement RoomScreenUI
            _uiManager = uiManager;
            _playFabAccountManager = playFabAccountManager;
            _playFabRoomManager = playFabRoomManager;
            _refreshTask = repeatedTask;
            _playFabRoomManager.OnPlayerJoined += OnSetRoomInfo;
            _playFabRoomManager.OnRefreshPlayers += OnRefreshPlayers;
            quitButton.BindDebouncedListener(() => _playFabRoomManager.LeaveRoom());
            startButton.BindDebouncedListener(() => _playFabRoomManager.StartGame());
            refreshButton.BindDebouncedListener(() => _playFabRoomManager.GetInvitablePlayers());
            _refreshTask.StartRepeatingTask(AutoRefresh, 2.5f);
        }
        
        private void AutoRefresh()
        {
            _playFabRoomManager.RefreshRoomData();
        }

        private void OnRefreshPlayers(InvitablePlayersData playersData)
        {
            if (playersData is { Players: { Count: > 0 } })
            {
                var list = playersData.Players.Select(player => new RoomInviteItemData
                {
                    Name = player.Nickname,
                    PlayerId = player.PlayerId,
                    Level = player.Level,
                    OnInviteClick = playerId => _playFabRoomManager.InvitePlayer(playerId),
                }).ToArray();
                playerContentListPrefab.SetItemList(list);
                return;
            }
            Debug.LogError("Failed to set players data");
        }

        private void OnSetRoomInfo(RoomData roomInfo)
        {
            if (roomInfo != null)
            {
                roomNameText.text = roomInfo.RoomCustomInfo.RoomName;
                if (roomInfo.PlayersInfo is { Count: > 0 })
                {
                    var list = roomInfo.PlayersInfo.Select(player => new RoomMemberItemData
                    {
                        Name = player.Nickname,
                        PlayerId = player.PlayerId,
                        Level = player.Level,
                        IsFriend = false,
                        IsSelf = player.PlayerId == PlayFabData.PlayFabId.Value,
                    }).ToArray();
                    roomContentListPrefab.SetItemList(list);
                }
                return;
            }
            
            Debug.LogError("Failed to set RoomInfo data");
        }

        private void OnDestroy()
        {
            _playFabRoomManager.OnPlayerJoined -= OnSetRoomInfo;
            _playFabRoomManager.OnRefreshPlayers -= OnRefreshPlayers;
            _refreshTask.StopRepeatingTask(AutoRefresh);
        }
    }
}