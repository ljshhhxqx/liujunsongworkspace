using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Mirror;
using VContainer;

public class GameLoopController : SingletonNetMono<GameLoopController>
{
    [SyncVar]
    private static int mainGameTime = 180; // 3分钟的倒计时
    [SyncVar]
    private static int warmupTime = 10; // 10秒热身时间
    private CancellationTokenSource cts;

    [Inject]
    private void Init()
    {
        cts = new CancellationTokenSource();
        StartGameLoop(cts.Token).Forget();
    }

    private async UniTaskVoid StartGameLoop(CancellationToken token)
    {
        // 1. 热身阶段
        Debug.Log("Game Warmup Started");
        await UniTask.Delay(warmupTime * 1000, cancellationToken: token);
        Debug.Log("Warmup Complete. Game Start!");

        // 2. 生成物品（模拟）
        Debug.Log("Generating Items...");
        await GenerateItemsAsync(token);

        // 3. 开始总的倒计时
        Debug.Log("Main game timer starts now!");
        await StartMainGameTimerAsync(mainGameTime, token);

        Debug.Log("Main game over. Exiting...");
    }

    private async UniTask GenerateItemsAsync(CancellationToken token)
    {
        // 假设物品生成需要一些时间，异步模拟
        await UniTask.Delay(2000, cancellationToken: token); // 生成物品的延迟
        Debug.Log("All Items Generated.");
    }

    private async UniTask StartMainGameTimerAsync(int totalTime, CancellationToken token)
    {
        int remainingTime = totalTime;

        while (remainingTime > 0 && !token.IsCancellationRequested)
        {
            // 启动或继续小循环
            await StartSubCycleAsync(token);

            Debug.Log($"Main Game Timer: {remainingTime} seconds remaining");

            // 这里模拟了每分钟（或其他时间段）减少的行为
            await UniTask.Delay(1000, cancellationToken: token);
            remainingTime--;

        }
        cts.Cancel();
    }

    private async UniTask StartSubCycleAsync(CancellationToken token)
    {
        int subCycleTime = Random.Range(10, 30); // 随着循环进行，时间可能会变化
        Debug.Log($"Starting SubCycle with {subCycleTime} seconds");

        // 模拟物品被拾取完或时间耗尽
        bool allItemsCollected = false;
        while (subCycleTime > 0 && !allItemsCollected && !token.IsCancellationRequested)
        {
            // 在这里检测是否所有物品都被拾取
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
