using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Panel.ItemList;
using Network.Server.PlayFab;
using TMPro;
using Tool.Coroutine;
using UI.UIBase;
using UI.UIs.Common;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

namespace UI.UIs.Panel
{
    public class RoomListScreenUI : ScreenUIBase
    {
        private UIManager _uiManager;
        private PlayFabRoomManager _playFabRoomManager;
        private RepeatedTask _repeatedTask;
        [SerializeField]
        private Button refreshButton;
        [SerializeField]
        private ContentItemList roomListContent;
        [SerializeField]
        private TMP_InputField searchInputField;
        private List<RoomData> _roomList;
        public override UIType Type => UIType.RoomScreen;
        public override UICanvasType CanvasType => UICanvasType.Panel;
        
        [Inject]
        private void Init(PlayFabRoomManager playFabRoomManager, UIManager uiManager, RepeatedTask repeatedTask)
        {
            _playFabRoomManager = playFabRoomManager;
            _uiManager = uiManager;
            _repeatedTask = repeatedTask;
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
        }
        
        private void AutoRefresh()
        {
            _playFabRoomManager.GetAllRooms();
        }

        private void OnRefreshRoomList(List<RoomData> roomList)
        {
            _roomList = roomList;
            var dataArray = _playFabRoomManager.GetFilteredRooms(searchInputField.text)
                .Select(room => new RoomListItemData
                {
                    RoomId = room.RoomId,
                    RoomName = room.RoomCustomInfo.RoomName,
                    RoomType = room.RoomCustomInfo.RoomType == 0 ? "远程" : "本地",
                    RoomOwnerName = room.CreatorId,
                    RoomStatus = $"{room.PlayersInfo.Count}/{room.RoomCustomInfo.MaxPlayers}({(room.RoomStatus == 0 ? "等待中" : "游戏中")})",
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
                }).ToArray();
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
