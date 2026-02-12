using System;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Data.NetworkMes;
using AOTScripts.Tool;
using AOTScripts.Tool.Message;
using AOTScripts.Tool.ObjectPool;
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
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool;
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
        private PlayerInGameManager _playerInGameManager;
        private UIManager _uiManager;
        private MapConfig _mapConfig;
        private GameInfo _gameInfo;
        private MapElementData _mapElementData;
        private MessageCenter _messageCenter;
        private MirrorNetworkMessageHandler _messageHandler;
        private IPlayFabClientCloudScriptCaller _playFabClientCloudScriptCaller;
        private InteractSystem _interactSystem;
        private BuffManager _buffManager;
        private WeatherManager _weatherManager;
        private bool _serverHandler;
        private bool _clientHandler;
        private NetworkEndHandler _endHandler;
        private NetworkGameObjectPoolManager _networkGameObjectPoolManager;

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
        private void Init(MessageCenter messageCenter, GameEventManager gameEventManager, IConfigProvider configProvider,
            MirrorNetworkMessageHandler messageHandler, IPlayFabClientCloudScriptCaller playFabClientCloudScriptCaller, 
            UIManager uiManager, NetworkEndHandler endHandler, WeatherManager weatherManager, GameSyncManager gameSyncManager,
            NetworkGameObjectPoolManager networkGameObjectPoolManager, PlayerInGameManager playerInGameManager,
            InteractSystem interactSystem, ItemsSpawnerManager itemsSpawnerManager)
        {
            _playFabClientCloudScriptCaller = playFabClientCloudScriptCaller;
            _endHandler = endHandler;
            _playerInGameManager = playerInGameManager;
            _uiManager = uiManager;
            _weatherManager = weatherManager;
            _networkGameObjectPoolManager = networkGameObjectPoolManager;
            _gameSyncManager = gameSyncManager;
            _interactSystem = interactSystem;
            _itemsSpawnerManager = itemsSpawnerManager;
            _gameEventManager = gameEventManager;
            _messageCenter = messageCenter;
            _messageHandler = messageHandler;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _roundInterval = _jsonDataConfig.GameConfig.roundInterval;
            _mapConfig = configProvider.GetConfig<MapConfig>();
            _mapElementData = configProvider.GetConfig<JsonDataConfig>().CollectData.mapElementData;
            Debug.Log($"GameLoopController Init");
            _endHandler.OnCleanup += Cleanup;
            _endHandler.OnDisconnected += Disconnected;
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            RegisterMessage();
        }

        private void Disconnected()
        {
            UnloadCurrentScene().Forget();
        }

        private void Cleanup()
        {
            ClearAndReleaseAsync().Forget();
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
                _playerInGameManager.SpawnAllBases(gameReadyEvent.GameInfo.SceneName, transform);
                isEndGameSync = false;
                IsEndGame = false;
                
                _warmupTime = _jsonDataConfig.GameConfig.warmupTime;
                _noUnionTime = _jsonDataConfig.GameConfig.noUnionTime;
                _mainGameTime = _gameInfo.GameTime;
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
                await UniTask.Delay(1000, DelayType.Realtime, cancellationToken: token);
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
            var interval = GameSyncManager.TickSeconds;
            var endGameFlag = false;

            // ⭐ 创建独立的 CancellationTokenSource 用于倒计时
            var timerCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            // ⭐ 启动独立的倒计时协程
            var timerTask = RunGlobalTimerAsync(
                () => remainingTime,
                (newTime) => remainingTime = newTime,
                () => endGameFlag,
                interval,
                timerCts.Token
            ).SuppressCancellationThrow();

            try
            {
                // 主游戏循环（不再负责倒计时）
                while (!endGameFlag && !token.IsCancellationRequested)
                {
                    Debug.Log($"[GameTimer] Starting Round {_currentRound}, Remaining Time: {remainingTime:F1}s");

                    // 开始新的回合
                    IsEndRound = false;

                    // 执行回合循环（不再传递 updateRemainingTime）
                    var subCycle = new SubCycle(
                        maxTime: (int)_roundInterval,
                        interval: interval,
                        endCondition: IsEndRoundFunc,
                        roundStartHandler: RoundStartAsync,
                        roundEndAction: RoundEndAsync
                    );

                    await subCycle.StartAsync(token);

                    // 回合结束
                    _currentRound++;
                    IsEndRound = true;

                    Debug.Log($"[GameTimer] Round {_currentRound - 1} completed, calling EndRound()");
                    await _itemsSpawnerManager.EndRound();

                    // 检查分数模式下是否有玩家达到目标分数
                    if (_gameInfo.GameMode == GameMode.Score)
                    {
                        if (_playerInGameManager.IsPlayerGetTargetScore(_gameInfo.GameScore))
                        {
                            Debug.Log("[GameTimer] Target score reached!");
                            endGameFlag = true;
                            break;
                        }
                    }

                    await UniTask.Yield();
                }

                Debug.Log($"[GameTimer] Main loop ended. EndGameFlag: {endGameFlag}, Token Cancelled: {token.IsCancellationRequested}");
            }
            finally
            {
                // ⭐ 停止倒计时协程
                timerCts.Cancel();
                timerCts.Dispose();

                // 等待倒计时协程完全结束
                await timerTask;

                // 确保游戏结束逻辑只执行一次
                if (!IsEndGame)
                {
                    isEndGameSync = true;
                    IsEndGame = true;
                    _gameEventManager.Publish(new PlayerListenMessageEvent());
                    SaveGameResult();
                    Debug.Log("[GameTimer] Game ended and results saved.");
                }
            }
        }
        private async UniTask RunGlobalTimerAsync(
            Func<float> getRemainingTime,
            Action<float> setRemainingTime,
            Func<bool> isEndGame,
            float interval,
            CancellationToken token)
        {
            var lastUpdateTime = Time.time;
            var intervalMs = (int)(interval * 1000);

            Debug.Log($"[GlobalTimer] Started with interval {interval}s ({intervalMs}ms)");

            try
            {
                while (!isEndGame() && !token.IsCancellationRequested)
                {
                    // ⭐ 使用 Realtime 确保精度
                    await UniTask.Delay(intervalMs, DelayType.Realtime, cancellationToken: token);

                    // ⭐ 计算实际流逝时间（更精确）
                    var currentTime = Time.time;
                    var actualDelta = currentTime - lastUpdateTime;
                    lastUpdateTime = currentTime;

                    // 更新剩余时间
                    var currentRemaining = getRemainingTime();
                    float newRemaining;

                    if (_gameInfo.GameMode == GameMode.Time)
                    {
                        newRemaining = currentRemaining - actualDelta;
                    }
                    else if (_gameInfo.GameMode == GameMode.Score)
                    {
                        newRemaining = currentRemaining + actualDelta;
                    }
                    else
                    {
                        newRemaining = currentRemaining;
                    }

                    setRemainingTime(newRemaining);

                    // 更新随机联盟计时
                    if (!_gameSyncManager.isRandomUnionStart)
                    {
                        _noUnionTime -= actualDelta;
                        if (_noUnionTime <= 0)
                        {
                            _gameSyncManager.isRandomUnionStart = true;
                            _playerInGameManager.RandomUnion(out var id);

                            if (id != 0)
                            {
                                var command = new NoUnionPlayerAddMoreScoreAndGoldCommand
                                {
                                    Header = GameSyncManager.CreateNetworkCommandHeader(
                                        id, 
                                        CommandType.Property, 
                                        CommandAuthority.Server, 
                                        CommandExecuteType.Immediate
                                    ),
                                };
                                _gameSyncManager.EnqueueServerCommand(command);
                            }
                        }
                    }

                    // 发送倒计时消息给客户端
                    _messageHandler.SendToAllClients(new MirrorCountdownMessage(newRemaining));

                    // 检查是否满足结束条件
                    if (IsEndGameWithCountDown(newRemaining))
                    {
                        Debug.Log($"[GlobalTimer] End condition met at {newRemaining:F1}s");
                        isEndGameSync = true;
                        IsEndGame = true;
                        _gameEventManager.Publish(new PlayerListenMessageEvent());
                        SaveGameResult();
                        break;
                    }

                    // 可选：每秒输出一次日志
                    // if (Mathf.Abs(newRemaining % 1.0f) < interval)
                    // {
                    //     Debug.Log($"[GlobalTimer] Remaining: {newRemaining:F1}s");
                    // }
                }
            }
            catch (OperationCanceledException)
            {
                Debug.Log("[GlobalTimer] Cancelled");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[GlobalTimer] Error: {ex.Message}\n{ex.StackTrace}");
                
                // 发生异常时强制结束游戏
                isEndGameSync = true;
                IsEndGame = true;
                _gameEventManager.Publish(new PlayerListenMessageEvent());
                SaveGameResult();
            }

            Debug.Log("[GlobalTimer] Stopped");
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
                var rank = index + 1;
                var playerName = _playerInGameManager.GetPlayerName(kvp.Key);
                data.playersResultData[index] = new PlayerGameResultData
                {
                    playerName = playerName,
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
            RpcEndGame(json);
            _endHandler.BeginGameEndProcedure();
        }
        
        [ClientRpc]
        private void RpcEndGame(string json)
        {
            var data = BoxingFreeSerializer.JsonDeserialize<GameResultData>(json);
            GameLoopDataModel.GameResult.SetValueAndNotify(data);
        }

        private void OnError(PlayFabError error)
        {
            Debug.LogError($"Failed to Save Game Result: {error.GenerateErrorReport()}");
        }
        
        private class SubCycle
        {
            private readonly int _subCycleTime;
            private bool _isEventHandled;
            private readonly Func<bool> _endCondition;
            private readonly Func<UniTask> _roundStartHandler;
            private readonly Func<UniTask> _roundEndAction;
            private readonly float _interval;

            public SubCycle(
                int maxTime, 
                float interval, 
                Func<bool> endCondition, 
                Func<UniTask> roundStartHandler = null,
                Func<UniTask> roundEndAction = null)
            {
                _subCycleTime = maxTime;
                _interval = interval;
                _endCondition = endCondition;
                _roundStartHandler = roundStartHandler;
                _isEventHandled = false;
                _roundEndAction = roundEndAction;
            }

            public async UniTask<bool> StartAsync(CancellationToken token)
            {
                Debug.Log($"[SubCycle] Starting with duration {_subCycleTime}s");

                float elapsedTime = 0;
                var intervalMs = (int)(_interval * 1000);
                var startTime = Time.time;

                try
                {
                    while (elapsedTime < _subCycleTime && !_endCondition() && !token.IsCancellationRequested)
                    {
                        // 触发回合开始事件（只执行一次）
                        if (_roundStartHandler != null && !_isEventHandled)
                        {
                            Debug.Log("[SubCycle] Triggering round start handler");
                            await _roundStartHandler();
                            _isEventHandled = true;
                        }

                        // ⭐ 使用 Realtime 延迟
                        await UniTask.Delay(intervalMs, DelayType.Realtime, cancellationToken: token);

                        // ⭐ 使用实际流逝时间
                        elapsedTime = Time.time - startTime;

                        // 可选：输出剩余时间
                        // Debug.Log($"[SubCycle] Elapsed: {elapsedTime:F1}s / {_subCycleTime}s");
                    }

                    // 执行回合结束逻辑
                    if (_roundEndAction != null)
                    {
                        Debug.Log("[SubCycle] Executing round end action");
                        await _roundEndAction();
                    }

                    Debug.Log($"[SubCycle] Ended. Elapsed: {elapsedTime:F1}s, EndCondition: {_endCondition()}");
                    return true;
                }
                catch (OperationCanceledException)
                {
                    Debug.Log("[SubCycle] Cancelled");
                    return false;
                }
            }
        }

        public async UniTask ClearAndReleaseAsync()
        {
            PlayFabData.PlayerList.Clear();
            _playerInGameManager.Clear();
            await UniTask.Yield();
            _networkGameObjectPoolManager.ClearAllPools();
            await UniTask.Yield();
        }
        
        public async UniTask UnloadCurrentScene()
        {
            _uiManager.ClearAllGameUI();
            await UniTask.Yield();
            _uiManager.UnloadAll();
            await UniTask.Yield();
            UISpriteContainer.Clear(ResourceManager.Instance.CurrentLoadingSceneName);
            await UniTask.Yield();
            GameObjectPoolManger.Instance.ClearAllPool();
            await UniTask.Yield();
            UIPropertyBinder.ClearAllData();
            await ResourceManager.Instance.UnloadCurrentScene();
            _uiManager.SwitchUI<MainScreenUI>();
            _gameEventManager.Unsubscribe<GameReadyEvent>(OnGameReady);
            GameLoopDataModel.Clear();
            _cts?.Cancel();
            _endHandler.OnCleanup -= Cleanup;
            _endHandler.OnDisconnected -= Disconnected;
            
            // GC.Collect();
            // GC.WaitForPendingFinalizers();
        }
    }
}
