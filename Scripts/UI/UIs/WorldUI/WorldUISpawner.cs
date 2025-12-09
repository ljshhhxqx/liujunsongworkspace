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
            controller.InitFollowedInstance(go, playerSpawnedEvent.PlayerId, _uiCanvas, MainCamera);
            controller.BindToModel(dataModel);
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
                dataModel.Dispose();
                _dataModels[WorldUIType.CollectItem].Remove(sceneItemSpawnedEvent.ItemId);
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
            controller.InitFollowedInstance(go, sceneItemSpawnedEvent.ItemId, _uiCanvas, MainCamera);
            controller.BindToModel(dataModel);
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
                infoDataModel.Health.Value = playerInfoChangedEvent.Health;
                infoDataModel.MaxHealth.Value = playerInfoChangedEvent.MaxHealth;
                infoDataModel.Mana.Value = playerInfoChangedEvent.Mana;
                infoDataModel.MaxMana.Value = playerInfoChangedEvent.MaxMana;
                infoDataModel.Name.Value = playerInfoChangedEvent.PlayerName;
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
                infoDataModel.Health.Value = sceneItemInfoChangedEvent.SceneItemInfo.health;
                infoDataModel.MaxHealth.Value = sceneItemInfoChangedEvent.SceneItemInfo.maxHealth;
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