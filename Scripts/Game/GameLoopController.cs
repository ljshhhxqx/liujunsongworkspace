using System;
using System.Threading;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Weather;
using Mirror;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Game
{
    public class GameLoopController : NetworkMonoController
    {
        [SyncVar]
        private int mainGameTime = 180 * 1000; // 3分钟的倒计时
        [SyncVar]
        private int warmupTime = 10 * 1000; // 10秒热身时间
        [SyncVar]
        private int currentRound = 1; // 当前轮数
        private CancellationTokenSource cts = new CancellationTokenSource();
        private GameEventManager _gameEventManager;
        private GameDataConfig _gameDataConfig;
        private MessageCenter _messageCenter;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private string _gameSceneName;
        
        private BuffManager _buffManager;
        private AudioManager _audioManager;
        private WeatherManager _weatherManager;
        
        [Inject]
        private void Init(MessageCenter messageCenter, GameEventManager gameEventManager, IObjectResolver objectResolver, IConfigProvider configProvider)
        {
            _messageCenter = messageCenter;
            _gameEventManager = gameEventManager;
            _gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
            _messageCenter.Register<CollectObjectsEmptyMessage>(OnCollectObjectsEmpty);
            _itemsSpawnerManager = GetComponent<ItemsSpawnerManager>();
            _buffManager = GetComponent<BuffManager>();
            _audioManager = GetComponent<AudioManager>();
            _weatherManager = GetComponent<WeatherManager>();
            objectResolver.Inject(_buffManager);
            objectResolver.Inject(_audioManager);
            objectResolver.Inject(_itemsSpawnerManager);
            objectResolver.Inject(_weatherManager);
        }

        private void OnCollectObjectsEmpty(CollectObjectsEmptyMessage collectObjectsEmptyMessage)
        {
        
        }

        private void OnGameReady(GameReadyEvent gameReadyEvent)
        {
            _gameSceneName = gameReadyEvent.SceneName;
            _weatherManager.StartWeatherAndDayNightCycle();
            StartGameLoop().Forget();
        }

        private async UniTask StartGameLoop()
        {
            // 1. 热身阶段
            Debug.Log("Game Warmup Started");
            await StartWarmupAsync(cts.Token);
            Debug.Log("Warmup Complete. Game Start!");

            // 2. 开始总的倒计时
            Debug.Log("Main game timer starts now!");
            _messageCenter.Post(new GameStartMessage(_gameSceneName));
            await StartMainGameTimerAsync(mainGameTime, cts.Token);

            Debug.Log("Main game over. Exiting...");
        }

        private async UniTask StartWarmupAsync(CancellationToken token)
        {
            Debug.Log("Warmup Started");
            int remainingTime = warmupTime;

            while (remainingTime > 0 && !token.IsCancellationRequested)
            {
                Debug.Log($"Warmup Timer: {remainingTime} seconds remaining");
                await UniTask.Delay(1000, cancellationToken: token);
                remainingTime--;
                _messageCenter.Post(new GameWarmupMessage(remainingTime));
            }
        }

        private async UniTask StartMainGameTimerAsync(int totalTime, CancellationToken token)
        {
            int remainingTime = totalTime;
            bool isSubCycleRunning = false;

            // 定义结束条件和随机事件处理逻辑
            Func<bool> endCondition = () => Random.value < 0.2f; // 示例条件：20% 概率结束小循环
            Func<UniTask> randomEventHandler = async () =>
            {
                Debug.Log("Handling random event...");
                await UniTask.Delay(500); // 模拟处理时间
                Debug.Log("Random event handled.");
            };

            while (remainingTime > 0 && !token.IsCancellationRequested)
            {
                Debug.Log($"Main Game Timer: {remainingTime} seconds remaining");

                // 如果当前没有小循环运行，启动一个新的小循环
                if (!isSubCycleRunning)
                {
                    isSubCycleRunning = true;
                    var subCycle = new SubCycle(10, 30, endCondition, randomEventHandler);
                    _ = subCycle.StartAsync(token).ContinueWith(result => isSubCycleRunning = false);
                }

                // 每秒倒计时
                await UniTask.Delay(1000, cancellationToken: token);
                remainingTime--;
            }
            cts.Cancel(); 
        
        }

        private void OnDestroy()
        {
            _weatherManager.StopWeatherAndDayNightCycle();
            cts.Cancel();
        }
        
        private class SubCycle
        {
            private int subCycleTime;
            private Func<bool> endCondition;
            private Func<UniTask> randomEventHandler;

            public SubCycle(int minTime, int maxTime, Func<bool> endCondition, Func<UniTask> randomEventHandler = null)
            {
                // 初始化小循环的持续时间
                subCycleTime = UnityEngine.Random.Range(minTime, maxTime);
                this.endCondition = endCondition;
                this.randomEventHandler = randomEventHandler;
            }

            public async UniTask<bool> StartAsync(CancellationToken token)
            {
                Debug.Log($"Starting SubCycle with {subCycleTime} seconds");

                int elapsedTime = 0;

                while (elapsedTime < subCycleTime && !endCondition() && !token.IsCancellationRequested)
                {
                    // 如果有随机事件处理函数，随机决定是否触发事件
                    if (randomEventHandler != null && UnityEngine.Random.value < 0.1f) // 10% 概率触发
                    {
                        Debug.Log("Random event triggered.");
                        await randomEventHandler();
                    }

                    await UniTask.Delay(1000, cancellationToken: token);
                    elapsedTime++;

                    Debug.Log($"SubCycle Timer: {subCycleTime - elapsedTime} seconds remaining");
                }

                if (endCondition())
                {
                    Debug.Log("End condition met. SubCycle Ended.");
                    return true;
                }
                else
                {
                    Debug.Log("SubCycle timer ended without meeting end condition.");
                    return false;
                }
            }
        }

    }
}
