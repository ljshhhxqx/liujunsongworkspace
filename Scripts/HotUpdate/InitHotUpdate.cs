using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Networking;
using UnityEngine.ResourceManagement.ResourceLocations;
using UnityEngine.ResourceManagement.ResourceProviders;

namespace HotUpdate
{
    public class InitHotUpdate : MonoBehaviour
    {
        private const string baseUrl = "http://192.168.31.156:1140/#/RemoteResCatalogs/catalog_0.1.hash";
        private string catalogPath = "Library/com.unity.addressables/aa/Windows/catalog.json";
        private IEnumerator LoadFile(string folderPath, string fileName)
        {
            string url = baseUrl;
            using (UnityWebRequest www = UnityWebRequest.Get(url))
            {
                www.timeout = 250;
                yield return www.SendWebRequest();

                if (www.result != UnityWebRequest.Result.Success)
                {
                    Debug.LogError("Failed to load file: " + www.error);
                }
                else
                {
                    // 使用下载的数据
                    string text = www.downloadHandler.text;
                    Debug.Log("File contents: " + text);
                }
            }
        }
        
        private void ReadCatalog()
        {
            // 检查文件是否存在
            if (File.Exists(catalogPath))
            {
                try
                {
                    // 读取文件内容
                    string jsonContent = File.ReadAllText(catalogPath);
                    Debug.Log("Catalog JSON Content:");
                    Debug.Log(jsonContent); // 输出文件内容
                }
                catch (IOException ex)
                {
                    Debug.LogError("Error reading the catalog file: " + ex.Message);
                }
            }
            else
            {
                Debug.LogError("Catalog file does not exist at path: " + catalogPath);
            }
        }
        
