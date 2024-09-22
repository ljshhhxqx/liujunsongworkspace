using System;
using Common;
using Cysharp.Threading.Tasks;
using Game.Map;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Map
{
    public class GameMapInit : MonoBehaviour
    {
        [SerializeField]
        private string mapName;
        
        private async void Start()
        {
            InjectGameObjects();
            await LoadGameResources();
        }

        private void InjectGameObjects()
        {
            var injectObject = GameObject.FindGameObjectWithTag("InjectObjects");
            if (injectObject == null)
            {
                throw new UnityException("InjectObjects GameObject not found.");
            }

            var childObjects = injectObject.GetComponentsInChildren<IInjectableObject>();

            foreach (var injectable in childObjects)
            {
                if (injectable is MonoBehaviour monoBehaviour)
                {
                    var ns = monoBehaviour.GetType().Namespace;
                    if (ns != null && !ns.StartsWith("UnityEngine"))
                    {
                        try
                        {
                            ObjectInjectProvider.Instance.Inject(monoBehaviour);
                            Debug.Log($"成功注入: {monoBehaviour.GetType().Name}");
                        }
                        catch (Exception e)
                        {
                            throw new UnityException($"注入 {monoBehaviour.GetType().Name} 时发生错误: {e.Message}", e);
                        }
                    }
                }
            }
        }

        private async UniTask LoadGameResources()
        {
            var mapResource = await ResourceManager.Instance.GetMapResource(mapName);
            if (mapResource == null)
            {
                throw new UnityException("Main map resource not found.");
            }

            foreach (var resource in mapResource)
            {
                Debug.Log($"加载资源成功: {resource.name}");
            }
        }

        private void OnDestroy()
        {
            ResourceManager.Instance.UnloadResourcesByAddress($"/Map/{mapName}");
        }
    }
}
