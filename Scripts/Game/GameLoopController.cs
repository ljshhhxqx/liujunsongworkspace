using System;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Data.NetworkMes;
using AOTScripts.Tool;
using AOTScripts.Tool.Message;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Panel;
using HotUpdate.Scripts.Weather;
using Mirror;
using PlayFab;
using PlayFab.CloudScriptModels;
using Tool.Message;
using UnityEngine;
using VContainer;
using ExecuteCloudScriptResult = PlayFab.CloudScriptModels.ExecuteCloudScriptResult;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Game
{
    public class GameLoopController : NetworkBehaviour
    {
        [SyncVar(hook = nameof(OnCurrentRoundChanged))]
        private int _currentRound = 1;
        [SyncVar]
        private bool _isEndGame;
        [SyncVar(hook = nameof(OnIsEndRoundChanged))] 
        private bool _isEndRound;
        [SyncVar]
        private float _mainGameTime;
        [SyncVar]
        private float _warmupTime;
        private float _noUnionTime;
        private float _roundInterval;
        private CancellationTokenSource _cts;
        private GameEventManager _gameEventManager;
        private GameSyncManager _gameSyncManager;
        private JsonDataConfig _jsonDataConfig;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private UIManager _uiManager;
        private MapConfig _mapConfig;
        private GameInfo _gameInfo;
        private MapElementData _mapElementData;
        private MessageCenter _messageCenter;
        private MirrorNetworkMessageHandler _messageHandler;
        private IPlayFabClientCloudScriptCaller _playFabClientCloudScriptCaller;
        private InteractSystem _interactSystem;
        private BuffManager _buffManager;
        private GameAudioManager _gameAudioManager;
        private WeatherManager _weatherManager;
        private bool _serverHandler;
        private bool _clientHandler;
        

        [SyncVar(hook = nameof(OnEndGameChanged))] 
        public bool isEndGameSync;
        
        public void OnEndGameChanged(bool oldValue, bool newValue)
        {
            IsEndGame = newValue;
        }

        public bool IsEndGame
        {
            get => _isEndGame;
            set
            {
                Debug.Log("Game Start!");
                _isEndGame = value;
                _gameSyncManager.isGameStart = value;
                _weatherManager.StartWeatherLoop(!value);
                if (value)
                {
                    Debug.Log("Game Over!");
                    _gameEventManager.Publish(new PlayerListenMessageEvent());
                    _cts?.Cancel();
                }

            }
        }
        
        public bool IsEndRound
        {
            get => _isEndRound;
            set
            {
                if (_serverHandler)
                {
                    if (IsEndGame) return;
                    _isEndRound = value;
                    if (value)
                    {
                        Debug.Log("End Round!");
                    }
                }
            }
        }

        private void OnIsEndRoundChanged(bool oldValue, bool newValue)
        {
            if (newValue && isServer)
            {
            }
        }

        private void OnCurrentRoundChanged(int oldValue, int newValue)
        {
            Debug.Log($"Current Round Changed from {oldValue} to {newValue}");
        }

        [Inject]
        private void Init(MessageCenter messageCenter, GameEventManager gameEventManager, IObjectResolver objectResolver, IConfigProvider configProvider,
            MirrorNetworkMessageHandler messageHandler, IPlayFabClientCloudScriptCaller playFabClientCloudScriptCaller, UIManager uiManager)
        {
            _playFabClientCloudScriptCaller = playFabClientCloudScriptCaller;
            _interactSystem = objectResolver.Resolve<InteractSystem>();
            _uiManager = uiManager;
            _gameEventManager = gameEventManager;
            _messageCenter = messageCenter;
            _messageHandler = messageHandler;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            _itemsSpawnerManager = objectResolver.Resolve<ItemsSpawnerManager>();
            _gameAudioManager = objectResolver.Resolve<GameAudioManager>();
            _weatherManager = objectResolver.Resolve<WeatherManager>();
            _gameSyncManager = objectResolver.Resolve<GameSyncManager>();
            _roundInterval = _jsonDataConfig.GameConfig.roundInterval;
            _mapConfig = configProvider.GetConfig<MapConfig>();
            _mapElementData = configProvider.GetConfig<JsonDataConfig>().CollectData.mapElementData;
            Debug.Log($"GameLoopController Init");
            RegisterMessage();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _clientHandler = true;
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            _serverHandler = true;
        }

        private void RegisterMessage()
        {
            _messageCenter.Register<GameStartMessage>(OnGameStartMessage);
            _messageCenter.Register<GameWarmupMessage>(OnGameWarmupMessage);
            _messageCenter.Register<CountdownMessage>(OnCountdownMessage);
        }
        
        private void OnCountdownMessage(CountdownMessage message)
        {
            //Debug.Log($"`{message.RemainingTime}` seconds remaining");
            GameLoopDataModel.GameRemainingTime.Value = message.RemainingTime;
        }
        
        private void OnGameWarmupMessage(GameWarmupMessage message)
        {
            //Debug.Log($"Warmup Timer: {message.TimeLeft} seconds remaining");
            GameLoopDataModel.WarmupRemainingTime.Value = message.TimeLeft;
        }
        
        private void OnGameStartMessage(GameStartMessage message)
        {
            //Debug.Log($"Game Start! Scene: {message.mapType} | Mode: {_gameInfo.GameMode} | Time: {_gameInfo.GameTime} | Score: {_gameInfo.GameScore}");
        }

        private void OnGameReady(GameReadyEvent gameReadyEvent)
        {
            _gameInfo = gameReadyEvent.GameInfo;

            if (_serverHandler)
            {
                _cts = new CancellationTokenSource();
                PlayerInGameManager.Instance.SpawnAllBases(gameReadyEvent.GameInfo.SceneName);
                isEndGameSync = false;
                IsEndGame = false;
                
                _warmupTime = _jsonDataConfig.GameConfig.warmupTime;
                _noUnionTime = _jsonDataConfig.GameConfig.noUnionTime;
                _mainGameTime = 6f;
                Debug.Log($"OnGameReady called {_gameInfo.GameMode}- {_gameInfo.GameTime} seconds");
                StartGameLoop(_cts).Forget();
            }
        }

        private async UniTask StartGameLoop(CancellationTokenSource cts)
        {
            // 1. 开始热身
            Debug.Log("Game Warmup Started");
            await StartWarmupAsync(cts.Token);
            Debug.Log("Warmup Complete. Game Start!");

            Debug.Log("Main game timer starts now!");
            _messageHandler.SendToAllClients(new MirrorGameStartMessage((int)_gameInfo.SceneName, (int)_gameInfo.GameMode, _gameInfo.GameScore, _gameInfo.GameTime, _gameInfo.PlayerCount));
            _gameEventManager.Publish(new GameStartEvent());
            await StartMainGameTimerAsync(cts.Token);

            Debug.Log("Main game over. Exiting...");
        }

        private async UniTask StartWarmupAsync(CancellationToken token)
        {
            Debug.Log("Warmup Started");
            var remainingTime = _warmupTime;

            while (remainingTime > 0 && !token.IsCancellationRequested)
            {
                Debug.Log($"Warmup Timer: {remainingTime} seconds remaining");
                await UniTask.Delay(1000, cancellationToken: token);
                remainingTime--;
               
                _messageHandler.SendToAllClients(new MirrorGameWarmupMessage(remainingTime)); 
            }
        }
        
        private bool IsEndGameWithCountDown(float remainingTime = 0)
        {
            return remainingTime <= 0;
        }
        
        private bool IsEndRoundFunc()
        {
            var isEndRound = IsEndRound && !IsEndGame;
            if (isEndRound)
            {
                Debug.Log("End Round!");
            }
            return IsEndRound && !IsEndGame; //_itemsSpawnerManager.SpawnedItems.Count == 0;
        }

        private async UniTask RoundStartAsync()
        {
            Debug.Log($"Round Start -- {_currentRound.ToString()}!");
            await _itemsSpawnerManager.SpawnItemsAndChest();
            Debug.Log("Random event handled.");
            //todo: 给所有玩家添加TimedBuff
            //todo: 给分数较低的玩家增加DeBuff
            _gameEventManager.Publish(new AddBuffToAllPlayerEvent(_currentRound));
            _gameEventManager.Publish(new AddDeBuffToLowScorePlayerEvent(_currentRound));

            switch (_gameInfo.SceneName)
            {
                case MapType.Christmas:
                    var wellPosition = MapBoundDefiner.Instance.GetRandomPoint(v =>
                    {
                        return v.y < 0.5f && v.y > -0.5f;
                    });
                    var rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
                    _gameEventManager.Publish(new StartGameWellEvent(wellPosition, rotation, ++_interactSystem.currentWellId));
                    break;
                case MapType.Rocket:
                    var rocketPosition = _mapElementData.rocketPositions.RandomSelect();
                    var duration = _mapElementData.durationRange.GetRandomValue();
                    _gameEventManager.Publish(new StartGameTrainEvent(rocketPosition.vectors[0], rocketPosition.vectors[^1], rocketPosition.rotation, duration, ++_interactSystem.currentTrainId));
                    break;
                case MapType.WestWild:
                    rocketPosition = _mapElementData.trainPositions.RandomSelect();
                    duration = _mapElementData.durationRange.GetRandomValue();
                    _gameEventManager.Publish(new StartGameTrainEvent(rocketPosition.vectors[0], rocketPosition.vectors[^1], rocketPosition.rotation,duration, ++_interactSystem.currentTrainId));
                    break;
            }
            
        }

        private async UniTask RoundEndAsync()
        {
            Debug.Log($"Round End -- {_currentRound.ToString()}!");
            await _itemsSpawnerManager.EndRound();
            Debug.Log("Round ended.");
        }

        private async UniTask StartMainGameTimerAsync(CancellationToken token)
        {
            _gameSyncManager.isGameStart = true;
            _gameEventManager.Publish(new AllPlayerGetSpeedEvent());
            var remainingTime = _mainGameTime;
            var endGameFlag = false;
            var interval = Time.fixedDeltaTime;

            // 定义一个Action，用于更新剩余时间
            Action updateRemainingTime = () =>
            {
                try
                {
                    if (_gameInfo.GameMode == GameMode.Time)
                    {
                        remainingTime -= interval;
                    }
                    else if (_gameInfo.GameMode == GameMode.Score)
                    {
                        remainingTime += interval;
                    }

                    if (!_gameSyncManager.isRandomUnionStart)
                    {
                        _noUnionTime -= interval;
                        if (_noUnionTime <= 0)
                        {
                            _gameSyncManager.isRandomUnionStart = true;
                        }
                    }
                    _messageHandler.SendToAllClients(new MirrorCountdownMessage(remainingTime));

                    // 检查是否满足结束游戏的条件
                    if (IsEndGameWithCountDown(remainingTime))
                    {
                        isEndGameSync = true;
                        IsEndGame = true;
                        _gameEventManager.Publish(new PlayerListenMessageEvent());
                        SaveGameResult();
                        endGameFlag = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error updating remaining time: {ex.Message}");
                    isEndGameSync = true;
                    IsEndGame = true;
                    _gameEventManager.Publish(new PlayerListenMessageEvent());
                    SaveGameResult();
                    endGameFlag = true;
                }
            };

            while (!endGameFlag && !token.IsCancellationRequested)
            {
                Debug.Log($"Main Game Timer: {remainingTime:F1} seconds remaining");

                // 开始新的回合
                IsEndRound = false;
                Debug.Log($"Starting Round {_currentRound}");

                // 执行回合循环，并传递更新倒计时的Action
                var subCycle = new SubCycle((int)_roundInterval, interval, IsEndRoundFunc, RoundStartAsync, updateRemainingTime, RoundEndAsync);
                await subCycle.StartAsync(token);

                // 回合结束，增加回合数
                _currentRound++;
                IsEndRound = true;
                await _itemsSpawnerManager.EndRound();
                Debug.Log($"Round {_currentRound - 1} completed");

                // 检查是否在分数模式下有玩家达到目标分数
                if (_gameInfo.GameMode == GameMode.Score)
                {
                    if (PlayerInGameManager.Instance.IsPlayerGetTargetScore(_gameInfo.GameScore))
                    {
                        isEndGameSync = true;
                        IsEndGame = true;
                        _gameEventManager.Publish(new PlayerListenMessageEvent());
                        SaveGameResult();
                        endGameFlag = true;
                        await UniTask.Yield();
                        return;
                    }
                }
                await UniTask.Yield();
            }

            if (IsEndGame)
            {
                return;
            }
            _cts?.Cancel();
            isEndGameSync = true;
            IsEndGame = true;
            _gameEventManager.Publish(new PlayerListenMessageEvent());
            SaveGameResult();
            Debug.Log("Main game timer ended.");
        }

        private void SaveGameResult()
        {
            if (!_serverHandler)
            {
                return;
            }
            _gameSyncManager.isGameOver = true;
            Debug.Log("Save game result");
            var playerPropertySyncSystem = _gameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            if (playerPropertySyncSystem == null)
            {
                Debug.LogError("PlayerPropertySyncSystem not found.");
                return;
            }

            var playerScores = playerPropertySyncSystem.GetSortedPlayerProperties(PropertyTypeEnum.Score, false);
            var data = new GameResultData();
            data.playersResultData = new PlayerGameResultData[playerScores.Count];
            var index = 0;
            foreach (var kvp in playerScores)
            {
                Debug.Log($"{kvp.Key} - {kvp.Value}");
                var playerData = PlayerInGameManager.Instance.GetPlayer(kvp.Key);
                var rank = index + 1;
                data.playersResultData[index] = new PlayerGameResultData
                {
                    playerName = playerData.player.Nickname,
                    score = (int)kvp.Value,
                    rank = rank,
                    isWinner = rank == 1 || rank == 2
                };
                index++;
            }
            var json = JsonUtility.ToJson(data);
            var request = new ExecuteEntityCloudScriptRequest();
            request.FunctionName = "SaveGameResult";
            request.FunctionParameter = new
            {
                gameId = PlayFabData.CurrentGameId.Value,
                gameResult = json
            };
            _playFabClientCloudScriptCaller.ExecuteCloudScript(request, r => OnSaveGameResult(r, json), OnError);
        }

        private void OnSaveGameResult(ExecuteCloudScriptResult result, string json)
        {
            var dic = result.ParseCloudScriptResultToDic();
            RpcStartGame(json);
        }
        
        [ClientRpc]
        private void RpcStartGame(string json)
        {
            var data = BoxingFreeSerializer.JsonDeserialize<GameResultData>(json);
            GameLoopDataModel.GameResult.SetValueAndForceNotify(data);
        }

        private void OnError(PlayFabError error)
        {
            Debug.LogError($"Failed to Save Game Result: {error.GenerateErrorReport()}");
        }

        private void OnDestroy()
        {
            GameLoopDataModel.Clear();
            _cts?.Cancel();
        }
        
        private class SubCycle
        {
            private readonly int _subCycleTime;
            private bool _isEventHandled;
            private readonly Func<bool> _endCondition;
            private readonly Func<UniTask> _roundStartHandler;
            private readonly Func<UniTask> _roundEndAction;
            private readonly Action _updateCountdownAction; // 新增的Action
            private readonly float _interval;

            public SubCycle(int maxTime, float interval, Func<bool> endCondition, Func<UniTask> roundStartHandler = null, 
                Action updateCountdownAction = null, Func<UniTask> roundEndAction = null)
            {
                _subCycleTime = maxTime;
                _interval = interval;
                _endCondition = endCondition;
                _roundStartHandler = roundStartHandler;
                _isEventHandled = false;
                _roundEndAction = roundEndAction;
                _updateCountdownAction = updateCountdownAction;
            }

            public async UniTask<bool> StartAsync(CancellationToken token)
            {
                Debug.Log($"Starting SubCycle with {_subCycleTime} seconds");

                float elapsedTime = 0;
                var milliseconds = (int)(_interval * 1000);

                while (elapsedTime < _subCycleTime && !_endCondition() && !token.IsCancellationRequested)
                {
                    if (_roundStartHandler != null && !_isEventHandled)
                    {
                        Debug.Log("Random event triggered.");
                        await _roundStartHandler();
                        _isEventHandled = true;
                    }

                    // 执行传入的倒计时Action
                    _updateCountdownAction?.Invoke();

                    await UniTask.Delay(milliseconds, cancellationToken: token);
                    elapsedTime += _interval;

                    // 可选的日志输出
                    // Debug.Log($"SubCycle Timer: {_subCycleTime - elapsedTime} seconds remaining");
                }
                // 回合结束，执行传入的回合结束Action
                if (_roundEndAction != null)
                {
                    await _roundEndAction();
                }
                Debug.Log("SubCycle Timer ended or end condition met. SubCycle Ended.");
                return true;
            }
        }

        public void ClearAll()
        {
            var op = ResourceManager.Instance.UnloadCurrentScene();
            op.Completed += _ =>
            {
                _uiManager.ClearAllGameUI();
                _uiManager.UnloadAll();
                UISpriteContainer.Clear(ResourceManager.Instance.CurrentLoadingSceneName);
                GameObjectPoolManger.Instance.ClearAllPool();
                _uiManager.SwitchUI<MainScreenUI>();
                _gameEventManager.Publish(new PlayerListenMessageEvent());
            };
        }
    }
}
