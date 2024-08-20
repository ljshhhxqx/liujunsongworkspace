using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using System.Threading.Tasks;
using System.IO;
using Cysharp.Threading.Tasks;
using HybridCLR;

public class HotUpdateCheckManager : Singleton<HotUpdateCheckManager>
{
    private const string HOT_UPDATE_DLL_NAME = "HotUpdate.dll";
    private const string HOT_UPDATE_DLL_BYTES_NAME = "HotUpdate.dll.bytes";

    public async UniTask CheckAndUpdateContent()
    {
        Debug.Log("Checking for updates...");

        // 初始化Addressable
        var initOperation = Addressables.InitializeAsync();
        await initOperation.Task.AsUniTask();

        // 检查更新
        var catalogUpdate = Addressables.CheckForCatalogUpdates(false);
        await catalogUpdate.Task.AsUniTask();

        if (catalogUpdate.Result.Count > 0)
        {
            Debug.Log("Catalog updates found. Updating...");

            // 更新目录
            var updateCatalogs = Addressables.UpdateCatalogs(catalogUpdate.Result);
            await updateCatalogs.Task.AsUniTask();

            // 获取需要更新的内容列表
            var checkForUpdates = Addressables.CheckForCatalogUpdates(true);
            await checkForUpdates.Task.AsUniTask();

            var updateKeys = checkForUpdates.Result;

            if (updateKeys.Count > 0)
            {
                // 下载更新
                var downloadSize = Addressables.GetDownloadSizeAsync(updateKeys);
                await downloadSize.Task.AsUniTask();

                if (downloadSize.Result > 0)
                {
                    Debug.Log($"Downloading {downloadSize.Result} bytes...");
                    var download = Addressables.DownloadDependenciesAsync(updateKeys);
                    
                    // 显示下载进度
                    while (!download.IsDone)
                    {
                        var progress = download.PercentComplete;
                        Debug.Log($"Download progress: {progress * 100}%");
                        await UniTask.Yield();
                    }

                    if (download.Status == AsyncOperationStatus.Succeeded)
                    {
                        Debug.Log("Update completed successfully.");
                        await LoadAndApplyHotUpdateDLL();
                    }
                    else
                    {
                        Debug.LogError("Update failed.");
                    }

                    Addressables.Release(download);
                }
                else
                {
                    Debug.Log("No updates to download.");
                }
            }
            else
            {
                Debug.Log("No content updates required.");
            }
        }
        else
        {
            Debug.Log("No catalog updates found.");
        }
    }

    private async UniTask LoadAndApplyHotUpdateDLL()
    {
        // 从Addressables加载热更新DLL
        var loadDllOperation = Addressables.LoadAssetAsync<TextAsset>(HOT_UPDATE_DLL_BYTES_NAME);
        await loadDllOperation.Task.AsUniTask();

        if (loadDllOperation.Status == AsyncOperationStatus.Succeeded)
        {
            var dllBytes = loadDllOperation.Result.bytes;

            // 将DLL保存到StreamingAssets
            var dllPath = Path.Combine(Application.streamingAssetsPath, HOT_UPDATE_DLL_BYTES_NAME);
            await File.WriteAllBytesAsync(dllPath, dllBytes);

            // 使用HybridCLR加载DLL
            LoadHotUpdateAssembly(dllPath);

            Debug.Log("Hot update DLL loaded and applied successfully.");
        }
        else
        {
            Debug.LogError("Failed to load hot update DLL from Addressables.");
        }

        Addressables.Release(loadDllOperation);
    }

    private void LoadHotUpdateAssembly(string dllPath)
    {
        var dllBytes = File.ReadAllBytes(dllPath);
        var hotUpdateAssembly = System.Reflection.Assembly.Load(dllBytes);
        
        // 使用HybridCLR加载补充元数据
        //LoadMetadataForAOTAssemblies();

        // 这里可以添加调用热更新DLL中的方法的代码
        // 例如：hotUpdateAssembly.GetType("HotUpdateNamespace.HotUpdateClass").GetMethod("HotUpdateMethod").Invoke(null, null);
    }

    // private unsafe void LoadMetadataForAOTAssemblies()
    // {
    //     // 这里应该包含所有需要补充元数据的AOT程序集
    //     List<string> aotMetaAssemblies = new List<string>()
    //     {
    //         "mscorlib.dll",
    //         "System.dll",
    //         "System.Core.dll",
    //         // 添加其他需要的AOT程序集
    //     };
    //
    //     foreach (var aotDllName in aotMetaAssemblies)
    //     {
    //         // 加载补充元数据
    //         byte[] dllBytes = File.ReadAllBytes(Path.Combine(Application.streamingAssetsPath, aotDllName + ".bytes"));
    //         fixed (byte* ptr = dllBytes)
    //         {
    //             // 加载补充元数据
    //             int err = HybridCLR.RuntimeApi.LoadMetadataForAOTAssembly(ptr, dllBytes.Length);
    //             Debug.Log($"LoadMetadataForAOTAssembly:{aotDllName}. ret:{err}");
    //         }
    //     }
    // }
}
