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
        [SyncVar]
        private float _mainGameTime; // 3?????????
        [SyncVar]
        private float _warmupTime; // 10?????????
        [SyncVar]
        private int _currentRound = 1; // ???????
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
        //private WeatherManager _weatherManager;
        
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
            GameLoopDataModel.GameRemainingTime.Value = message.RemainingTime;
        }
        
        private void OnGameWarmupMessage(GameWarmupMessage message)
        {
            GameLoopDataModel.WarmupRemainingTime.Value = message.TimeLeft;
        }
        
        private void OnGameStartMessage(GameStartMessage message)
        {
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
                _warmupTime = _gameDataConfig.GameConfigData.WarmupTime;
                _mainGameTime = _gameInfo.GameMode == GameMode.Time ? _gameInfo.GameTime : 0;
                StartGameLoop(_cts).Forget();
            }
        }

        private async UniTask StartGameLoop(CancellationTokenSource cts)
        {
            // 1. ???????
            Debug.Log("Game Warmup Started");
            await StartWarmupAsync(cts.Token);
            Debug.Log("Warmup Complete. Game Start!");

            // 2. ??????????
            Debug.Log("Main game timer starts now!");
            _messageHandler.SendMessage(new MirrorGameStartMessage(_gameInfo));
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
               
                _messageHandler.SendMessage(new MirrorGameWarmupMessage(remainingTime)); 
            }
        }
        
        private bool IsEndGame(GameMode gameMode, float remainingTime = 0, int targetScore = 0)
        {
            return gameMode switch
            {
                GameMode.Time => remainingTime <= 0,
                GameMode.Score => false,//_playerInGameManager.IsPlayerGetTargetScore(targetScore),
                _ => throw new Exception("Invalid game mode")
            };
        }
        
        private bool IsEndRound()
        {
            return false; //_itemsSpawnerManager.SpawnedItems.Count == 0;
        }

        private async UniTask RoundStartAsync()
        {
            Debug.Log($"Round Start -- {_currentRound.ToString()}!");
            await UniTask.Yield(); // ????????
            Debug.Log("Random event handled.");
        }

        private async UniTask StartMainGameTimerAsync(CancellationToken token)
        {
            var isSubCycleRunning = false;
            var remainingTime = _mainGameTime;

            while (!IsEndGame(_gameInfo.GameMode, remainingTime, _gameInfo.GameScore) && !token.IsCancellationRequested)
            {
                Debug.Log($"Main Game Timer: {remainingTime} seconds remaining");

                // ?????????С??????У???????????С???
                if (!isSubCycleRunning)
                {
                    isSubCycleRunning = true;
                    var subCycle = new SubCycle(30, IsEndRound, RoundStartAsync);
                    _ = subCycle.StartAsync(token).ContinueWith(result => isSubCycleRunning = false);
                    _currentRound++;
                }

                // ??????
                await UniTask.Delay(100, cancellationToken: token);
                if (_gameInfo.GameMode == GameMode.Time)
                {
                    remainingTime-=0.1f;
                }
                else if (_gameInfo.GameMode == GameMode.Score)
                {
                    remainingTime+=0.1f;
                }

                _messageHandler.SendMessage(new MirrorCountdownMessage(remainingTime));
            }
            _cts.Cancel(); 
        
        }

        private void OnDestroy()
        {
            _cts.Cancel();
        }
        
        private class SubCycle
        {
            private readonly int _subCycleTime;
            private readonly Func<bool> _endCondition;
            private readonly Func<UniTask> _randomEventHandler;

            public SubCycle(int maxTime, Func<bool> endCondition, Func<UniTask> randomEventHandler = null)
            {
                // ?????С???????????
                _subCycleTime = maxTime;
                _endCondition = endCondition;
                _randomEventHandler = randomEventHandler;
            }

            public async UniTask<bool> StartAsync(CancellationToken token)
            {
                Debug.Log($"Starting SubCycle with {_subCycleTime} seconds");

                float elapsedTime = 0;

                while (elapsedTime < _subCycleTime && !_endCondition() && !token.IsCancellationRequested)
                {
                    // ??????????????????????????????
                    
                    if (_randomEventHandler != null)
                    {
                        Debug.Log("Random event triggered.");
                        await _randomEventHandler();
                    }

                    await UniTask.Delay(100, cancellationToken: token);
                    elapsedTime+=0.1f;

                    Debug.Log($"SubCycle Timer: {_subCycleTime - elapsedTime} seconds remaining");
                }

                Debug.Log("SubCycle Timer ended or end condition met. SubCycle Ended.");
                return true;
            }
        }

    }
}
