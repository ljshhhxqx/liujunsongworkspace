using System.Linq;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using TMPro;
using UI.UIBase;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.Panel
{
    public class RoomListScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabRoomManager _playFabRoomManager;
        private RepeatedTask _repeatedTask;
        [SerializeField]
        private Button refreshButton;
        [SerializeField]
        private Button quitButton;
        [SerializeField]
        private ContentItemList roomListContent;
        [SerializeField]
        private TMP_InputField searchInputField;
        private RoomData[] _roomList;
        public override UIType Type => UIType.RoomList;
        public override UICanvasType CanvasType => UICanvasType.Panel;
        
        [Inject]
        private void Init(PlayFabRoomManager playFabRoomManager, UIManager uiManager)
        {
            _playFabRoomManager = playFabRoomManager;
            _uiManager = uiManager;
            _repeatedTask = RepeatedTask.Instance;
            _playFabRoomManager.OnRefreshRoomData += OnRefreshRoomList;
            _repeatedTask.StartRepeatingTask(AutoRefresh, 5);
            refreshButton.BindDebouncedListener(() =>
            {
                _playFabRoomManager.GetAllRooms();
            });
            searchInputField.onValueChanged.AddListener(text =>
            {
                OnRefreshRoomList(_roomList);
            });
            quitButton.BindDebouncedListener(() =>
            {
                _uiManager.CloseUI(Type);
            });
        }
        
        private void AutoRefresh()
        {
            _playFabRoomManager.GetAllRooms();
        }

        private void OnRefreshRoomList(RoomData[] roomList)
        {
            _roomList = roomList;
            var dataArray = _playFabRoomManager.GetFilteredRooms(searchInputField.text)
                .Select(room => new RoomListItemData
                {
                    Id = room.Id,
                    RoomId = room.RoomId,
                    RoomName = room.RoomCustomInfo.RoomName,
                    RoomType = room.RoomCustomInfo.RoomType == 0 ? "远程" : "本地",
                    RoomOwnerName = room.CreatorId,
                    RoomStatus = $"{room.PlayersInfo.Length}/{room.RoomCustomInfo.MaxPlayers}({(room.RoomStatus == 0 ? "等待中" : "游戏中")})",
                    HasPassword = string.IsNullOrEmpty(room.RoomCustomInfo.RoomPassword) ? "无" : "有",
                    OnJoinClick = roomId =>
                    {
                        if (string.IsNullOrEmpty(searchInputField.text))
                        {
                            _playFabRoomManager.RequestJoinRoom(roomId, searchInputField.text);
                        }
                        else
                        {
                            _uiManager.ShowPasswordInput(room.RoomCustomInfo.RoomPassword, isTrue =>
                            {
                                if (isTrue)
                                {
                                    _playFabRoomManager.RequestJoinRoom(roomId, searchInputField.text);
                                }
                                else
                                {
                                    _uiManager.ShowTips("密码错误");
                                }
                            });
                        }
                    },
                }).ToDictionary(x => x.Id, x => x);
            roomListContent.SetItemList(dataArray);
        }

        private void OnDestroy()
        {
            searchInputField.onValueChanged.RemoveAllListeners();
            _playFabRoomManager.OnRefreshRoomData -= OnRefreshRoomList;
            _repeatedTask.StopRepeatingTask(AutoRefresh);
        }
    }
}
