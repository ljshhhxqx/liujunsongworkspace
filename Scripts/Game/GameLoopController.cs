using System;
using System.Threading;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using Data;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.InGame;
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
    public class GameLoopController : NetworkMonoController
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
        private GameDataConfig _gameDataConfig;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private PlayerInGameManager _playerInGameManager;
        private GameInfo _gameInfo;
        private MessageCenter _messageCenter;
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
                        _itemsSpawnerManager.EndRound();
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

        // [Command]
        // private void CmdSetIsEndRound(bool value)
        // {
        //     _isEndRound = value;
        // }
        //
        // [Command]
        // private void CmdSetIsEndGame(bool value)
        // {
        //     _isEndGame = value;
        // }

        private void OnCurrentRoundChanged(int oldValue, int newValue)
        {
            Debug.Log($"Current Round Changed from {oldValue} to {newValue}");
        }

        [Inject]
        private void Init(MessageCenter messageCenter, GameEventManager gameEventManager, IObjectResolver objectResolver, IConfigProvider configProvider,
            MirrorNetworkMessageHandler messageHandler)
        {
            _gameEventManager = gameEventManager;
            _messageCenter = messageCenter;
            _messageHandler = messageHandler;
            _gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            _buffManager = FindObjectOfType<BuffManager>();
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
            Debug.Log($"`{message.RemainingTime}` seconds remaining");
            GameLoopDataModel.GameRemainingTime.Value = message.RemainingTime;
        }
        
        private void OnGameWarmupMessage(GameWarmupMessage message)
        {
            Debug.Log($"Warmup Timer: {message.TimeLeft} seconds remaining");
            GameLoopDataModel.WarmupRemainingTime.Value = message.TimeLeft;
        }
        
        private void OnGameStartMessage(GameStartMessage message)
        {
            Debug.Log($"Game Start! Scene: {message.GameInfo.SceneName} | Mode: {_gameInfo.GameMode} | Time: {_gameInfo.GameTime} | Score: {_gameInfo.GameScore}");
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
                _warmupTime = _gameDataConfig.GameConfigData.WarmupTime;
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

            // 2. ??????????
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
        
        private bool IsEndGameWithCountDown(GameMode gameMode, float remainingTime = 0, int targetScore = 0)
        {
            return gameMode switch
            {
                GameMode.Time => remainingTime <= 0,
                GameMode.Score => false,//_playerInGameManager.IsPlayerGetTargetScore(targetScore),
                _ => throw new Exception("Invalid game mode")
            };
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

        private async UniTask StartMainGameTimerAsync(CancellationToken token)
        {
            var remainingTime = _mainGameTime;

            while (!IsEndGameWithCountDown(_gameInfo.GameMode, remainingTime, _gameInfo.GameScore) && !token.IsCancellationRequested)
            {
                Debug.Log($"Main Game Timer: {remainingTime} seconds remaining");

                // 开始新的回合
                IsEndRound = false;
                Debug.Log($"Starting Round {_currentRound}");
        
                // 执行回合循环
                var subCycle = new SubCycle(30, IsEndRoundFunc, RoundStartAsync);
                await subCycle.StartAsync(token);
        
                // 回合结束，增加回合数
                _currentRound++;
                Debug.Log($"Round {_currentRound - 1} completed");

                // 更新游戏时间
                await UniTask.Delay(100, cancellationToken: token);
                if (_gameInfo.GameMode == GameMode.Time)
                {
                    remainingTime-=0.1f;
                }
                else if (_gameInfo.GameMode == GameMode.Score)
                {
                    remainingTime+=0.1f;
                }

                _messageHandler.SendToAllClients(new MirrorCountdownMessage(remainingTime));
            }
            _cts?.Cancel(); 
        
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
            private readonly Func<UniTask> _randomEventHandler;

            public SubCycle(int maxTime, Func<bool> endCondition, Func<UniTask> randomEventHandler = null)
            {
                _subCycleTime = maxTime;
                _endCondition = endCondition;
                _randomEventHandler = randomEventHandler;
                _isEventHandled = false;
            }

            public async UniTask<bool> StartAsync(CancellationToken token)
            {
                Debug.Log($"Starting SubCycle with {_subCycleTime} seconds");

                float elapsedTime = 0;

                while (elapsedTime < _subCycleTime && !_endCondition() && !token.IsCancellationRequested)
                {
                    if (_randomEventHandler != null && !_isEventHandled)
                    {
                        Debug.Log("Random event triggered.");
                        await _randomEventHandler();
                        _isEventHandled = true;
                    }

                    await UniTask.Delay(100, cancellationToken: token);
                    elapsedTime+=0.1f;

                    //Debug.Log($"SubCycle Timer: {_subCycleTime - elapsedTime} seconds remaining");
                }

                Debug.Log("SubCycle Timer ended or end condition met. SubCycle Ended.");
                return true;
            }
        }

    }
}
