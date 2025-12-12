using System.Collections.Generic;
using AOTScripts.Tool;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIs.UIFollow.DataModel;
using HotUpdate.Scripts.UI.UIs.UIFollow.UIController;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.UI.UIs.WorldUI
{
    public class WorldUISpawner : SingletonAutoMono<WorldUISpawner>
    {
        private GameEventManager _gameEventManager;
        private Camera _mainCamera;
        private Canvas _uiCanvas;
        private GameObject _uiFollowParent;
        private Dictionary<WorldUIType, GameObject> _prefabs = new Dictionary<WorldUIType, GameObject>();
        private Dictionary<WorldUIType, Dictionary<uint, IUIDataModel>> _dataModels = new Dictionary<WorldUIType, Dictionary<uint, IUIDataModel>>();
        
        private Dictionary<uint, FollowedUIController> _followedUIControllers = new Dictionary<uint, FollowedUIController>();
        private Camera MainCamera => _mainCamera ??= Camera.main;
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnGameSceneResourcesLoaded);
            _gameEventManager.Subscribe<PlayerInfoChangedEvent>(OnPlayerInfoChanged);
            _gameEventManager.Subscribe<PlayerSpawnedEvent>(OnPlayerSpawned);
            _gameEventManager.Subscribe<SceneItemInfoChangedEvent>(OnSceneItemInfoChanged);
            _gameEventManager.Subscribe<SceneItemSpawnedEvent>(OnSceneItemSpawned);
            _uiCanvas = transform.parent.GetComponent<Canvas>();
        }

        private void OnPlayerSpawned(PlayerSpawnedEvent playerSpawnedEvent)
        {
            var dataModel = GetDataModel(WorldUIType.CollectItem, playerSpawnedEvent.PlayerId, out var dataModels);

            if (!playerSpawnedEvent.Spawned)
            {
                dataModel.Dispose();
                _dataModels[WorldUIType.PlayerItem].Remove(playerSpawnedEvent.PlayerId);
                _followedUIControllers.Remove(playerSpawnedEvent.PlayerId);
                GameObjectPoolManger.Instance.ReturnObject(_followedUIControllers[playerSpawnedEvent.PlayerId].gameObject);
                return;
            }

            if (!_prefabs.TryGetValue(WorldUIType.PlayerItem, out var prefab))
            {
                Debug.LogWarning($"No prefab found for {WorldUIType.PlayerItem}");
                return;
            }
            
            _dataModels[WorldUIType.PlayerItem] = dataModels;
            var go = GameObjectPoolManger.Instance.GetObject(prefab, parent: transform);
            var controller = go.GetComponent<FollowedUIController>();
            controller.InitFollowedInstance(playerSpawnedEvent.Target, playerSpawnedEvent.PlayerId, playerSpawnedEvent.Target.transform, MainCamera);
            controller.BindToModel(dataModel);
            _followedUIControllers.Add(playerSpawnedEvent.PlayerId, controller);
        }
        
        private IUIDataModel GetDataModel(WorldUIType uiType, uint id, out Dictionary<uint, IUIDataModel> dataModels)
        {
            if (!_dataModels.TryGetValue(uiType, out dataModels))
            {
                dataModels = new Dictionary<uint, IUIDataModel>();
                _dataModels.Add(uiType, dataModels);
            }

            if (!dataModels.TryGetValue(id, out var dataModel))
            {
                switch (uiType)
                {
                    case WorldUIType.CollectItem:
                    case WorldUIType.PlayerItem:
                        dataModel = new InfoDataModel();
                        break;
                }
                dataModels.Add(id, dataModel);
            }

            return dataModel;
        }

        private void OnSceneItemSpawned(SceneItemSpawnedEvent sceneItemSpawnedEvent)
        {
            var dataModel = GetDataModel(WorldUIType.CollectItem, sceneItemSpawnedEvent.ItemId, out var dataModels);

            if (!sceneItemSpawnedEvent.Spawned)
            {
                GameObjectPoolManger.Instance.ReturnObject(_followedUIControllers[sceneItemSpawnedEvent.ItemId].gameObject);
                dataModel.Dispose();
                _dataModels[WorldUIType.CollectItem].Remove(sceneItemSpawnedEvent.ItemId);
                _followedUIControllers.Remove(sceneItemSpawnedEvent.ItemId);
                return;
            }

            if (!_prefabs.TryGetValue(WorldUIType.CollectItem, out var prefab))
            {
                Debug.LogWarning($"No prefab found for {WorldUIType.CollectItem}");
                return;
            }
            
            _dataModels[WorldUIType.CollectItem] = dataModels;
            var go = GameObjectPoolManger.Instance.GetObject(prefab, parent: transform);
            var controller = go.GetComponent<FollowedUIController>();
            controller.InitFollowedInstance(sceneItemSpawnedEvent.SpawnedObject, sceneItemSpawnedEvent.ItemId, sceneItemSpawnedEvent.Player, MainCamera);
            controller.BindToModel(dataModel);
            _followedUIControllers.Add(sceneItemSpawnedEvent.ItemId, controller);
            Debug.Log($"Spawned {sceneItemSpawnedEvent.SpawnedObject.name} with id {sceneItemSpawnedEvent.ItemId}");
        }

        private void OnPlayerInfoChanged(PlayerInfoChangedEvent playerInfoChangedEvent)
        {
            var dataModel = GetDataModel(WorldUIType.PlayerItem, playerInfoChangedEvent.PlayerId, out var dataModels);
            if (dataModel == null)
            {
                Debug.LogWarning($"No dataModel found for {playerInfoChangedEvent.PlayerId}");
                return;
            }

            if (dataModel is InfoDataModel infoDataModel)
            {
                infoDataModel.Health.Value = (int)playerInfoChangedEvent.Health;
                infoDataModel.MaxHealth.Value = (int)playerInfoChangedEvent.MaxHealth;
                infoDataModel.Mana.Value = (int)playerInfoChangedEvent.Mana;
                infoDataModel.MaxMana.Value = (int)playerInfoChangedEvent.MaxMana;
                infoDataModel.Name.Value ??= playerInfoChangedEvent.PlayerName;
            }
        }

        private void OnSceneItemInfoChanged(SceneItemInfoChangedEvent sceneItemInfoChangedEvent)
        {
            var dataModel = GetDataModel(WorldUIType.CollectItem, sceneItemInfoChangedEvent.ItemId, out var dataModels);
            if (dataModel == null)
            {
                Debug.LogWarning($"No dataModel found for {sceneItemInfoChangedEvent.ItemId}");
                return;
            }

            if (dataModel is InfoDataModel infoDataModel)
            {
                infoDataModel.Health.Value = (int)sceneItemInfoChangedEvent.SceneItemInfo.health;
                infoDataModel.MaxHealth.Value = (int)sceneItemInfoChangedEvent.SceneItemInfo.maxHealth;
                infoDataModel.Name.Value ??= $"{WorldUIType.CollectItem.ToString()}/{sceneItemInfoChangedEvent.ItemId}";
            }
        }

        private void OnGameSceneResourcesLoaded(GameSceneResourcesLoadedEvent gameSceneResourcesLoadedEvent)
        {
            var resources = ResourceManager.Instance.GetResources<GameObject>(resourceData => resourceData.resourceData.Address.StartsWith($"/Map/{GameStaticExtensions.CommonMapName}/WorldUIPrefab") && resourceData.resourceInfo.Resource is GameObject);
            foreach (var resource in resources)
            {
                if (resource.TryGetComponent<FollowedUIController>(out var followedUIController))
                {
                    _prefabs.TryAdd(followedUIController.worldUIType, resource);
                }

                if (resource.CompareTag("UIFollowParent"))
                {
                    _uiFollowParent = resource;
                }
            }
        }
    }

    public enum WorldUIType
    {
        None,
        CollectItem,
        PlayerItem,
    }
}