using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.UI.UIs.Popup;
using HybridCLR;
using TMPro;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.AddressableAssets.ResourceLocators;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.UI;

namespace HotUpdate
{
    public class HotUpdateCheckManager : Singleton<HotUpdateCheckManager>
    {
        private TextMeshProUGUI _text;
        private Slider _slider;
        private CommonTipsPopup _commonTipsPopup;
        
        public async UniTask CheckForUpdates()
        {
            _text ??= GameObject.Find("InfoText").GetComponent<TextMeshProUGUI>();
            _slider??= GameObject.Find("Progress").GetComponent<Slider>();
            _commonTipsPopup ??= GameObject.Find("TipsPopup").GetComponent<CommonTipsPopup>();
            _commonTipsPopup.gameObject.SetActive(false);
            _slider.gameObject.SetActive(false);
            _slider.value = 0;
            _text.text = "正在检查更新...";
            // 初始化 Addressables
            var initOperation = Addressables.InitializeAsync();
            await initOperation.Task.AsUniTask();
            Debug.Log("Addressables initialized.");
            // 检查目录更新
            var catalogUpdateOperation = Addressables.CheckForCatalogUpdates(false);
            var catalogsToUpdate = await catalogUpdateOperation.Task.AsUniTask();
            
            if (catalogsToUpdate.Count > 0)
            {
                Debug.Log("发现目录更新");
                // 使用自定义方法更新目录
                _commonTipsPopup.ShowTips("发现有资源需要更新，是否更新？", () =>
                {
                    ConfirmedUpdate(catalogsToUpdate);
                }, () =>
                {
                    Debug.Log("取消更新");
                    Application.Quit();
                });
            }
            else
            {
                _text.text = "正在加载资源...";
                Debug.Log("无需更新目录或资源");
            }
            Debug.Log("start load dll");
            await LoadAndApplyHotUpdateDLL();
            await LoadLoginSceneAsync();
        }

        private async void ConfirmedUpdate(IEnumerable<string> catalogsToUpdate)
        {
            var updateCatalogOperation = Addressables.UpdateCatalogs(catalogsToUpdate);
            var result = await updateCatalogOperation.Task.AsUniTask();
                
            if (result.Count > 0)
            {
                // 获取需要更新的资源列表
                Debug.Log("已更新本地目录，开始更新资源");
                        
                await UpdateContent(result);
                Debug.Log("更新完成");
            }
        }

        private async UniTask UpdateContent(List<IResourceLocator> locationsToUpdate)
        {
            // 获取需要更新的内容大小
            var sizeOperation = Addressables.GetDownloadSizeAsync(locationsToUpdate);
            var size = await sizeOperation.Task.AsUniTask();

            if (size > 0)
            {
                Debug.Log($"需要下载的内容大小: {size} bytes");

                // 开始下载
                var downloadOperation = Addressables.DownloadDependenciesAsync(locationsToUpdate, Addressables.MergeMode.Union, false);
                await DownloadWithProgress(downloadOperation, size);

                Debug.Log("更新完成");
            }
            else
            {
                Debug.Log("无需下载任何内容");
            }
            Addressables.Release(sizeOperation);
        }


        private async UniTask DownloadWithProgress(AsyncOperationHandle downloadOperation, float totalSize)
        {
            while (!downloadOperation.IsDone)
            {
                var progress = downloadOperation.PercentComplete;
                var currentSize = totalSize * progress;
                _text.text = $"正在下载... {currentSize / 1024}KB/{totalSize / 1024}KB";
                _slider.value = progress;
                Debug.Log($"下载进度: {progress:P}");
                await UniTask.Yield();
            }
            Addressables.Release(downloadOperation);
        }
        
        private async UniTask LoadAndApplyHotUpdateDLL()
        {
#if !UNITY_EDITOR
            // 从Addressables加载热更新DLL
            var loadDllOperation = Addressables.LoadAssetAsync<TextAsset>("HotUpdateDll");
            await loadDllOperation.Task.AsUniTask();

            if (loadDllOperation.Status == AsyncOperationStatus.Succeeded)
            {
                var dllBytes = loadDllOperation.Result.bytes;

                // 使用HybridCLR加载DLL
                LoadHotUpdateAssembly(dllBytes);

                Debug.Log("Hot update DLL loaded and applied successfully.");
            }
            else
            {
                Debug.LogError("Failed to load hot update DLL from Addressables.");
            }

            Addressables.Release(loadDllOperation);
#else
            // 编辑器下直接获取热更新DLL
            var assemblys = AppDomain.CurrentDomain.GetAssemblies();
            var hotUpdateAss = assemblys.First(a => a.GetName().Name == "HotUpdate");
            InvokeMethod(hotUpdateAss);
#endif
        }

