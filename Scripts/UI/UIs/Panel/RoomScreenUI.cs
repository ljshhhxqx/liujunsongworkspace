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
        
        private readonly Dictionary<int, RoomMemberItemData> _roomInevitablePlayers = new Dictionary<int, RoomMemberItemData>();
        private readonly Dictionary<int, RoomMemberItemData> _roomPlayers = new Dictionary<int, RoomMemberItemData>();

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
            // _playFabAccountManager.OnFriendStatusChanged += OnFriendStatusChanged;
            quitButton.BindDebouncedListener(() => _playFabRoomManager.LeaveRoom());
            startButton.BindDebouncedListener(() => _playFabRoomManager.StartGame());
            refreshButton.BindDebouncedListener(() => _playFabRoomManager.GetInvitablePlayers());
            _refreshTask.StartRepeatingTask(AutoRefresh, 2.5f);
            
        }

        private void AutoRefresh()
        {
            _playFabRoomManager.RefreshRoomData();
        }

        private void OnRefreshPlayers(PlayerReadOnlyData[] playersData)
        {
            _roomInevitablePlayers.Clear();
            for (int i = 0; i < playersData.Length; i++)
            {
                var player = playersData[i];
                var data = new RoomMemberItemData();
                data.Id = player.Id;
                data.Name = player.Nickname;
                data.PlayerId = player.PlayerId;
                data.Level = player.Level;
                data.IsFriend = false;
                data.IsSelf = player.PlayerId == PlayFabData.PlayFabId.Value;
                data.OnInviteClick = playerId => _playFabRoomManager.InvitePlayer(playerId);
                _roomInevitablePlayers.Add(data.Id, data);
                playerContentListPrefab.SetItemList(_roomInevitablePlayers);
            }
        }

        private void OnSetRoomInfo(RoomData roomInfo)
        {
            if (roomInfo.PlayersInfo != null)
            {
                roomNameText.text = roomInfo.RoomCustomInfo.RoomName;
                _roomPlayers.Clear();
                for (int i = 0; i < roomInfo.PlayersInfo.Length; i++)
                {
                    var player = roomInfo.PlayersInfo[i];
                    var playerInfo = player;
                    var itemData = new RoomMemberItemData();
                    itemData.Id = playerInfo.Id;
                    itemData.Name = playerInfo.Nickname;
                    itemData.PlayerId = playerInfo.PlayerId;
                    itemData.Level = playerInfo.Level;
                    itemData.IsFriend = false;
                    itemData.IsSelf = playerInfo.PlayerId == PlayFabData.PlayFabId.Value;
                    _roomPlayers.Add(itemData.Id, itemData);
                }
                roomContentListPrefab.SetItemList(_roomPlayers);
            }
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