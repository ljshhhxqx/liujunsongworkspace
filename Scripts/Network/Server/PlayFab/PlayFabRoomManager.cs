using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using Data;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Coroutine;
using Network.Data;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.MultiplayerModels;
using Tool.Coroutine;
using UI.UIBase;
using UI.UIs.Panel;
using UnityEngine;
using VContainer;
using EntityKey = PlayFab.CloudScriptModels.EntityKey;

namespace Network.Server.PlayFab
{
    public class PlayFabRoomManager
    {
        private readonly UIManager _uiManager;
        private readonly IPlayFabClientCloudScriptCaller _playFabClientCloudScriptCaller;
        private readonly PlayerDataManager _playerDataManager;
        
        private int _pollCount = 0;
        private bool _isMatchmaking;  
        private const int MaxPollAttempts = 12; // 最大轮询次数
        private RoomData _currentRoomData;
        public RoomsData RoomsData { get; private set; }
        public string CurrentRoomId { get; private set; }
        public bool IsMatchmaking {
            get => _isMatchmaking;

            private set
            {
                _isMatchmaking = value;
                OnMatchmakingChanged?.Invoke(_isMatchmaking);
            }
        } 
        public event Action<List<RoomData>> OnRefreshRoomData;
        public event Action<RoomData> OnCreateRoom;
        public event Action<RoomData> OnPlayerJoined;
        public event Action<RoomData> OnJoinRoom;
        public event Action<RoomData> OnRefreshRoom;
        public event Action<bool> OnMatchmakingChanged;
        public event Action<InvitablePlayersData> OnRefreshPlayers;
        
        
        [Inject]
        private PlayFabRoomManager(UIManager uiManager, IPlayFabClientCloudScriptCaller playFabClientCloudScriptCaller, PlayerDataManager playerDataManager)
        {
            _uiManager = uiManager;
            _playerDataManager = playerDataManager;
            _playFabClientCloudScriptCaller = playFabClientCloudScriptCaller;
        }

        #region 匹配
        public void CreateOrJoinMatchingRoom()
        {
            var createRequest = new CreateMatchmakingTicketRequest
            {
                Creator = new MatchmakingPlayer
                {
                    Entity = new()
                    {
                        Id = PlayFabData.EntityKey.Value.Id, // 替换为你的玩家ID获取逻辑
                        Type = PlayFabData.EntityKey.Value.Type
                    }
                },
                GiveUpAfterSeconds = 60,
                QueueName = "CustomQueue"
            };
            IsMatchmaking = true;
            PlayFabMultiplayerAPI.CreateMatchmakingTicket(createRequest, OnMatchingRoomCreated, OnRoomCreationError);
        }

        private void OnMatchingRoomCreated(CreateMatchmakingTicketResult result)
        {
            Debug.Log($"Room created with TicketId: {result.TicketId}");
            PollMatchmakingTicket(result.TicketId);
        }

        private void OnRoomCreationError(PlayFabError error)
        {
            Debug.LogError($"Room creation error: {error.GenerateErrorReport()}");
            IsMatchmaking = false;
        }

        public void PollMatchmakingTicket(string ticketId)
        {
            if (_pollCount >= MaxPollAttempts)
            {
                Debug.LogError("Matchmaking timeout. Please try again.");
                IsMatchmaking = false;
                return;
            }

            _pollCount++;
            var request = new GetMatchmakingTicketRequest
            {
                TicketId = ticketId,
                QueueName = "CustomQueue"
            };

            PlayFabMultiplayerAPI.GetMatchmakingTicket(request, OnMatchFound, OnMatchmakingError);
        }

        private void OnMatchmakingError(PlayFabError error)
        {
            Debug.LogError($"Matchmaking error: {error.GenerateErrorReport()}");
            IsMatchmaking = false;
        }