        private void LoadHotUpdateAssembly(byte[] dllBytes)
        {
            var hotUpdateAssembly = Assembly.Load(dllBytes);
            Debug.Log($"Hot update assembly loaded: {hotUpdateAssembly.FullName}");

#if !UNITY_EDITOR
            // 使用HybridCLR加载补充元数据
            LoadMetadataForAOTAssemblies();
#endif
            // 调用热更新DLL的方法
            
            //InvokeMethod(hotUpdateAssembly);
        }

        private static void InvokeMethod(Assembly hotUpdateAssembly)
        {
            var type = hotUpdateAssembly.GetType("Hello");
            Debug.Log($"Hot update type loaded: {type.FullName}");
            type.GetMethod("SayHello")?.Invoke(null, null);
            type.GetMethod("TestGetComponent")?.Invoke(null, null);
            // 获取 TestType 方法的 MethodInfo
            var testTypeMethod = type.GetMethod("TestType");

            // 创建一个特定类型的方法，例如 TestType<string>
            if (testTypeMethod != null)
            {
                var testTypeStringMethod = testTypeMethod.MakeGenericMethod(typeof(string));
                var parameters = new object[] { "John",};

                // 调用方法
                testTypeStringMethod.Invoke(null, parameters);
            }

            // // 如果你想调用 TestType<int>
            if (testTypeMethod != null)
            {
                var testTypeIntMethod = testTypeMethod.MakeGenericMethod(typeof(int));
                var parameters = new object[] { 15, };
                testTypeIntMethod.Invoke(null, parameters);
            }
        }

        private void LoadMetadataForAOTAssemblies()
        {
            // 这里应该包含所有需要补充元数据的AOT程序集
            var aotDllList = new List<string>
            {
                "System.Core.dll", // 如果使用了Linq，需要这个
                "mscorlib.dll", // 如果使用了Json，需要这个
                "UnityEngine.CoreModule.dll", // 如果使用了热更代码，需要这个
            };

            foreach (var aotDllName in aotDllList)
            {
                var dllBytes = File.ReadAllBytes($"{Application.streamingAssetsPath}/{aotDllName}.bytes");
                var err = RuntimeApi.LoadMetadataForAOTAssembly(dllBytes, HomologousImageMode.SuperSet);
                Debug.Log($"LoadMetadataForAOTAssembly:{aotDllName}. ret:{err}");
            }
        }

        private async UniTask LoadLoginSceneAsync()
        {
            var operation = Addressables.LoadSceneAsync("LoginScene");
            while (!operation.IsDone)
            {
                _text.text = $"正在加载主场景... {operation.PercentComplete:P}";
                _slider.value = operation.PercentComplete;
                await UniTask.Yield();
            }
            Addressables.Release(operation);

            Debug.Log("Login scene loaded.");
        }

        // private static string InternalIdTransformFunc(IResourceLocation location)
        // {
        //     string originalId = location.InternalId;
        //     string newId = originalId;
        //
        //     Debug.Log($"InternalIdTransformFunc: No transformation for {originalId}");  
        //     if (originalId.EndsWith(".bundle") || originalId.EndsWith(".hash"))
        //     {
        //         newId = originalId + ".txt";
        //         //Debug.Log($"InternalIdTransformFunc: Transformed {originalId} to {newId}");
        //     }
        //
        //     else if (originalId.EndsWith(".json.txt"))
        //     {
        //         newId = originalId.Replace(".json.txt", ".json");
        //     }
        //     // else
        //     // {
        //     //     Debug.Log($"InternalIdTransformFunc: No transformation for {originalId}");
        //     // }
        //
        //     return newId;
        // }

        

        private void RemapFiles()
        {
            // var resourceLocators = Addressables.ResourceLocators;
            // foreach (var locator in resourceLocators)
            // {
            //     foreach (var key in locator.Keys)
            //     {
            //         if (locator.Locate(key, typeof(object), out var locations))
            //         {
            //             foreach (var location in locations)
            //             {
            //                 string originalPath = location.InternalId;
            //                 string remappedPath = RemapFilePath(originalPath);
            //                 if (originalPath != remappedPath)
            //                 {
            //                     _remappedFiles[originalPath] = remappedPath;
            //                 }
            //             }
            //         }
            //     }
            // }
        }

        // 重写 Addressables 的加载方法
        public static AsyncOperationHandle<T> LoadAssetAsync<T>(object key)
        {
            // var operation = Addressables.LoadAssetAsync<T>(key);
            // operation.Completed += handle => {
            //     if (handle.Status == AsyncOperationStatus.Failed)
            //     {
            //         // 如果加载失败，尝试使用重映射的路径
            //         string originalPath = handle.OperationException.Message;
            //         if (Instance._remappedFiles.TryGetValue(originalPath, out string remappedPath))
            //         {
            //             Debug.Log($"尝试加载重映射路径: {remappedPath}");
            //             Addressables.LoadAssetAsync<T>(remappedPath);
            //         }
            //     }
            // };
            // return operation;
            return default;
        }

    }
    
    public class CustomCertificateHandler : CertificateHandler
    {
        protected override bool ValidateCertificate(byte[] certificateData)
        {
            return true;
        }
    }
}