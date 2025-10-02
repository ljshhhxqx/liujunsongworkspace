using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool;
using Cysharp.Threading.Tasks;
using Data;
using Game;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel;
using Network.Data;
using Network.Server.PlayFab;
using Newtonsoft.Json;
using PlayFab;
using PlayFab.CloudScriptModels;
using PlayFab.MultiplayerModels;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Server.PlayFab
{
    public class PlayFabRoomManager
    {
        private readonly UIManager _uiManager;
        private readonly IPlayFabClientCloudScriptCaller _playFabClientCloudScriptCaller;
        private readonly PlayerDataManager _playerDataManager;
        private readonly GameSceneManager _gameSceneManager;
        
        private int _pollCount = 0;
        private bool _isMatchmaking;  
        private const int MaxPollAttempts = 12; // 最大轮询次数
        private RoomData _currentRoomData;
        private MainGameInfo _currentMainGameInfo;
        private GamePlayerInfo _currentGamePlayerInfo;
        public RoomData[] RoomsData { get; private set; } = Array.Empty<RoomData>();
        public static string CurrentRoomId { get; private set; }
        public bool IsMatchmaking {
            get => _isMatchmaking;

            private set
            {
                _isMatchmaking = value;
                OnMatchmakingChanged?.Invoke(_isMatchmaking);
            }
        } 
        public event Action<RoomData[]> OnRefreshRoomData;
        public event Action<RoomData> OnCreateRoom;
        public event Action<RoomData> OnPlayerJoined;
        public event Action<RoomData> OnJoinRoom;
        public event Action<RoomData> OnRefreshRoom;
        public event Action<bool> OnMatchmakingChanged;
        public event Action<PlayerReadOnlyData[]> OnRefreshPlayers;
        public event Action<string, GamePlayerInfo>  OnPlayerInfoChanged;
        
        [Inject]
        private PlayFabRoomManager(UIManager uiManager, IPlayFabClientCloudScriptCaller playFabClientCloudScriptCaller, PlayerDataManager playerDataManager, GameSceneManager gameSceneManager)
        {
            _gameSceneManager = gameSceneManager;
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
            var data = result.ParseCloudScriptResultToDic();

            if (data.TryGetValue("roomData", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomData>(value.ToString());
                CurrentRoomId = roomData.RoomId;
                _uiManager.SwitchUI<RoomScreenUI>(ui =>
                {
                    _currentRoomData = roomData;
                    OnCreateRoom?.Invoke(roomData);
                });
                Debug.Log($"房间创建成功，房间信息 -- {roomData}");
            }
        }

        private void OnError(PlayFabError error)
        {
            Debug.LogError($"Failed to create room: {error.GenerateErrorReport()}");
        }

        public void ApplyJoinRoom(string roomId)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "ApplyJoinRoom",
                FunctionParameter = new { roomId = roomId, playerId = PlayFabData.PlayFabId.Value, },
                GeneratePlayStreamEvent = true,
                Entity = PlayFabData.EntityKey.Value,
            };

            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnApplyJoinRoomSuccess, OnError);
        }

        public void RoomCreatorApplyJoinRoom(RoomData roomData)
        {
            
        }

        private void OnApplyJoinRoomSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.ParseCloudScriptResultToDic();
            if (data.TryGetValue("roomData", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomData>(value.ToString());
                CurrentRoomId = roomData.RoomId;
                _uiManager.SwitchUI<RoomScreenUI>(ui =>
                {
                    _currentRoomData = roomData;
                    OnJoinRoom?.Invoke(roomData);
                });
                Debug.Log($"加入房间成功，房间信息 -- {roomData}");
            }
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
            var data = executeCloudScriptResult.ParseCloudScriptResultToDic();
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
            var data = executeCloudScriptResult.ParseCloudScriptResultToDic();
        }

        public void OnLeaveRoom(RoomData roomData = default)
        {
            if (roomData.RoomId == null)
            {
                OnPlayerJoined?.Invoke(roomData);
                Debug.Log("有人退出了房间");
            }
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
                Debug.Log($"{PlayFabData.PlayFabId.Value}离开房间");
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
            var data = result.ParseCloudScriptResultToDic();
            if (data.TryGetValue("roomData", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomData>(value.ToString());
                _currentRoomData = roomData;
                OnPlayerJoined?.Invoke(roomData);
                _uiManager.CloseUI(UIType.RoomScreen);
                _uiManager.SwitchUI<MainScreenUI>();
                Debug.Log("退出房间成功");
                CurrentRoomId = null;
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
            var data = result.ParseCloudScriptResultToDic();
            if (data.TryGetValue("allRooms", out var value))
            {
                var roomData = JsonConvert.DeserializeObject<RoomData[]>(value.ToString());
                if (_uiManager.IsUIOpen(UIType.RoomList))
                {
                    OnRefreshRoomData?.Invoke(roomData);
                }
                else
                {
                    _uiManager.SwitchUI<RoomListScreenUI>( ui =>
                    {
                        OnRefreshRoomData?.Invoke(roomData);
                    });
                }
                RoomsData = roomData;
                Debug.Log("房间数据更新成功");
            }
        }

        public void GetInvitablePlayers(bool isShowLoading = true)
        {
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "GetPlayersWithStatus",
                GeneratePlayStreamEvent = true,
                FunctionParameter = new { status = "Online" },
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnGetInvitablePlayersSuccess, OnError, isShowLoading);
        }

        private void OnGetInvitablePlayersSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.ParseCloudScriptResultToDic();
           
            if (data.TryGetValue("players", out var value))
            {
                var str = value.ToString();
                if (str == "[]")
                {
                    //_uiManager.ShowTips("当前没有可邀请的玩家");
                    return;
                }
                var players = JsonConvert.DeserializeObject<PlayerReadOnlyData[]>(value.ToString());
                if (_uiManager.IsUIOpen(UIType.RoomScreen))
                {
                    players = players.Where(p => p.PlayerId != PlayFabData.PlayFabId.Value).ToArray();
                    OnRefreshPlayers?.Invoke(players);
                }
            }
            
        }

        public void RefreshRoomData()
        {
            if (CurrentRoomId == null)
            {
                return;
            }
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "CheckPlayerOnline",
                GeneratePlayStreamEvent = true,
                FunctionParameter = new { roomId = CurrentRoomId },
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnRefreshRoomDataSuccess, OnError, false);
        }

        private void OnRefreshRoomDataSuccess(ExecuteCloudScriptResult result)
        {
            var data = result.ParseCloudScriptResultToDic();
            if (data.TryGetValue("roomData", out var value))
            {
                var roomData = JsonUtility.FromJson<RoomData>(value.ToString());
                OnRefreshRoom?.Invoke(roomData);
            }
        }

        public void StartGame()
        {
            if (_currentRoomData.CreatorId != PlayFabData.PlayFabId.Value)
            {
                _uiManager.ShowTips("只有房主才能开始游戏");
                return;
            }
            
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "StartGame",
                GeneratePlayStreamEvent = true,
                FunctionParameter = new { ipAddress = "127.0.0.1", port = 7777, roomId = CurrentRoomId },
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnStartGameSuccess, OnError, false);
        }

        private void OnStartGameSuccess(ExecuteCloudScriptResult result)
        {
            Debug.Log("开始游戏成功");
            var data = result.ParseCloudScriptResultToDic();
        }

        public IEnumerable<RoomData> GetFilteredRooms(string inputText)
        {
            if (string.IsNullOrWhiteSpace(inputText))
            {
                return RoomsData;
            }
            return RoomsData.Where(room => FilterByIdOrName(room.RoomId, room.RoomCustomInfo.RoomName, inputText));
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

        public void OnStartGame(StartGameMessage message)
        {
            // TODO: 根据房间性质，开启一个云服务器或者本地服务器进行游戏
            _currentMainGameInfo = message.mainGameInfo;
            for (int i = 0; i < _currentMainGameInfo.playersInfo.Length; i++)
            {
                var playerInfo = _currentMainGameInfo.playersInfo[i];
                if (playerInfo.playerId == PlayFabData.PlayFabId.Value)
                {
                    _currentGamePlayerInfo = playerInfo;
                    break;
                }
            }
            var operation = ResourceManager.Instance.LoadSceneAsync(message.mainGameInfo.mapType);
            operation.Completed += (op) =>
            {
                _playerDataManager.InitRoomPlayer(_currentRoomData);
                PlayFabData.ConnectionAddress.Value = message.mainGameInfo.ipAddress;
                PlayFabData.ConnectionPort.Value = message.mainGameInfo.port;
            };
        }

        public void TryChangePlayerGameInfo(PlayerGameDuty duty = PlayerGameDuty.None, PlayerGameStatus status = PlayerGameStatus.None)
        {
            var dutyEnum = Enum.Parse<PlayerGameDuty>(_currentGamePlayerInfo.playerDuty);
            if (dutyEnum == duty)
            {
                _uiManager.ShowTips("已经是该职位了");
                return;
            }
            if (duty == PlayerGameDuty.None && status == PlayerGameStatus.None)
            {
                _uiManager.ShowTips("请改变职位或状态的至少一个");
                return;
            }
            _currentGamePlayerInfo.playerDuty = duty == PlayerGameDuty.None ? _currentGamePlayerInfo.playerDuty : duty.ToString();
            _currentGamePlayerInfo.playerStatus = status == PlayerGameStatus.None ? _currentGamePlayerInfo.playerStatus : status.ToString();
            
            var request = new ExecuteEntityCloudScriptRequest
            {
                FunctionName = "SetPlayerGameInfo",
                GeneratePlayStreamEvent = true,
                FunctionParameter = new { playerId = PlayFabData.PlayFabId.Value, gameInfo = JsonUtility.ToJson(_currentGamePlayerInfo), gameId = _currentMainGameInfo.gameId },
                Entity = PlayFabData.EntityKey.Value,
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, OnChangePlayerDutySuccess, OnError);
        }

        private void OnChangePlayerDutySuccess(ExecuteCloudScriptResult result)
        {
            var data = result.ParseCloudScriptResultToDic();
            // if (data.TryGetValue("gameInfo", out var value))
            // {
            //     var gameInfo = JsonUtility.FromJson<GamePlayerInfo>(value.ToString());
            //     _currentGamePlayerInfo = gameInfo;
            //     _uiManager.ShowTips("职位或状态修改成功");
            // }
        }

        public void OnChangeGameInfo(ChangeGameInfoMessage changeGameInfoMessage)
        {
            if (_currentGamePlayerInfo.playerId == changeGameInfoMessage.gamePlayerInfo.playerId)
            {
                _currentGamePlayerInfo = changeGameInfoMessage.gamePlayerInfo;
                _uiManager.ShowTips("玩家信息更新成功");
            }
            for (int i = 0; i < _currentMainGameInfo.playersInfo.Length; i++)
            {
                if (changeGameInfoMessage.gamePlayerInfo.playerId == _currentMainGameInfo.playersInfo[i].playerId)
                {
                    _currentMainGameInfo.playersInfo[i] = changeGameInfoMessage.gamePlayerInfo;
                    OnPlayerInfoChanged?.Invoke(_currentGamePlayerInfo.playerId, _currentMainGameInfo.playersInfo[i]);
                    _uiManager.ShowTips("玩家信息更新成功");
                    break;
                }
            }
        }
    }
}