        private void OnMatchFound(GetMatchmakingTicketResult result)
        {
            IsMatchmaking = false;
            if (result.Status == "Matched")
            {
                Debug.Log("Match found!");
                _uiManager.ShowTips("已找到匹配，是否立即加入游戏？", () =>
                {
                    ConnectToGameServer(result);
                }, CancelMatchmaking);
            }
            else if (result.Status is "Canceled" or "Failed")
            {
                Debug.LogError("Matchmaking canceled or failed.");
            }
            else
            {
                DelayInvoker.DelayInvoke(5, () => PollMatchmakingTicket(result.TicketId));
            }
        }

        private void ConnectToGameServer(GetMatchmakingTicketResult result)
        {
            // 在这里添加连接到游戏服务器的具体实现
            Debug.Log("Connecting to game server...");
        }

        public void CancelMatchmaking()
        {
            IsMatchmaking = false;
            if (CurrentRoomId == null)
            {
                Debug.LogWarning("No matchmaking ticket to cancel.");
                return;
            }

            var cancelRequest = new CancelMatchmakingTicketRequest
            {
                TicketId = CurrentRoomId,
                QueueName = "CustomQueue"
            };

            PlayFabMultiplayerAPI.CancelMatchmakingTicket(cancelRequest, OnMatchmakingCancelled, OnMatchmakingCancelError);
        }

        private void OnMatchmakingCancelled(CancelMatchmakingTicketResult result)
        {
            Debug.Log("Matchmaking cancelled successfully.");
            CurrentRoomId = null;
            // statusText.text = "Matchmaking cancelled.";
            // pollCount = 0;
            // matchButton.interactable = true;
            // cancelButton.interactable = false;
        }

        private void OnMatchmakingCancelError(PlayFabError error)
        {
            Debug.LogError($"Failed to cancel matchmaking: {error.GenerateErrorReport()}");
            //statusText.text = "Failed to cancel matchmaking.";
        }

        #endregion
        
        public void CreateRoom(RoomCustomInfo roomCustomInfo)
        {
            var roomData = new RoomData
            {
                RoomId = "", // 后端生成
                CreatorId = PlayFabData.PlayFabId.Value,
                CreatorName = PlayFabData.PlayerReadOnlyData.Value.Nickname,
                RoomCustomInfo = roomCustomInfo,
                RoomStatus = 0, 
            };

            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "CreateRoom",
                FunctionParameter = new { roomData = JsonUtility.ToJson(roomData), playerId = PlayFabData.PlayFabId.Value },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnCreateRoomSuccess, OnError);
        }

        private void OnCreateRoomSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.FunctionResult.ParseCloudScriptResultToDic();

            if (data.TryGetValue("roomData", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomData>(value.ToString());
                CurrentRoomId = roomData.RoomId;
                _uiManager.SwitchUI<RoomScreenUI>(() =>
                {
                    _currentRoomData = roomData;
                    OnCreateRoom?.Invoke(roomData);
                });
                Debug.Log("房间创建成功");
            }
        }

        private void OnError(PlayFabError error)
        {
            Debug.LogError($"Failed to create room: {error.GenerateErrorReport()}");
        }
        
