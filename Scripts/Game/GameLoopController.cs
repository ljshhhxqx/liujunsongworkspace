using System;
using System.Threading;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using Data;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
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
        private float _mainGameTime; // 3分钟的倒计时
        [SyncVar]
        private float _warmupTime; // 10秒热身时间
        [SyncVar]
        private int _currentRound = 1; // 当前轮数
        private CancellationTokenSource _cts;
        private GameEventManager _gameEventManager;
        private GameDataConfig _gameDataConfig;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private PlayerInGameManager _playerInGameManager;
        private GameInfo _gameInfo;
        
        private BuffManager _buffManager;
        private NetworkAudioManager _networkAudioManager;
        //private WeatherManager _weatherManager;
        
        [Inject]
        private void Init(MessageCenter messageCenter, GameEventManager gameEventManager, IObjectResolver objectResolver, IConfigProvider configProvider)
        {
            _gameEventManager = gameEventManager;
            _gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            _buffManager = FindObjectOfType<BuffManager>();
            _networkAudioManager = FindObjectOfType<NetworkAudioManager>();
            _playerInGameManager = FindObjectOfType<PlayerInGameManager>();
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
            // 1. 热身阶段
            Debug.Log("Game Warmup Started");
            await StartWarmupAsync(cts.Token);
            Debug.Log("Warmup Complete. Game Start!");

            // 2. 开始总的倒计时
            Debug.Log("Main game timer starts now!");
            NetworkServer.SendToAll(new GameStartMessage(_gameInfo));
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
               
                NetworkServer.SendToAll(new GameWarmupMessage(remainingTime)); 
            }
        }
        
        private bool IsEndGame(GameMode gameMode, float remainingTime = 0, int targetScore = 0)
        {
            switch (gameMode)
            {
                case GameMode.Time:
                    return remainingTime <= 0;
                case GameMode.Score:
                    return _playerInGameManager.IsPlayerGetTargetScore(targetScore);
                default:
                    throw new Exception("Invalid game mode");
            }
        }
        
        private bool IsEndRound()
        {
            return _itemsSpawnerManager.SpawnedItems.Count == 0;
        }

        private async UniTask RoundStartAsync()
        {
            Debug.Log($"Round Start -- {_currentRound.ToString()}!");
            await UniTask.Yield(); // 模拟处理时间
            Debug.Log("Random event handled.");
        }

        private async UniTask StartMainGameTimerAsync(CancellationToken token)
        {
            var isSubCycleRunning = false;
            var remainingTime = _mainGameTime;

            while (!IsEndGame(_gameInfo.GameMode, remainingTime, _gameInfo.GameScore) && !token.IsCancellationRequested)
            {
                Debug.Log($"Main Game Timer: {remainingTime} seconds remaining");

                // 如果当前没有小循环运行，启动一个新的小循环
                if (!isSubCycleRunning)
                {
                    isSubCycleRunning = true;
                    var subCycle = new SubCycle(30, IsEndRound, RoundStartAsync);
                    _ = subCycle.StartAsync(token).ContinueWith(result => isSubCycleRunning = false);
                    _currentRound++;
                }

                // 每秒倒计时
                await UniTask.Delay(100, cancellationToken: token);
                if (_gameInfo.GameMode == GameMode.Time)
                {
                    remainingTime-=0.1f;
                }
                else if (_gameInfo.GameMode == GameMode.Score)
                {
                    remainingTime+=0.1f;
                }
                NetworkServer.SendToAll(new CountdownMessage(remainingTime));
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
                // 初始化小循环的持续时间
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
                    // 如果有随机事件处理函数，则触发随机事件
                    
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
