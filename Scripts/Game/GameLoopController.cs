using System.Threading;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using Mirror;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;
using VContainer;

public class GameLoopController : NetworkMonoController
{
    [SyncVar]
    private int mainGameTime = 180; // 3���ӵĵ���ʱ
    [SyncVar]
    private int warmupTime = 10; // 10������ʱ��
    private CancellationTokenSource cts;
    private GameEventManager _gameEventManager;
    private MessageCenter _messageCenter;
    private ItemsSpawner _itemsSpawner;
    private string _gameSceneName;
        
    [Inject]
    private void Init(MessageCenter messageCenter, GameEventManager gameEventManager, IObjectResolver objectResolver)
    {
        _messageCenter = messageCenter;
        _gameEventManager = gameEventManager;
        _itemsSpawner = GetComponent<ItemsSpawner>();
        _gameEventManager.Subscribe<GameReadyEvent>(OnGameReady);
        objectResolver.Inject(_itemsSpawner);
    }

    private void OnGameReady(GameReadyEvent gameReadyEvent)
    {
        _gameSceneName = gameReadyEvent.SceneName;
        StartGameLoop().Forget();
    }

    private async UniTaskVoid StartGameLoop()
    {
        // 1. ����׶�
        Debug.Log("Game Warmup Started");
        await UniTask.Delay(warmupTime * 1000, cancellationToken: cts.Token);
        Debug.Log("Warmup Complete. Game Start!");

        // 2. ��ʼ�ܵĵ���ʱ
        Debug.Log("Main game timer starts now!");
        await StartMainGameTimerAsync(mainGameTime, cts.Token);

        Debug.Log("Main game over. Exiting...");
    }

    private async UniTask GenerateItemsAsync(CancellationToken token)
    {
        // ������Ʒ������ҪһЩʱ�䣬�첽ģ��
        await UniTask.Delay(2000, cancellationToken: token); // ������Ʒ���ӳ�
        Debug.Log("All Items Generated.");
    }

    private async UniTask StartMainGameTimerAsync(int totalTime, CancellationToken token)
    {
        int remainingTime = totalTime;

        while (remainingTime > 0 && !token.IsCancellationRequested)
        {
            // ���������Сѭ��
            await StartSubCycleAsync(token);

            Debug.Log($"Main Game Timer: {remainingTime} seconds remaining");

            // ����ģ����ÿ���ӣ�������ʱ��Σ����ٵ���Ϊ
            await UniTask.Delay(1000, cancellationToken: token);
            remainingTime--;

        }
        cts.Cancel();
    }

    private async UniTask StartSubCycleAsync(CancellationToken token)
    {
        int subCycleTime = Random.Range(10, 30); // ����ѭ�����У�ʱ����ܻ�仯
        Debug.Log($"Starting SubCycle with {subCycleTime} seconds");

        // ģ����Ʒ��ʰȡ���ʱ��ľ�
        bool allItemsCollected = false;
        while (subCycleTime > 0 && !allItemsCollected && !token.IsCancellationRequested)
        {
            // ���������Ƿ�������Ʒ����ʰȡ
            // allItemsCollected = CheckIfAllItemsCollected();

            await UniTask.Delay(1000, cancellationToken: token);
            subCycleTime--;

            Debug.Log($"SubCycle Timer: {subCycleTime} seconds remaining");
        }

        Debug.Log("SubCycle Ended");
    }

    private void OnDestroy()
    {
        cts.Cancel();
    }
}
