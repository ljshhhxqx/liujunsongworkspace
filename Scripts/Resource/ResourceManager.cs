using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Resource;
using UI.UIBase;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceProviders;
using UnityEngine.SceneManagement;
using VContainer;

[Serializable]
public class ResourceInfo
{
    public int RefCount;
    public object Resource;
}

[Serializable]
public class ResourceDataInfo
{
    public ResourceInfo resourceInfo;
    public ResourceData resourceData;
}

public class ResourceManager : Singleton<ResourceManager>
{
    [Serializable]
    private class ResourceLoadTask
    {
        public ResourceData ResourceData;
        public int Priority;
        public Type ResourceType;

        public ResourceLoadTask(ResourceData resourceData, Type resourceType, int priority)
        {
            ResourceData = resourceData;
            ResourceType = resourceType;
            Priority = priority;
        }
    }
    private readonly Dictionary<ResourceData, ResourceInfo> _resources = new Dictionary<ResourceData, ResourceInfo>();
    private readonly List<ResourceData> preloadResourcesList = new List<ResourceData>(); // 预加载资源列表
    private readonly List<ResourceLoadTask> loadQueue = new List<ResourceLoadTask>();

    public ResourceManager()
    {
        Debug.Log("ResourceManager Init");
    }

    #region 常规资源加载逻辑

    public List<ResourceData> PreloadResourcesList => preloadResourcesList;
    
    public async UniTask LoadPermanentResources()
    {
        var json = DataJsonManager.Instance.GetJson(DataType.ResourcesData);
        if (json != null)
        {
            var fromJson = JsonUtility.FromJson<ResourcesContainer>(json);
            foreach (var resource in fromJson.Resources)
            {
                if (resource.IsPermanent)
                {
                    var resourceAsync = await LoadResourceAsync<object>(resource);
                    if (_resources.TryGetValue(resource, out var resourceInfo))
                    {
                        resourceInfo.RefCount++;
                        return;
                    }
                    _resources.Add(resource, new ResourceInfo { Resource = resourceAsync, RefCount = 1 });
                }
            }
        }
        else
        {
            Debug.LogWarning("ResourcesData.json not found");
        }
    }

    public AsyncOperationHandle<SceneInstance> LoadSceneAsync(string sceneName, LoadSceneMode mode = LoadSceneMode.Single)
    {
        try
        {
            var scene = DataJsonManager.Instance.GetResourceData(sceneName);
            var sceneAddress = "Assets/HotUpdate/Res" + scene.Address;
            return Addressables.LoadSceneAsync(sceneAddress, mode);
        }
        catch (Exception e)
        {
            throw new Exception($"Load scene failed: {sceneName}", e);
        }
    }

    // 异步加载资源
    public async UniTask<T> LoadResourceAsync<T>(ResourceData resourceData)
    {
        if (_resources.TryGetValue(resourceData, out var resourceInfo) && resourceInfo.Resource is T infoResource)
        {
            resourceInfo.RefCount++;
            return infoResource;
        }
        else
        {
            try
            {
                var resource = await Addressables.LoadAssetAsync<T>("Assets/HotUpdate/Res"+ resourceData.Address).Task;
                if (resource is GameObject go)
                {
                    if (!go.TryGetComponent<ResourceComponent>(out var resourceComponent))
                    {
                        resourceComponent = go.AddComponent<ResourceComponent>();
                    }
                    resourceComponent.ResourceData = resourceData;
                    Debug.Log($"Load resource success: {resourceData.Name}");
                }
                _resources.Add(resourceData, new ResourceInfo { Resource = resource, RefCount = 1 });
                return resource;
            }
            catch (Exception e)
            {
                throw new Exception($"Load resource failed: {resourceData.Name}", e);
            }
        }
    }

    // 预加载资源
    public async UniTask PreloadResources()
    {
        if (preloadResourcesList.Count == 0)
        {
            var json = DataJsonManager.Instance.GetJson(DataType.ResourcesData);
            if (json != null)
            {
                var res = JsonUtility.FromJson<ResourcesContainer>(json);
                foreach (var resource in res.Resources)
                {
                    if (resource.IsPreload)
                    {
                        preloadResourcesList.Add(resource);
                    }
                }
            }
        }

        foreach (var resourceData in preloadResourcesList)
        {
            await LoadResourceAsync<object>(resourceData);
        }
    }