        public void RequestJoinRoom(string roomId, string roomPassword)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "RequestJoinRoom",
                FunctionParameter = new { roomId = roomId, playerId = PlayFabData.PlayFabId.Value, roomPassword = roomPassword },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };

            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnRequestJoinRoomSuccess, OnError);
        }

        private void OnRequestJoinRoomSuccess(ExecuteCloudScriptResult executeCloudScriptResult)
        {
            Debug.Log("申请加入房间成功");
        }
        
        public void ApproveJoinRequest(RoomData roomData)
        {
            if (!_uiManager.IsUIOpen(UIType.RoomScreen))
            {
                _uiManager.SwitchUI<RoomScreenUI>();
            }
            _currentRoomData = roomData;
            OnPlayerJoined?.Invoke(roomData);
        }
        
        public void InvitePlayer(string id)
        {
            var request = new ExecuteEntityCloudScriptRequest()
            {
                FunctionName = "InvitePlayer",
                FunctionParameter = new { roomId = CurrentRoomId, playerId = id },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnInvitePlayerSuccess, OnError);

        }

        private void OnInvitePlayerSuccess(ExecuteCloudScriptResult executeCloudScriptResult)
        {
            Debug.Log("邀请玩家成功");
        }
        
        /// <summary>
        /// 离开房间
        /// </summary>
        /// <param name="roomData">没有数据表示退出当前房间，有数据表示其他人退出房间</param>
        public void LeaveRoom(RoomData roomData = default)
        {
            if (roomData.RoomId == null)
            {
                var request = new ExecuteEntityCloudScriptRequest
                {
                    FunctionName = "LeaveRoom",
                    FunctionParameter = new { roomId = CurrentRoomId, playerId = PlayFabData.PlayFabId.Value },
                    GeneratePlayStreamEvent = true
                };
                _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnLeaveRoomSuccess, OnError);
            }
            else
            {
                OnPlayerJoined?.Invoke(roomData);
                Debug.Log("有人退出房间成功");
            }
        }

        private void OnLeaveRoomSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.FunctionResult.ParseCloudScriptResultToDic();
            if (data.TryGetValue("roomData", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomData>(value.ToString());
                _currentRoomData = roomData;
                OnPlayerJoined?.Invoke(roomData);
                CurrentRoomId = null;
                _uiManager.SwitchUI<MainScreenUI>();
                Debug.Log("退出房间成功");
            }
        }
        
        public void GetAllRooms()
        {
            var request = new ExecuteEntityCloudScriptRequest()
            {
                FunctionName = "GetAllRooms",
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };
            
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnGetAllRoomsSuccess, OnError);
        }

        private void OnGetAllRoomsSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.FunctionResult.ParseCloudScriptResultToDic();
            if (data.TryGetValue("allRooms", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomsData>(value.ToString());
                if (_uiManager.IsUIOpen(UIType.Room))
                {
                    OnRefreshRoomData?.Invoke(roomData.AllRooms);
                }
                else
                {
                    _uiManager.SwitchUI<RoomListScreenUI>(() =>
                    {
                        OnRefreshRoomData?.Invoke(roomData.AllRooms);
                    });
                }
                RoomsData = roomData;
                Debug.Log("房间数据更新成功");
            }
        }

        public void GetInvitablePlayers()
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "GetPlayersWithStatus",
                GeneratePlayStreamEvent = true,
                FunctionParameter = new { status = "Online" },
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnGetInvitablePlayersSuccess, OnError);
        }

        private void OnGetInvitablePlayersSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.FunctionResult.ParseCloudScriptResultToDic();
            if (data.TryGetValue("players", out var value))
            {
                var players = JsonUtility.FromJson<InvitablePlayersData>(value.ToString());
                if (_uiManager.IsUIOpen(UIType.Room))
                {
                    OnRefreshPlayers?.Invoke(players);
                }
            }
        }

        public void RefreshRoomData()
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "CheckPlayerOnline",
                GeneratePlayStreamEvent = true,
                FunctionParameter = new { roomId = CurrentRoomId },
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnRefreshRoomDataSuccess, OnError);
        }

        private void OnRefreshRoomDataSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.FunctionResult.ParseCloudScriptResultToDic();
            if (data.TryGetValue("roomData", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomData>(value.ToString());
                OnRefreshRoom?.Invoke(roomData);
            }
        }

        public void StartGame()
        {
            // TODO: 根据房间性质，开启一个云服务器或者本地服务器进行游戏
            var operation = ResourceManager.Instance.LoadSceneAsync(_currentRoomData.RoomCustomInfo.MapType.ToString());
            operation.Completed += (op) =>
            {
                _playerDataManager.InitRoomPlayer(_currentRoomData);
            };
        }

        public IEnumerable<RoomData> GetFilteredRooms(string inputText)
        {
            return RoomsData.AllRooms.Where(room => FilterByIdOrName(room.RoomId, room.RoomCustomInfo.RoomName, inputText));
        }

        private bool FilterByIdOrName(string id, string name, string inputText)
        {
            if (string.IsNullOrEmpty(inputText))
            {
                return true;
            }
            var isNumeric = int.TryParse(inputText, out _);
            return isNumeric ? id.Contains(inputText) : name.Contains(inputText);
        }
    }
}