        private static string GetRemoteLoadPath()
        {
            foreach (var locator in Addressables.ResourceLocators)
            {
                var locations = locator.Locate("RemoteLoadPath", typeof(string), out var resourceLocations);
                if (resourceLocations != null && resourceLocations.Any())
                {
                    if (resourceLocations.First() is ResourceLocationBase remoteLoadLocation)
                    {
                        Debug.Log($"RemoteLoadPath found at: {remoteLoadLocation.PrimaryKey}");
                        return remoteLoadLocation.PrimaryKey;
                    }
                }
            }
            Debug.LogWarning("RemoteLoadPath not found in Addressables configuration.");
            return string.Empty;
        }
        private static string InternalIdTransformFunc(IResourceLocation location)
        {
            string originalId = location.InternalId;
            string newId = originalId;

            //Debug.Log($"InternalIdTransformFunc: No transformation for {originalId}");  
            if (originalId.EndsWith(".bundle") || originalId.EndsWith(".hash"))
            {
                newId = originalId + ".txt";
                Debug.Log($"InternalIdTransformFunc: Transformed {originalId} to {newId}");
            }
            else
            {
                Debug.Log($"InternalIdTransformFunc: No transformation for {originalId}");
            }

            return newId;
        }
        [Button]
        private async void Start()
        {
            Debug.Log("InitHotUpdate Start");
            //ListAllKeys();
            //ReadCatalog();
            //StartCoroutine(LoadFile("",""));
            
            var initOp = Addressables.InitializeAsync();
             //Addressables.ResourceManager.InternalIdTransformFunc = InternalIdTransformFunc;
             await initOp.Task.AsUniTask();

            // Debug.Log("Addressables initialized successfully");
            // var loadDllOperation = await Addressables.LoadAssetAsync<TextAsset>("HotUpdateDll");
            // Debug.Log(loadDllOperation.text);
            // var sliverCoin = await Addressables.LoadAssetAsync<GameObject>("Assets/Res/Map/Main/Collect/SilverCoin.prefab");
            // Instantiate(sliverCoin, Vector3.zero, Quaternion.identity);

            // var getDownloadSizeOp = Addressables.GetDownloadSizeAsync("Remote");
            // var size = await getDownloadSizeOp.Task.AsUniTask();
            // if (size > 0)
            // {
            //     Debug.Log($"需要下载的内容大小: {size} bytes");
            //     Addressables.Release(getDownloadSizeOp);
            //     var downloadOperation = Addressables.DownloadDependenciesAsync("Remote");
            //     await downloadOperation.Task.AsUniTask();
            //     while (!downloadOperation.IsDone)
            //     {
            //         var progress = downloadOperation.PercentComplete;
            //         Debug.Log($"下载进度: {progress:P}");
            //         await UniTask.Yield();
            //     }
            //     foreach (var item in (List<IAssetBundleResource>)downloadOperation.Result)
            //     {
            //         var ab = item as VirtualAssetBundle;
            //         if (ab == null)
            //         {
            //             continue;
            //         }
            //         Debug.Log("ab name " + ab.Name);
            //         foreach (var assetName in ab.GetAssetBundle().GetAllAssetNames())
            //         {
            //             Debug.Log("asset name " + assetName);
            //         }
            //     }
            //     Addressables.Release(downloadOperation);
            // }
            // foreach (var locator in Addressables.ResourceLocators)
            // {
            //     Debug.Log($"Locator: {locator.LocatorId}");
            //     foreach (var key in locator.Keys)
            //     {
            //         Debug.Log($"{locator.LocatorId} Key: {key}");
            //     }
            // }
            // var check = Addressables.CheckForCatalogUpdates(false);
            // await check.Task.AsUniTask();
            // foreach (var locator in check.Result)
            // {
            //     Debug.Log($"Locator: {locator}");
            // }
            //
            // var updateCatalogs = await Addressables.UpdateCatalogs(check.Result);
            // Debug.Log("Catalogs updated successfully");
            // foreach (var locator in updateCatalogs)
            // {
            //     Debug.Log(locator.Keys);
            // }
            // var handle = Addressables.LoadContentCatalogAsync(Addressables.RuntimePath + "/catalog.json");
            // var loc = await handle.Task.AsUniTask();
            // Debug.Log(loc.Keys);
            // var catalogs = loc.Keys.Select(x => x.ToString());
            // var updateCatalogs = Addressables.UpdateCatalogs(catalogs);
            // var result = await updateCatalogs.Task.AsUniTask();
            // if (updateCatalogs.Status == AsyncOperationStatus.Succeeded)
            // {
            //     Debug.Log("Catalogs updated successfully");
            //     foreach (var locator in result)
            //     {
            //         Debug.Log(locator.Keys);
            //     }
            // }
            await HotUpdateCheckManager.Instance.CheckForUpdates();
            // var catalog =  Addressables.LoadContentCatalogAsync(catalogPath);
            // await catalog.Task;
            // if (catalog.Status == AsyncOperationStatus.Succeeded)
            // {
            //     Debug.Log($"Catalog loaded successfully at path: {catalogPath}");
            //     Debug.Log(catalog.Result.ToString());
            // }
        }
        private async void ListAllKeys()
        {
            // HashSet<object> allKeys = new HashSet<object>();
            //
            // foreach (IResourceLocator locator in Addressables.ResourceLocators)
            // {
            //     foreach (object key in locator.Keys)
            //     {
            //         if (!allKeys.Contains(key))
            //         {
            //             allKeys.Add(key);
            //             Debug.Log($"Addressable Key: {key}");
            //         }
            //     }
            // }
            
            var sizeOperation = Addressables.GetDownloadSizeAsync("Remote");
            var size = await sizeOperation.Task.AsUniTask();

            if (size > 0)
            {
                Debug.Log($"需要下载的内容大小: {size} bytes");
                
                // 开始下载
            }
            else
            {
                Debug.Log("无需下载任何内容");
            }
            Addressables.Release(sizeOperation);
        }
        
        [Button]
        private void Test()
        {
            StartCoroutine(LoadFile("",""));
        }
    }
}
