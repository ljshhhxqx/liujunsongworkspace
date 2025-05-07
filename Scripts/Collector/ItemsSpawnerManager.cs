using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.Tool.Static;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using MemoryPack;
using Mirror;
using Tool.GameEvent;
using UI.UIBase;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class ItemsSpawnerManager : ServerNetworkComponent
    {
        private readonly Dictionary<int, CollectObjectController> _collectiblePrefabs = new Dictionary<int, CollectObjectController>();

        private readonly Dictionary<QualityType, Dictionary<PropertyTypeEnum, Material>> _collectibleMaterials =
            new Dictionary<QualityType, Dictionary<PropertyTypeEnum, Material>>();
        private readonly Dictionary<QualityType, DroppedItem> _droppedItemPrefabs = new Dictionary<QualityType, DroppedItem>();
        private readonly Dictionary<QualityType, TreasureChestComponent> _treasureChestPrefabs = new Dictionary<QualityType, TreasureChestComponent>();
        private IConfigProvider _configProvider;
        private MapBoundDefiner _mapBoundDefiner;
        private JsonDataConfig _jsonDataConfig;
        private ChestDataConfig _chestConfig;
        private MessageCenter _messageCenter;
        private GameMapInjector _gameMapInjector;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private LayerMask _sceneLayer;
        private LayerMask _spawnedLayer;
        private Dictionary<Vector2Int, Grid> _gridMap = new Dictionary<Vector2Int, Grid>();
        private static float _itemSpacing;
        private static int _maxGridItems;
        private static float _itemHeight;
        private static float _gridSize; // Size of each grid cell
        private static int _onceSpawnCount;
        private static int _onceSpawnWeight;
        private Transform _spawnedParent;
        private Collider[] _colliderBuffer; // 复用缓冲区
        private UIManager _uiManager;
        private ConstantBuffConfig _constantBuffConfig;
        private RandomBuffConfig _randomBuffConfig;
        private ShopConfig _shopConfig;
        private GameLoopController _gameLoopController;
        private GameSyncManager _gameSyncManager;
        private PlayerInGameManager _playerInGameManager;
        
        // 服务器维护的核心数据
        private readonly SyncDictionary<uint, byte[]> _serverItemMap = new SyncDictionary<uint, byte[]>();
        private readonly Dictionary<int, IColliderConfig> _colliderConfigs = new Dictionary<int, IColliderConfig>();
        private readonly HashSet<uint> _processedItems = new HashSet<uint>();
        private readonly Dictionary<uint, CollectObjectController> _clientCollectObjectControllers = new Dictionary<uint, CollectObjectController>();
        
        [SyncVar]
        //private byte[] _serverTreasureChestMetaDataBytes;
        private CollectItemMetaData _serverTreasureChestMetaData;
        private readonly Dictionary<QualityType, IColliderConfig> _chestColliderConfigs = new Dictionary<QualityType, IColliderConfig>();
        private readonly Dictionary<QualityType, IColliderConfig> _droppedItemColliderConfigs = new Dictionary<QualityType, IColliderConfig> ();
        private TreasureChestComponent _clientTreasureChest;

        [Inject]
        private void Init(MapBoundDefiner mapBoundDefiner, UIManager uiManager, IConfigProvider configProvider, GameSyncManager gameSyncManager, GameMapInjector gameMapInjector, PlayerInGameManager playerInGameManager,GameEventManager gameEventManager, MessageCenter messageCenter)
        {
            _uiManager = uiManager;
            _configProvider = configProvider;
            _jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _mapBoundDefiner = mapBoundDefiner;
            _playerInGameManager = playerInGameManager;
            _gameMapInjector = gameMapInjector;
            _gameSyncManager = gameSyncManager;
            _messageCenter = messageCenter;
            gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnGameSceneResourcesLoadedLoaded);
            _collectObjectDataConfig = _configProvider.GetConfig<CollectObjectDataConfig>();
            _constantBuffConfig = _configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = _configProvider.GetConfig<RandomBuffConfig>();
            _chestConfig = _configProvider.GetConfig<ChestDataConfig>();
            _shopConfig = _configProvider.GetConfig<ShopConfig>();
            _sceneLayer = _jsonDataConfig.GameConfig.groundSceneLayer;
            // _messageCenter.Register<PickerPickUpChestMessage>(OnPickerPickUpChestMessage);
            // _messageCenter.Register<PickerPickUpMessage>(OnPickUpItem);
            _gameLoopController = FindObjectOfType<GameLoopController>();
            _spawnedParent = transform;
        }

        private void OnGameSceneResourcesLoadedLoaded(GameSceneResourcesLoadedEvent gameSceneResourcesLoadedEvent)
        {
            var collectData = _jsonDataConfig.CollectData;
            _itemSpacing = collectData.itemSpacing;
            _maxGridItems = collectData.maxGridItems;
            _itemHeight = collectData.itemHeight;
            _gridSize = collectData.gridSize;
            _onceSpawnCount = collectData.onceSpawnCount;
            _onceSpawnWeight = collectData.onceSpawnWeight;
            OnGameStart(gameSceneResourcesLoadedEvent.SceneName);
            InitializeGrid();
        }

        [Server]
        public void SpawnItemsByDroppedItems(byte[] droppedItemsBytes, Vector3 position)
        {
            var items = MemoryPackSerializer.Deserialize<DroppedItemData[]>(droppedItemsBytes);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var go = GameObjectPoolManger.Instance.GetObject(_droppedItemPrefabs[item.Quality].gameObject, position, Quaternion.identity
                , _spawnedParent);
                var droppedItem = go.GetComponent<DroppedItem>();
            }
        }
        
        [ClientRpc]
        public void SpawnItemsByDroppedItemsClientRpc(byte[] droppedItemsBytes, Vector3 position)
        {
            var items = MemoryPackSerializer.Deserialize<DroppedItemData[]>(droppedItemsBytes);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var go = GameObjectPoolManger.Instance.GetObject(_droppedItemPrefabs[item.Quality].gameObject, position, Quaternion.identity
                , _spawnedParent);
                var droppedItem = go.GetComponent<DroppedItem>();
            }
        }

        [Server]
        public void PickerPickUpChest(uint pickerId, uint itemId)
        {
            var state = (ItemState)_serverTreasureChestMetaData.StateFlags;
            if (state.HasAnyState(ItemState.IsInteracting) || state.HasAnyState(ItemState.IsInBag)) return;
            state = state.AddState(ItemState.IsInteracting);
            _serverTreasureChestMetaData.StateFlags = (byte)state;
            if (itemId != _serverTreasureChestMetaData.ItemId)
            {
                Debug.LogError($"Item id {itemId} is not match with treasure chest id {_serverTreasureChestMetaData.ItemId}");
                state = state.RemoveState(ItemState.IsInteracting);
                _serverTreasureChestMetaData.StateFlags = (byte)state;
                return;
            }
            var chestData = _serverTreasureChestMetaData.GetCustomData<ChestItemCustomData>();
            var chestPos = _serverTreasureChestMetaData.Position.ToVector3();
            var player = NetworkServer.spawned[pickerId];
            var connectionId = player.connectionToClient.connectionId;
            if (!player)
            {
                Debug.LogError($"Player {pickerId} could not be found");
                state = state.RemoveState(ItemState.IsInteracting);
                _serverTreasureChestMetaData.StateFlags = (byte)state;
                return;
            }
            var playerConnection = player.connectionToClient.connectionId;
            var playerCollider = _playerInGameManager.GetPlayerPhysicsData();

            var chestCollider = _chestColliderConfigs.GetValueOrDefault(chestData.Quality);

            try
            {
                if (ValidatePickup(chestPos, player.transform.position, chestCollider, playerCollider))
                {
                     var configData = _chestConfig.GetChestConfigData(chestData.ChestConfigId);
                     var extraItems = _shopConfig.GetItemsByShopId(chestData.ShopIds);
                     extraItems.UnionWith(configData.itemIds);
                     var items = extraItems.Select(x => new ItemsCommandData
                     {
                         ItemConfigId = x,
                         Count = 1,
                         ItemUniqueId = new int[]{ HybridIdGenerator.GenerateItemId(x, GameSyncManager.CurrentTick) } 
                     }).ToArray();
                     _gameSyncManager.EnqueueServerCommand(new ItemsGetCommand
                     {
                         Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item),
                         Items = items,
                     });
                     Debug.Log($"Player {player.name} pick up chest {chestData.ChestConfigId}");
                     _serverTreasureChestMetaData = default;
                     RpcPickupChest(pickerId, itemId);
                }
                else
                {
                    state = state.RemoveState(ItemState.IsInteracting);
                    _serverTreasureChestMetaData.StateFlags = (byte)state;
                    Debug.Log($"Player {player.netId} cannot pick up chest");
                }
            
            }
            catch (Exception e)
            {
                state = state.RemoveState(ItemState.IsInteracting);
                _serverTreasureChestMetaData.StateFlags = (byte)state;
                Console.WriteLine(e);
                throw;
            }
        }

        [ClientRpc]
        private void RpcPickupChest(uint pickerId, uint itemId)
        {
            var item = _clientCollectObjectControllers[itemId];
            item.CollectSuccess();
            GameObjectPoolManger.Instance.ReturnObject(item.gameObject);
        }

        [Server]
        public void PickerPickupItem(uint pickerId, uint itemId)
        {
            HandleItemPickup(pickerId, itemId);
            JudgeEndRound();
        }

        private void OnGameStart(string sceneName)
        {
            _gridMap.Clear();
            _gridMap = _mapBoundDefiner.GridMap.ToDictionary(x => x,_ => new Grid());
            var res = ResourceManager.Instance.GetMapCollectObject(sceneName);
            if (_collectiblePrefabs.Count == 0)
            {
                foreach (var data in res)
                {
                    if (data.TryGetComponent(out CollectObjectController controller))
                    {
                        var component = data.GetComponent<CollectObjectController>();
                        _collectiblePrefabs.Add(component.CollectConfigId, component);
                    }
                }
            }

            if (_colliderConfigs.Count == 0)
            {
                foreach (var data in _collectiblePrefabs.Values)
                {
                    var gameObjectCollider = data.GetComponent<Collider>();
                    var config = GamePhysicsSystem.CreateColliderConfig(gameObjectCollider);
                    if (config != null)
                    {
                        _colliderConfigs.Add(data.CollectConfigId, config);
                    }
                }
            }

            if (_collectibleMaterials.Count == 0)
            {
                var collectObjectSizes = Enum.GetValues(typeof(QualityType)).Cast<QualityType>().ToArray();
                for (var i = 0; i < collectObjectSizes.Length; i++)
                {
                    var qualityType = collectObjectSizes[i];
                    if (!_collectibleMaterials.ContainsKey(qualityType))
                    {
                        _collectibleMaterials.Add(qualityType, new Dictionary<PropertyTypeEnum, Material>());
                    }
                    var matList = ResourceManager.Instance.GetMapCollectObjectMaterial(sceneName, qualityType.ToString());
                    foreach (var material in matList)
                    {
                        if (Enum.TryParse(material.name, out PropertyTypeEnum propertyType))
                        {
                            _collectibleMaterials[qualityType].Add(propertyType, material);
                        }
                    }
                }
            }

            if (_treasureChestPrefabs.Count == 0)
            {
                foreach (var go in res)
                {
                    if (go.TryGetComponent<TreasureChestComponent>(out var treasureChest))
                    {
                        _treasureChestPrefabs.Add(treasureChest.Quality, treasureChest);
                    }
                }
            }

            if (_chestColliderConfigs.Count == 0)
            {
                foreach (var data in _treasureChestPrefabs.Values)
                {
                    var gameObjectCollider = data.GetComponent<Collider>();
                    var config = GamePhysicsSystem.CreateColliderConfig(gameObjectCollider);
                    if (config != null)
                    {
                        _chestColliderConfigs.Add(data.Quality, config);
                    }
                }
            }
            
            if (_droppedItemPrefabs.Count == 0)
            {
                foreach (var go in res)
                {
                    if (go.TryGetComponent<DroppedItem>(out var droppedItem))
                    {
                        _droppedItemPrefabs.Add(droppedItem.droppedItemSceneData.qualityType, droppedItem);
                    }
                }
            }
            
            if (_droppedItemColliderConfigs.Count == 0)
            {
                foreach (var data in _droppedItemPrefabs.Values)
                {
                    var gameObjectCollider = data.GetComponent<Collider>();
                    var config = GamePhysicsSystem.CreateColliderConfig(gameObjectCollider);
                    if (config != null)
                    {
                        _droppedItemColliderConfigs.Add(data.droppedItemSceneData.qualityType, config);
                    }
                }
            }

            _uiManager.SwitchUI<TargetShowOverlay>();
        }

        private void OnDestroy()
        {
            if (isLocalPlayer)
            {
                _uiManager.CloseUI(UIType.TargetShowOverlay);
            }
        }

        [Server]
        private void HandleItemPickup(uint itemId, uint pickerId)
        {
            if (_serverItemMap.TryGetValue(itemId, out var itemInfo))
            {
                if (!_processedItems.Add(itemId))
                {
                    return;
                }
                var itemData = MemoryPackSerializer.Deserialize<CollectItemMetaData>(itemInfo);
                var itemColliderData = _colliderConfigs.GetValueOrDefault(itemData.ItemCollectConfigId);
                var itemConfigId = _collectObjectDataConfig.GetCollectObjectData(itemData.ItemCollectConfigId).itemId;
                var itemPos = itemData.Position;
                var customData = itemData.GetCustomData<CollectItemCustomData>();
                var player =  NetworkServer.spawned[pickerId];
                var playerConnectionId = _playerInGameManager.GetPlayerId(pickerId);
                var playerColliderConfig = _playerInGameManager.GetPlayerPhysicsData();
                if (!player)
                {
                    Debug.LogError($"Cannot find player with netId: {pickerId}");
                    return;
                }
                
                // 验证位置和碰撞
                if (ValidatePickup(itemPos.ToVector3(), player.transform.position, itemColliderData, playerColliderConfig))
                {
                    // 处理拾取逻辑
                    _gameSyncManager.EnqueueServerCommand(new ItemsGetCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Item),
                        Items = new ItemsCommandData[]
                        {
                            new ItemsCommandData()
                            {
                                ItemConfigId = itemConfigId,
                                Count = 1,
                                ItemUniqueId = new []{ customData.ItemUniqueId},
                            },
                        },
                    });

                    // 通知客户端
                    RpcPickupItem(itemId);
            
                    _processedItems.Remove(itemId);
                    _serverItemMap.Remove(itemId);
                }
            }
        }
        
        [ClientRpc]
        private void RpcPickupItem(uint itemId)
        {
            var item = _clientCollectObjectControllers[itemId];
            item.CollectSuccess();
            GameObjectPoolManger.Instance.ReturnObject(item.gameObject);
        }
        
        [Server]
        public int GenerateRandomWeight()
        {
            // 根据需要生成随机权重
            return Mathf.FloorToInt(Random.Range(_onceSpawnWeight-10, _onceSpawnWeight+10));
        }

        [Server]
        public int GenerateRandomSpawnMethod()
        {
            // 随机选择生成方式，0-3分别对应四种生成方式
            return Random.Range(1, 4);
        }

        [Server]
        private void JudgeEndRound()
        {
            var endRound = _serverItemMap.Count == 0 && _serverTreasureChestMetaData.ItemId == 0;
            if (endRound)
            {
                _gameLoopController.IsEndRound = true;
            }
        }

        [Server]
        public async UniTask EndRound()
        {
            try
            {
                Debug.Log("Starting EndRound");
                // 清理网格数据
                _serverTreasureChestMetaData = default;
                foreach (var grid in _gridMap)
                {
                    _gridMap[grid.Key] = new Grid(new List<int>());
                }

                Debug.Log("EndRound finished on server");
                // 通知客户端清理
                RpcEndRound();
                _serverItemMap.Clear();
                await UniTask.Yield();
                
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in EndRound: {e}");
            }
        }

        [ClientRpc]
        private void RpcEndRound()
        {
            if (isClient)
            {
                // 清理客户端的生成物
                foreach (var item in _clientCollectObjectControllers.Values)
                {
                    GameObjectPoolManger.Instance.ReturnObject(item.gameObject);
                }
                GameObjectPoolManger.Instance.ReturnObject(_clientTreasureChest.gameObject);
            } 

            Debug.Log("EndRound finished on client");
        }

        [Server]
        public async UniTask SpawnItemsAndChest()
        {
            await SpawnManyItems();
            SpawnTreasureChestServer();
        }
        
        private CollectObjectController GetPrefabByCollectType(int configId)
        {
            if (_collectiblePrefabs.TryGetValue(configId, out var itemData))
            {
                return itemData;
            }
            Debug.LogError($"No prefab found for CollectType: {configId}");
            return null;
        }

        [Server]
        public async UniTask SpawnManyItems()
        {
            try
            {
                _serverItemMap.Clear();
                var spawnedCount = 0;
                Debug.Log("Starting SpawnManyItems");
                while (spawnedCount < _onceSpawnCount)
                {
                    var newSpawnInfos = SpawnItems(GenerateRandomWeight(), GenerateRandomSpawnMethod());
                    if (newSpawnInfos.Count == 0)
                    {
                        await UniTask.Yield();
                        continue;
                    }
                    spawnedCount += newSpawnInfos.Count;
                    foreach (var item in newSpawnInfos)
                    {
                        var id = CollectItemMetaData.GenerateItemId(item.Item2);
                        if (_serverItemMap.TryGetValue(id, out var itemInfo))
                        {
                            continue;
                        }

                        var buffData = GetBuffExtraData(item.Item1);
                        var buff = _randomBuffConfig.GetRandomBuffData(buffData.buffId);
                        var extraData = new CollectItemCustomData
                        {
                            RandomBuffId = buff.buffId,
                            ItemUniqueId = HybridIdGenerator.GenerateItemId(_collectObjectDataConfig.GetItemId(item.Item1), GameSyncManager.CurrentTick),
                        };
                        var state = (ItemState)0;
                        state = state.AddState(ItemState.IsActive);
                        var itemMetaData = new CollectItemMetaData(id, 
                            item.Item2,
                            (byte)state, 
                            item.Item1,
                            (uint)Mathf.Abs(GameSyncManager.CurrentTick),
                            30, 
                            (ushort)Random.Range(0, 65535),
                            -1);
                        itemMetaData = itemMetaData.SetCustomData(extraData);
                        itemInfo = MemoryPackSerializer.Serialize(itemMetaData);
                        _serverItemMap.Add(id, itemInfo);
                    }
                    Debug.Log($"Calculated {spawnedCount} spawn positions");
                    await UniTask.Yield();
                }
                SpawnManyItemsClientRpc(_serverItemMap.Values.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in SpawnManyItems: {e}");
                throw;
            }
        }

        private BuffExtraData GetBuffExtraData(int configId)
        {
            var configData = _collectObjectDataConfig.GetCollectObjectData(configId);
            return configData.buffExtraData;
        }

        [Server]
        public void SpawnTreasureChestServer()
        {
            var random = Random.Range(0, 1);
            var chestData = _chestConfig.RandomOne(random);
            var position = GetRandomStartPoint(0.75f);
            var id = CollectItemMetaData.GenerateItemId(position);
            var metaData = new CollectItemMetaData(id,
                position,
                0,
                chestData.chestId,
                (uint)Mathf.Abs(GameSyncManager.CurrentTick),
                30,
                (ushort)Random.Range(0, 65535),
                -1);
            var qualityItems = RandomItemsData.GenerateQualityItems(chestData.randomItems, random);
            var shopIds = _shopConfig.GetQualityItems(qualityItems, random);
            var chestUniqueId = HybridIdGenerator.GenerateChestId(chestData.chestId, GameSyncManager.CurrentTick);
            metaData = metaData.SetCustomData(new ChestItemCustomData
            {
                ChestConfigId = chestData.chestId,
                ShopIds = shopIds.ToArray(),
                ChestUniqueId = chestUniqueId,
                Quality = chestData.randomItems.quality,
            });
            var serverTreasureChestMetaDataBytes = MemoryPackSerializer.Serialize(metaData);
            _serverTreasureChestMetaData = metaData;
            RpcSpawnTreasureChest(serverTreasureChestMetaDataBytes);
            GameItemManager.AddChestData(new GameChestData
            {
                ChestId = chestUniqueId,
                ChestConfigId = chestData.chestId,
            }, netIdentity);
        }

        [ClientRpc]
        private void RpcSpawnTreasureChest(byte[] serverTreasureChestMetaData)
        {
            var metaData = MemoryPackSerializer.Deserialize<CollectItemMetaData>(serverTreasureChestMetaData);
            var position = metaData.Position.ToVector3();
            var quality = metaData.GetCustomData<ChestItemCustomData>().Quality;
            var treasureChestPrefab = _treasureChestPrefabs.GetValueOrDefault(quality);
            if (!treasureChestPrefab)
            {
                Debug.LogError($"No treasure chest prefab found for quality: {quality}");
                return;
            }
            var spawnedChest = GameObjectPoolManger.Instance.GetObject(
                treasureChestPrefab.gameObject,
                position,
                Quaternion.identity,
                _spawnedParent,
                go => _gameMapInjector.InjectGameObject(go)
            );
            _clientTreasureChest = spawnedChest.GetComponent<TreasureChestComponent>();
            _clientTreasureChest.ItemId = metaData.ItemId;
            //_clientTreasureChest.chestType = chestType;
            Debug.Log($"Client spawning treasure chest at position: {position} with id: {metaData.ItemId}");
        }

        [ClientRpc]
        private void SpawnManyItemsClientRpc(byte[][] allSpawnedItems)
        {
            var allItems = MemoryPackSerializer.Deserialize<CollectItemMetaData[]>(allSpawnedItems[0]);
            SpawnItems(allItems);
        }

        private void SpawnItems(CollectItemMetaData[] allSpawnedItems)
        {
            foreach (var data in allSpawnedItems)
            {
                var position = data.Position;
                Debug.Log($"Client spawning item at position: {position}"); // 添加日志
                var prefab = GetPrefabByCollectType(data.ItemCollectConfigId);
                if (!prefab)
                {
                    Debug.LogError($"Failed to find prefab for CollectType: {data.ItemCollectConfigId}");
                    continue;
                }

                var go = GameObjectPoolManger.Instance.GetObject(prefab.gameObject, position, Quaternion.identity, _spawnedParent,
                    go => _gameMapInjector.InjectGameObject(go));
                if (!go)
                {
                    Debug.LogError("Failed to get object from pool");
                    continue;
                }
                var configData = _collectObjectDataConfig.GetCollectObjectData(data.ItemCollectConfigId);
                var collectItemCustomData = data.GetCustomData<CollectItemCustomData>();
                var buff = _randomBuffConfig.GetRandomBuffData(collectItemCustomData.RandomBuffId);
                var component = go.GetComponent<CollectObjectController>();
                var material = _collectibleMaterials[component.Quality][buff.propertyType];
                component.collectId = data.ItemId;
                if (component.CollectObjectData.collectObjectClass == CollectObjectClass.Buff)
                {
                    component.SetBuffData(new BuffExtraData
                    {
                        buffId = buff.buffId,
                        buffType = BuffType.Random,
                    });
                    component.SetMaterial(material);
                }
                Debug.Log($"Spawning item at position: {position} with id: {data.ItemId}-{data.OwnerId}-{data.ItemCollectConfigId}-{buff.propertyType}-{buff.buffId}-{material.name}");
        
                // 确保位置正确设置
                go.transform.position = position;
                component.ItemId = data.ItemId;
                _clientCollectObjectControllers.Add(data.ItemId, component);
            }
        }

        private void InitializeGrid()
        {
            for (var x = _mapBoundDefiner.MapMinBoundary.x; x <= _mapBoundDefiner.MapMaxBoundary.x; x += _gridSize)
            {
                for (var z = _mapBoundDefiner.MapMinBoundary.z; z <= _mapBoundDefiner.MapMaxBoundary.z; z += _gridSize)
                {
                    var gridPos = GetGridPosition(new Vector3(x, 0, z));
                    _gridMap[gridPos] = new Grid(new List<int>());
                }
            }
        }

        private Vector2Int GetGridPosition(Vector3 position)
        {
            var x = Mathf.FloorToInt(position.x / _gridSize);
            var z = Mathf.FloorToInt(position.z / _gridSize);
            return new Vector2Int(x, z);
        }

        private List<(int, Vector3)> SpawnItems(int totalWeight, int spawnMode)
        {
            var collectTypes = new List<int>();
            var remainingWeight = totalWeight;

            switch (spawnMode)
            {
                case 1:
                    SpawnMode1(collectTypes, ref remainingWeight);
                    break;
                case 2:
                    SpawnMode2(collectTypes, ref remainingWeight);
                    break;
                case 3:
                    SpawnMode3(collectTypes, ref remainingWeight);
                    break;
            }

            return PlaceItems(collectTypes).Shuffle() as List<(int, Vector3)>;
        }

        /// <summary>
        /// 一直生成score类型item，直到remainingWeight小于等于0，再生成buff类型item
        /// </summary>
        /// <param name="itemsToSpawn"></param>
        /// <param name="remainingWeight"></param>
        private void SpawnMode1(List<int> itemsToSpawn, ref int remainingWeight)
        {
            var scoreCount = 0;
            var count = 0;
            var goldCount = 0;
            while (remainingWeight > 0)
            {
                var scoreItem = GetRandomItem(CollectObjectClass.Score);
                var goldItem = GetRandomItem(CollectObjectClass.Gold);
                if (scoreItem != -1)
                {
                    var configData = _collectObjectDataConfig.GetCollectObjectData(scoreItem);
                    itemsToSpawn.Add(scoreItem);
                    remainingWeight -= configData.weight;
                    scoreCount++;
                    count++;
                }

                if (goldItem != -1)
                {
                    var configData = _collectObjectDataConfig.GetCollectObjectData(goldItem);
                    itemsToSpawn.Add(goldItem);
                    remainingWeight -= configData.weight;
                    count++;
                    goldCount++;
                }

                if (count >= 3 && remainingWeight > 0)
                {
                    count = 0;
                    var buffItem = GetRandomItem(CollectObjectClass.Buff);
                    if (buffItem != -1)
                    {
                        var configData = _collectObjectDataConfig.GetCollectObjectData(buffItem);
                        itemsToSpawn.Add(buffItem);
                        remainingWeight -= configData.weight;
                        break; 
                    }
                }
            }

            if ((scoreCount > 0 && goldCount > 0) && remainingWeight > 0)
            {
                var buffItem = GetRandomItem(CollectObjectClass.Score);
                var goldItem = GetRandomItem(CollectObjectClass.Gold);
                if (buffItem != -1)
                {
                    itemsToSpawn.Add(buffItem);
                }

                if (goldItem != -1)
                {
                    itemsToSpawn.Add(goldItem);
                }
            }
        }

        private void SpawnMode2(List<int> itemsToSpawn, ref int remainingWeight)
        {
            var scoreItems = new List<int>();
            var buffItems = new List<int>();

            while (remainingWeight > 0)
            {
                var scoreItem1 = GetRandomItem(CollectObjectClass.Score);
                if (scoreItem1 != -1)
                {
                    var configData = _collectObjectDataConfig.GetCollectObjectData(scoreItem1);
                    scoreItems.Add(scoreItem1);
                    remainingWeight -= configData.weight;
                }

                var buffItem = GetRandomItem(CollectObjectClass.Buff);
                if (buffItem != -1)
                {
                    var configData = _collectObjectDataConfig.GetCollectObjectData(buffItem);
                    buffItems.Add(buffItem);
                    remainingWeight -= configData.weight;
                }
                
                var goldItem = GetRandomItem(CollectObjectClass.Gold);
                if (goldItem != -1)
                {
                    var configData = _collectObjectDataConfig.GetCollectObjectData(goldItem);
                    scoreItems.Add(goldItem);
                    remainingWeight -= configData.weight;
                }
            }

            itemsToSpawn.AddRange(scoreItems);
            itemsToSpawn.AddRange(buffItems);
        }

        private void SpawnMode3(List<int> itemsToSpawn, ref int remainingWeight)
        {
            var collectTypes = Enum.GetValues(typeof(CollectObjectClass)).Cast<CollectObjectClass>().ToArray();
            while (remainingWeight > 0)
            {
                var randomType = Random.Range(0, collectTypes.Length);
                var item = GetRandomItem(collectTypes[randomType]);
                if (item != -1)
                {
                    var type = item;
                    var configData = _collectObjectDataConfig.GetCollectObjectData(type);
                    itemsToSpawn.Add(type);
                    remainingWeight -= configData.weight;
                }
                
            }
        }
    
        private int GetRandomItem(CollectObjectClass itemClass)
        {
            var configData = _collectObjectDataConfig.GetCollectObjectDataWithCondition(x => x.collectObjectClass == itemClass)
                .Select(x => x.id)
                .ToArray();
            var count = configData.Length;
            if (count > 0)
            {
                var randomIndex = Random.Range(0, count);
                return configData[randomIndex];
            }
            return -1;
        }
        
        private bool ValidatePickup(Vector3 item, Vector3 player, IColliderConfig itemColliderConfig, IColliderConfig playerColliderConfig)
        {
            return GamePhysicsSystem.CheckIntersectsWithMargin(player, item, playerColliderConfig, itemColliderConfig);
        }
        
        private List<(int, Vector3)> PlaceItems(List<int> itemsToSpawn)
        {
            var startPoint = GetRandomStartPoint(_itemHeight);
            _colliderBuffer = new Collider[itemsToSpawn.Count];
            if (startPoint == Vector3.zero)
            {
                Debug.LogWarning("Failed to find valid start point");
                return new List<(int, Vector3)>();
            }

            var direction = GetRandomDirection();
            var spawnedIDs = new List<(int, Vector3)>();

            foreach (var configId in itemsToSpawn)
            {
                var attempts = 0;
                var itemPrefab = _collectiblePrefabs[configId];
                const int maxAttempts = 5;
                var validPosition = false;
                var position = Vector3.zero;

                while (!validPosition && attempts < maxAttempts)
                {
                    position = startPoint + _itemSpacing * spawnedIDs.Count * direction;
            
                    // 从高处发射射线
                    var rayStart = position + Vector3.up * 1000f;
                    if (Physics.Raycast(rayStart, Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
                    {
                        position = hit.point + Vector3.up * _itemHeight;
                        if (IsPositionValid(position, itemPrefab.GetComponent<Collider>()) && 
                            IsWithinBoundary(position))
                        {
                            validPosition = true;
                        }
                    }
            
                    attempts++;
                    if (!validPosition && attempts < maxAttempts)
                    {
                        // 尝试新的方向
                        direction = GetRandomDirection();
                    }
                }

                if (validPosition)
                {
                    var gridPos = GetGridPosition(position);
                    if (_gridMap.TryGetValue(gridPos, out var grid))
                    {
                        var list = new List<int>(grid.itemIDs);
                        list.Add(configId);
                        _gridMap[gridPos] = new Grid(list);
                    }
            
                    spawnedIDs.Add(new ValueTuple<int, Vector3> {Item1 = configId, Item2 = position});
                }
            }

            return spawnedIDs;
        }
        
        private bool IsPositionValidWithoutItem(Vector3 position)
        {
            return IsPositionValid(position, null);
        }
    
        private Vector3 GetRandomStartPoint(float height)
        {
            var randomPos = _mapBoundDefiner.GetRandomPoint(IsPositionValidWithoutItem);
            return new Vector3(randomPos.x, randomPos.y + height, randomPos.z);
        }
    
        private Vector3 GetRandomDirection()
        {
            return _mapBoundDefiner.GetRandomDirection();
        }

        private bool IsPositionValid(Vector3 position, Collider itemPrefab)
        {
            var gridPos = GetGridPosition(position);
            if (!_gridMap.TryGetValue(gridPos, out var grid))
                return false;

            // 检查网格内物品数量
            if (grid.itemIDs != null && grid.itemIDs.Length >= _maxGridItems)
                return false;

            // 从高处发射射线检查地面
            var rayStart = position + Vector3.up * 1000f;
            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
                return false;

            // 调整位置到地面上方
            position = hit.point + Vector3.up * _itemHeight;

            // 检查碰撞
            if (itemPrefab && itemPrefab is not MeshCollider)
            {
                int count;
                float checkRadius;

                if (itemPrefab is BoxCollider boxCollider)
                {
                    var checkSize = boxCollider.size * (0.5f * 1.1f); // 直接使用size更高效
                    count = Physics.OverlapBoxNonAlloc(
                        position, 
                        checkSize, 
                        _colliderBuffer,
                        boxCollider.transform.rotation, // 考虑旋转
                        _sceneLayer.value,
                        QueryTriggerInteraction.Ignore);
                }
                else if (itemPrefab is SphereCollider sphereCollider)
                {
                    checkRadius = sphereCollider.radius * 1.1f;
                    count = Physics.OverlapSphereNonAlloc(
                        position, 
                        checkRadius, 
                        _colliderBuffer, 
                        _sceneLayer.value,
                        QueryTriggerInteraction.Ignore);
                }
                else if (itemPrefab is CapsuleCollider capsuleCollider)
                {
                    checkRadius = capsuleCollider.radius * 1.1f;
                    var point2 = position + capsuleCollider.transform.TransformDirection(
                        capsuleCollider.direction switch {
                            0 => Vector3.right,
                            1 => Vector3.up,
                            2 => Vector3.forward,
                            _ => Vector3.up
                        }) * (capsuleCollider.height * 0.5f);

                    count = Physics.OverlapCapsuleNonAlloc(
                        position,
                        point2,
                        checkRadius,
                        _colliderBuffer,
                        _sceneLayer.value,
                        QueryTriggerInteraction.Ignore);
                }
                else
                {
                    Debug.LogError("Unsupported collider type");
                    return false;
                }

                // 有效性检查（注意：NonAlloc返回的是实际碰撞数）
                for (int i = 0; i < count; i++) 
                {
                    // 排除非障碍物（可通过tag二次过滤）
                    if ((_spawnedLayer.value & (1 << _colliderBuffer[i].gameObject.layer)) == 0) 
                        return false;
                }
                return count == 0;
            }

            return true;
        }

        private bool IsWithinBoundary(Vector3 position)
        {
            return _mapBoundDefiner.IsWithinMapBounds(position);
        
        }
        
        [Serializable]
        private struct Grid
        {
            public int[] itemIDs;
            public Grid(IEnumerable<int> ids)
            {
                itemIDs = ids.ToArray();
            }
        }
    }
}