    // 根据ResourceData获取资源
    public T GetResource<T>(ResourceData resourceData)
    {
        if (_resources.TryGetValue(resourceData, out var cachedResource) && cachedResource.Resource is T resource)
        {
            cachedResource.RefCount++;
            return resource;
        }
        Debug.LogError($"Resource not found: {resourceData.Name}");
        return default;
    }

    // 卸载资源
    public void UnloadResource(ResourceData resourceData)
    {
        if (_resources.TryGetValue(resourceData, out var resource))
        {
            resource.RefCount--;
            if (resource.RefCount <= 0)
            {
                
                Addressables.Release(resource.Resource);
                _resources.Remove(resourceData);
            }
        }
    }

    // 清理所有缓存的资源
    public void ClearCache()
    {
        foreach (var address in _resources.Keys)
        {
            Addressables.Release(_resources[address].Resource);
        }
        _resources.Clear();
    }

    #endregion// 加载队列处理方法
    public async UniTask ProcessLoadQueueAsync<T>()
    {
        while (loadQueue.Count > 0)
        {
            // 按优先级排序，这里简单地使用LINQ，实际项目中可以考虑性能更优的排序算法
            loadQueue.Sort((task1, task2) => task2.Priority.CompareTo(task1.Priority));

            var highestPriorityTask = loadQueue[0];
            loadQueue.RemoveAt(0); // 移除已选择的任务

            // 异步加载资源
            await LoadResourceAsync<T>(highestPriorityTask.ResourceData);
        }
    }
    // 异步加载资源，带优先级
    public async UniTask<T> LoadResourceAsync<T>(ResourceData resourceData, int priority = 0)
    {
        if (_resources.TryGetValue(resourceData, out var cachedResource) && cachedResource is T)
        {
            cachedResource.RefCount++;
            return (T)cachedResource.Resource;
        }
        else
        {
            // 加入加载队列
            var task = new ResourceLoadTask(resourceData, typeof(T), priority);
            loadQueue.Add(task);

            // 立即处理加载队列
            await ProcessLoadQueueAsync<T>();

            // 加载完成后，资源已缓存，直接返回
            if (_resources.TryGetValue(resourceData, out cachedResource) && cachedResource is T)
            {
                return (T)cachedResource.Resource;
            }
        }
        return default;
    }
    
    // 添加一个通用的方法来筛选资源
    public IEnumerable<T> GetResources<T>(Func<ResourceDataInfo, bool> predicate)
    {
        foreach (var resourcePair in _resources)
        {
            var resourceDataInfo = new ResourceDataInfo
            {
                resourceInfo = resourcePair.Value,
                resourceData = resourcePair.Key
            };
            if (predicate(resourceDataInfo) && resourcePair.Value.Resource is T resource)
            {
                yield return resource;
            }
        }
    }
}

public static class ResourceManagerExtensions
{
    public static ScriptableObject[] GetAllScriptableObjects(this ResourceManager manager)
    {
        var res = manager.GetResources<ScriptableObject>(resourceInfo => resourceInfo.resourceInfo.Resource is ScriptableObject).ToArray();
        if (res.Length == 0)
        {
            Debug.LogWarning($"ResourceManager has no ScriptableObjects");
            return null;
        }
        return res;
    }

    public static GameObject[] GetAllUIObjects(this ResourceManager manager)
    {
        var res = manager.GetResources<GameObject>(resourceInfo => resourceInfo.resourceInfo.Resource is GameObject go && go.GetComponent<ScreenUIBase>() != null).ToArray();
        if (res.Length == 0)
        {
            Debug.LogWarning($"ResourceManager has no UIObjects");
            return null;
        }
        return res;
    }

    public static GameObject[] GetPermanentUI(this ResourceManager manager)
    {
        var res = manager.GetResources<GameObject>(resourceInfo => resourceInfo.resourceInfo.Resource is GameObject go && go.GetComponent<ScreenUIBase>() != null && resourceInfo.resourceData.IsPermanent).ToArray();
        if (res.Length == 0)
        {
            Debug.LogWarning($"ResourceManager has no Permanent UIObjects");
            return null;
        }
        return res;
    }

    public static async UniTask<List<GameObject>> GetMapResource(this ResourceManager manager, string mapName)
    {
        var list = new List<GameObject>();
        var data = DataJsonManager.Instance.GetResourcesDataByAddress($"Map/{mapName}");
        if (data != null)
        {
            foreach (var resourceData in data)
            {
                var go = await manager.LoadResourceAsync<GameObject>(resourceData);
                list.Add(go);
            }
            return list;
        }
        throw new NotImplementedException("GetMaps not implemented");
    }
}