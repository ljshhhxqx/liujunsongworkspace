using System;
using Cysharp.Threading.Tasks;
using Game.Map;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using Sirenix.OdinInspector;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Game.Map
{
    public class GameMapInit : NetworkAutoInjectHandlerBehaviour // MonoBehaviour NetworkAutoInjectHandlerBehaviour
    {
        private string _mapName;
        private int _staticObjectId;
        
#if UNITY_EDITOR
        //[MenuItem("CONTEXT/InitMapStaticObjectsId")]
        [Button("静态物品ID初始化")]
        private void InitMapStaticObjectsId()
        {
            var mapStaticObject = FindObjectsOfType<GameStaticObject>();
            foreach (var staticObject in mapStaticObject)
            {
                _staticObjectId++;
                staticObject.ModifyId(_staticObjectId);
            }
        }
#endif
        [Inject]
        private async void Init(GameEventManager gameEventManager, UIManager uiManager)
        {
            //uiManager.SwitchUI<LoadingScreenUI>();
            _mapName ??= gameObject.scene.name;
            InjectGameObjects();
            await LoadGameResources();
            gameEventManager.Publish(new GameSceneResourcesLoadedEvent(_mapName));
            uiManager.InitMapSprites(_mapName);
            uiManager.InitMapUIs(_mapName);
            uiManager.CloseUI(UIType.Loading);
            Debug.Log("game map init complete!!!!!!!!!!");
        }

        private void InjectGameObjects()
        {
            var injectObject = GameObject.FindGameObjectWithTag("InjectObjects");
            if (!injectObject)
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
            var mapResource = await ResourceManager.Instance.GetMapResource(_mapName);
            if (mapResource == null)
            {
                throw new UnityException("Main map resource not found.");
            }

            foreach (var resource in mapResource)
            {
                Debug.Log($"加载资源成功: {resource.name}");
            }
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            var mapStaticObject = FindObjectsOfType<GameStaticObject>();
            foreach (var staticObject in mapStaticObject)
            {
        
                GameObjectContainer.Instance.AddStaticObject(staticObject.gameObject, staticObject.interactableCollider, staticObject.isMesh);
            }
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            var mapStaticObject = FindObjectsOfType<GameStaticObject>();
            foreach (var staticObject in mapStaticObject)
            {
        
                GameObjectContainer.Instance.AddStaticObject(staticObject.gameObject, staticObject.interactableCollider, staticObject.isMesh);
            }
        }

        private void OnDestroy()
        {
            GameObjectContainer.Instance.ClearObjects();
            ResourceManager.Instance.UnloadResourcesByAddress($"/Map/{_mapName}");
        }
    }
}
