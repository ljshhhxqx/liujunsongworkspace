using System;
using System.Threading;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using Data;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.Weather;
using Mirror;
using Network.NetworkMes;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;
using VContainer;

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
        private float _mainGameTime;
        private float _warmupTime;
        private CancellationTokenSource _cts;
        private GameEventManager _gameEventManager;
        private JsonDataConfig _jsonDataConfig;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private PlayerInGameManager _playerInGameManager;
        private GameInfo _gameInfo;
        private MessageCenter _messageCenter;
        private MapBoundDefiner _mapBoundDefiner;
        private MirrorNetworkMessageHandler _messageHandler;
        
        private BuffManager _buffManager;
        private NetworkAudioManager _networkAudioManager;
        private WeatherManager _weatherManager;

        public bool IsEndGame
        {
            get => _isEndGame;
            set
            {
                if (isServer)
                {
                    _isEndGame = value;
                    _weatherManager.StartWeatherLoop(!value);
                    if (value)
                    {
                        Debug.Log("Game Over!");
                        _cts?.Cancel();
                    }
                }
                else
                {
                    Debug.LogError("Client cannot set IsEndGame");
                }
            }
        }
        
        public bool IsEndRound
        {
            get => _isEndRound;
            set
            {
                if (isServer)
                {
                    if (IsEndGame) return;
                    _isEndRound = value;
                    if (value)
                    {
                        Debug.Log("End Round!");
                    }
                }
                else
                {
                    Debug.LogError("Client cannot set IsEndRound");
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
            MirrorNetworkMessageHandler messageHandler, MapBoundDefiner mapBoundDefiner)
        {
            _gameEventManager = gameEventManager;
            _messageCenter = messageCenter;
            _messageHandler = messageHandler;
            _mapBoundDefiner = mapBoundDefiner;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            _networkAudioManager = FindObjectOfType<NetworkAudioManager>();
            _playerInGameManager = FindObjectOfType<PlayerInGameManager>();
            _weatherManager = FindObjectOfType<WeatherManager>();
            RegisterMessage();
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
            //Debug.Log($"Game Start! Scene: {message.GameInfo.SceneName} | Mode: {_gameInfo.GameMode} | Time: {_gameInfo.GameTime} | Score: {_gameInfo.GameScore}");
            GameLoopDataModel.GameSceneName.Value = message.GameInfo.SceneName;
            GameLoopDataModel.GameLoopData.Value = new GameLoopData
            {
                GameMode = _gameInfo.GameMode,
                TargetScore = _gameInfo.GameTime,
                TimeLimit = _gameInfo.GameScore,
            };
        }

        private void OnGameReady(GameReadyEvent gameReadyEvent)
        {
            _gameInfo = gameReadyEvent.GameInfo;

            if (isServer)
            {
                _cts = new CancellationTokenSource();
                IsEndGame = false;
                _warmupTime = _jsonDataConfig.GameConfig.warmupTime;
                _mainGameTime = _gameInfo.GameMode == GameMode.Time ? _gameInfo.GameTime : 0;
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
            _messageHandler.SendToAllClients(new MirrorGameStartMessage(_gameInfo));
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
        }

        private async UniTask RoundEndAsync()
        {
            Debug.Log($"Round End -- {_currentRound.ToString()}!");
            await _itemsSpawnerManager.EndRound();
            Debug.Log("Round ended.");
        }

        private async UniTask StartMainGameTimerAsync(CancellationToken token)
        {
            _playerInGameManager.isGameStarted = true;
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

                    _messageHandler.SendToAllClients(new MirrorCountdownMessage(remainingTime));

                    // 检查是否满足结束游戏的条件
                    if (IsEndGameWithCountDown(remainingTime))
                    {
                        IsEndGame = true;
                        endGameFlag = true;
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError($"Error updating remaining time: {ex.Message}");
                    IsEndGame = true;
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
                var subCycle = new SubCycle(30, interval, IsEndRoundFunc, RoundStartAsync, updateRemainingTime, RoundEndAsync);
                await subCycle.StartAsync(token);

                // 回合结束，增加回合数
                _currentRound++;
                IsEndRound = true;
                await _itemsSpawnerManager.EndRound();
                Debug.Log($"Round {_currentRound - 1} completed");

                // 检查是否在分数模式下有玩家达到目标分数
                if (_gameInfo.GameMode == GameMode.Score)
                {
                    if (_playerInGameManager.IsPlayerGetTargetScore(_gameInfo.GameScore))
                    {
                        IsEndGame = true;
                        endGameFlag = true;
                        break;
                    }
                }
            }

            _cts?.Cancel();
            Debug.Log("Main game timer ended.");
        }

        private void OnDestroy()
        {
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

    }
}
