using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.HotUpdate
{
    public class InitHotUpdate : MonoBehaviour
    {
        [Button]
        private async void Start()
        {
            Debug.Log("InitHotUpdate Start");
            await HotUpdateCheckManager.Instance.CheckForUpdates();
        }
        
        // private const string baseUrl = "http://192.168.31.156:1140/#/RemoteResCatalogs/catalog_0.1.hash";
        // private string catalogPath = "Library/com.unity.addressables/aa/Windows/catalog.json";
        // private IEnumerator LoadFile(string folderPath, string fileName)
        // {
        //     string url = baseUrl;
        //     using (UnityWebRequest www = UnityWebRequest.Get(url))
        //     {
        //         www.timeout = 250;
        //         yield return www.SendWebRequest();
        //
        //         if (www.result != UnityWebRequest.Result.Success)
        //         {
        //             Debug.LogError("Failed to load file: " + www.error);
        //         }
        //         else
        //         {
        //             // 使用下载的数据
        //             string text = www.downloadHandler.text;
        //             Debug.Log("File contents: " + text);
        //         }
        //     }
        // }
        //
        // private void ReadCatalog()
        // {
        //     // 检查文件是否存在
        //     if (File.Exists(catalogPath))
        //     {
        //         try
        //         {
        //             // 读取文件内容
        //             string jsonContent = File.ReadAllText(catalogPath);
        //             Debug.Log("Catalog JSON Content:");
        //             Debug.Log(jsonContent); // 输出文件内容
        //         }
        //         catch (IOException ex)
        //         {
        //             Debug.LogError("Error reading the catalog file: " + ex.Message);
        //         }
        //     }
        //     else
        //     {
        //         Debug.LogError("Catalog file does not exist at path: " + catalogPath);
        //     }
        // }
        //
        // private static string GetRemoteLoadPath()
        // {
        //     foreach (var locator in Addressables.ResourceLocators)
        //     {
        //         var locations = locator.Locate("RemoteLoadPath", typeof(string), out var resourceLocations);
        //         if (resourceLocations != null && resourceLocations.Any())
        //         {
        //             if (resourceLocations.First() is ResourceLocationBase remoteLoadLocation)
        //             {
        //                 Debug.Log($"RemoteLoadPath found at: {remoteLoadLocation.PrimaryKey}");
        //                 return remoteLoadLocation.PrimaryKey;
        //             }
        //         }
        //     }
        //     Debug.LogWarning("RemoteLoadPath not found in Addressables configuration.");
        //     return string.Empty;
        // }
        // private static string InternalIdTransformFunc(IResourceLocation location)
        // {
        //     string originalId = location.InternalId;
        //     string newId = originalId;
        //
        //     //Debug.Log($"InternalIdTransformFunc: No transformation for {originalId}");  
        //     if (originalId.EndsWith(".bundle") || originalId.EndsWith(".hash"))
        //     {
        //         newId = originalId + ".txt";
        //         Debug.Log($"InternalIdTransformFunc: Transformed {originalId} to {newId}");
        //     }
        //     else
        //     {
        //         Debug.Log($"InternalIdTransformFunc: No transformation for {originalId}");
        //     }
        //
        //     return newId;
        // }
        // private async void ListAllKeys()
        // {
        //     // HashSet<object> allKeys = new HashSet<object>();
        //     //
        //     // foreach (IResourceLocator locator in Addressables.ResourceLocators)
        //     // {
        //     //     foreach (object key in locator.Keys)
        //     //     {
        //     //         if (!allKeys.Contains(key))
        //     //         {
        //     //             allKeys.Add(key);
        //     //             Debug.Log($"Addressable Key: {key}");
        //     //         }
        //     //     }
        //     // }
        //     
        //     var sizeOperation = Addressables.GetDownloadSizeAsync("Remote");
        //     var size = await sizeOperation.Task.AsUniTask();
        //
        //     if (size > 0)
        //     {
        //         Debug.Log($"需要下载的内容大小: {size} bytes");
        //         
        //         // 开始下载
        //     }
        //     else
        //     {
        //         Debug.Log("无需下载任何内容");
        //     }
        //     Addressables.Release(sizeOperation);
        // }
        //
        // [Button]
        // private void Test()
        // {
        //     StartCoroutine(LoadFile("",""));
        // }
    }
}
