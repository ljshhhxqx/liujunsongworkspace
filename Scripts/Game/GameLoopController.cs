using UnityEngine;
using Cysharp.Threading.Tasks;
using System.Threading;
using Mirror;
using VContainer;

public class GameLoopController : SingletonNetMono<GameLoopController>
{
    [SyncVar]
    private static int mainGameTime = 180; // 3���ӵĵ���ʱ
    [SyncVar]
    private static int warmupTime = 10; // 10������ʱ��
    private CancellationTokenSource cts;

    [Inject]
    private void Init()
    {
        cts = new CancellationTokenSource();
        StartGameLoop(cts.Token).Forget();
    }

    private async UniTaskVoid StartGameLoop(CancellationToken token)
    {
        // 1. ����׶�
        Debug.Log("Game Warmup Started");
        await UniTask.Delay(warmupTime * 1000, cancellationToken: token);
        Debug.Log("Warmup Complete. Game Start!");

        // 2. ������Ʒ��ģ�⣩
        Debug.Log("Generating Items...");
        await GenerateItemsAsync(token);

        // 3. ��ʼ�ܵĵ���ʱ
        Debug.Log("Main game timer starts now!");
        await StartMainGameTimerAsync(mainGameTime, token);

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
