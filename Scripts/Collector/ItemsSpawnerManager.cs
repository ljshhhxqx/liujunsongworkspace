using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AOTScripts.Data;
using AOTScripts.Tool.Coroutine;
using AOTScripts.Tool.Resource;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.UI.UIBase;
using MemoryPack;
using Mirror;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class ItemsSpawnerManager : NetworkHandlerBehaviour
    {
        private readonly Dictionary<int, CollectObjectController> _collectiblePrefabs = new Dictionary<int, CollectObjectController>();

        private readonly Dictionary<QualityType, Dictionary<PropertyTypeEnum, Material>> _collectibleMaterials =
            new Dictionary<QualityType, Dictionary<PropertyTypeEnum, Material>>();
        private readonly Dictionary<QualityType, DroppedItem> _droppedItemPrefabs = new Dictionary<QualityType, DroppedItem>();
        private readonly Dictionary<QualityType, TreasureChestComponent> _treasureChestPrefabs = new Dictionary<QualityType, TreasureChestComponent>();
        private IConfigProvider _configProvider;
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
        private static readonly Vector2Int[] GridDirections =
        {
            new(1, 0),
            new(-1, 0),
            new(0, 1),
            new(0, -1),
            new(1, 1),
            new(-1, 1),
            new(1, -1),
            new(-1, -1),
        };

        private static int _onceSpawnCount;
        private static int _onceSpawnWeight;
        private Transform _spawnedParent;
        private Collider[] _colliderBuffer; // 复用缓冲区
        private UIManager _uiManager;
        private ConstantBuffConfig _constantBuffConfig;
        private RandomBuffConfig _randomBuffConfig;
        private ShopConfig _shopConfig;
        private ItemConfig _itemConfig;
        private GameEventManager _gameEventManager;
        private GameLoopController _gameLoopController;
        private GameSyncManager _gameSyncManager;
        private PlayerInGameManager _playerInGameManager;
        private NetworkGameObjectPoolManager _networkGameObjectPoolManager;
        
        // 服务器维护的核心数据
        private readonly SyncDictionary<uint, byte[]> _serverItemMap = new SyncDictionary<uint, byte[]>();
        private readonly SyncDictionary<uint, int> _serverItemBehaviour = new SyncDictionary<uint, int>();
        private readonly Dictionary<int, IColliderConfig> _colliderConfigs = new Dictionary<int, IColliderConfig>();
        private readonly HashSet<uint> _processedItems = new HashSet<uint>();
        private readonly Dictionary<uint, CollectObjectController> _clientCollectObjectControllers = new Dictionary<uint, CollectObjectController>();
        
        private readonly SyncDictionary<uint, AttackInfo> _attackInfoMap = new SyncDictionary<uint, AttackInfo>();
        private readonly SyncDictionary<uint, MoveInfo> _moveInfoMap = new SyncDictionary<uint, MoveInfo>();
        private readonly SyncDictionary<uint, HiddenItemData> _hiddenInfoMap = new SyncDictionary<uint, HiddenItemData>();
        
        private CollectItemMetaData _serverTreasureChestMetaData;
        private readonly Dictionary<QualityType, IColliderConfig> _chestColliderConfigs = new Dictionary<QualityType, IColliderConfig>();
        private readonly Dictionary<QualityType, IColliderConfig> _droppedItemColliderConfigs = new Dictionary<QualityType, IColliderConfig> ();
        private TreasureChestComponent _clientTreasureChest;

        public Shader EffectShader { get; private set; }

        [Inject]
        private void Init(UIManager uiManager, IConfigProvider configProvider, GameSyncManager gameSyncManager, PlayerInGameManager playerInGameManager,
            GameMapInjector gameMapInjector,GameEventManager gameEventManager, MessageCenter messageCenter,NetworkGameObjectPoolManager networkGameObjectPoolManager)
        {
            _uiManager = uiManager;
            _configProvider = configProvider;
            _jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _playerInGameManager = playerInGameManager;
            _gameMapInjector = gameMapInjector;
            _gameSyncManager = gameSyncManager;
            _itemConfig = _configProvider.GetConfig<ItemConfig>();
            _messageCenter = messageCenter;
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnGameSceneResourcesLoadedLoaded);
            _collectObjectDataConfig = _configProvider.GetConfig<CollectObjectDataConfig>();
            _constantBuffConfig = _configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = _configProvider.GetConfig<RandomBuffConfig>();
            _chestConfig = _configProvider.GetConfig<ChestDataConfig>();
            _shopConfig = _configProvider.GetConfig<ShopConfig>();
            _sceneLayer = _jsonDataConfig.GameConfig.groundSceneLayer;
            _spawnedParent = transform;
            _networkGameObjectPoolManager = networkGameObjectPoolManager;
            ReadWriteData();
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

        protected override void StartServer()
        {
            RepeatedTask.Instance.StartRepeatingTask(UpdateDynamicObjects, 0.1f);
        }
        
        protected override void StartClient()
        {
            RepeatedTask.Instance.StartRepeatingTask(UpdateDynamicObjects, 0.1f);
        }

        private void UpdateDynamicObjects()
        {
            GameObjectContainer.Instance.UpdateDynamicObjects(ServerHandler);
        }

        [Server]
        public void SpawnItemsByDroppedItems(byte[] droppedItemsBytes, Vector3 position)
        {
            if (!ServerHandler)
            {
                return;
            }
            var items = BoxingFreeSerializer.MemoryDeserialize<DroppedItemData[]>(droppedItemsBytes);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var go = _networkGameObjectPoolManager.Spawn(_droppedItemPrefabs[item.Quality].gameObject, position, Quaternion.identity
                );
                var droppedItem = go.GetComponent<DroppedItem>();
            }
        }
        
        [ClientRpc]
        public void SpawnItemsByDroppedItemsClientRpc(byte[] droppedItemsBytes, Vector3 position)
        {
            if (!ClientHandler)
            {
                return;
            }
            var items = BoxingFreeSerializer.MemoryDeserialize<DroppedItemData[]>(droppedItemsBytes);
            for (int i = 0; i < items.Length; i++)
            {
                var item = items[i];
                var go = _networkGameObjectPoolManager.Spawn(_droppedItemPrefabs[item.Quality].gameObject, position, Quaternion.identity
                );
                var droppedItem = go.GetComponent<DroppedItem>();
            }
        }

        [Server]
        public void PickerPickUpChest(uint pickerId, uint itemId)
        {
            if (ServerHandler)
            {
                Debug.Log($"[ItemSpawnerManager] Picker {pickerId} pick up chest {itemId}");
                var state = (ItemState)_serverTreasureChestMetaData.StateFlags;
                Debug.Log($"[ItemSpawnerManager] var state = (ItemState)_serverTreasureChestMetaData.StateFlags");
                if (state.HasAnyState(ItemState.IsInteracting) || state.HasAnyState(ItemState.IsInBag)) return;
                Debug.Log($"[ItemSpawnerManager] state = state.AddState(ItemState.IsInteracting)");
                state = state.AddState(ItemState.IsInteracting);
                Debug.Log($"[ItemSpawnerManager] _serverTreasureChestMetaData.StateFlags = (byte)state");
                _serverTreasureChestMetaData.StateFlags = (byte)state;
                Debug.Log($"if (itemId != _serverTreasureChestMetaData.ItemId)");
                if (itemId != _serverTreasureChestMetaData.ItemId)
                {
                    Debug.LogError($"Item id {itemId} is not match with treasure chest id {_serverTreasureChestMetaData.ItemId}");
                    state = state.RemoveState(ItemState.IsInteracting);
                    _serverTreasureChestMetaData.StateFlags = (byte)state;
                    return;
                }
                Debug.Log($"[ItemSpawnerManager] var chestData = _serverTreasureChestMetaData.GetCustomData<ChestItemCustomData>()");
                var chestData = _serverTreasureChestMetaData.GetCustomData<ChestItemCustomData>();
                Debug.Log($"[ItemSpawnerManager] var chestCollider = _chestColliderConfigs.GetValueOrDefault(chestData.Quality)");
                var chestPos = _serverTreasureChestMetaData.Position.ToVector3();
                Debug.Log($"[ItemSpawnerManager] var chestPos = _serverTreasureChestMetaData.Position.ToVector3()");
                var player = NetworkServer.spawned[pickerId];
                Debug.Log($"[ItemSpawnerManager] var player = NetworkServer.spawned[pickerId]");
                var connectionId = _playerInGameManager.GetPlayerId(player.netId);
                if (!player)
                {
                    Debug.LogError($"Player {pickerId} could not be found");
                    state = state.RemoveState(ItemState.IsInteracting);
                    _serverTreasureChestMetaData.StateFlags = (byte)state;
                    return;
                }
                var playerCollider = _playerInGameManager.PlayerPhysicsData;

                var chestCollider = _chestColliderConfigs.GetValueOrDefault(chestData.Quality);

                try
                {
                    if (ValidatePickup(chestPos, player.transform.position, chestCollider, playerCollider))
                    {
                         var configData = _chestConfig.GetChestConfigData(chestData.ChestConfigId);
                         var extraItems = _shopConfig.GetItemsByShopId(chestData.ShopIds);
                         extraItems.UnionWith(configData.itemIds);
                         var list = new MemoryList<ItemsCommandData>();
                         foreach (var extraItem in extraItems)
                         {
                             var commonData = new ItemsCommandData
                             {
                                 ItemConfigId = extraItem,
                                 Count = 1,
                                 ItemUniqueId = new int[]
                                     { HybridIdGenerator.GenerateItemId(extraItem, GameSyncManager.CurrentTick) }
                             };
                             list.Add(commonData);
                         }
                         _gameSyncManager.EnqueueServerCommand(new ItemsGetCommand
                         {
                             Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item),
                             Items = list,
                         });
                         StringBuilder sb = new StringBuilder();
                         sb.AppendLine($"Player {player.name} pick up chest {chestData.ChestConfigId}");
                         foreach (var item in extraItems)
                         {
                             var itemConfig = _itemConfig.GetGameItemData(item);
                             sb.AppendLine($"Get item config id {item}-{itemConfig.name}-{itemConfig.desc}");
                         }
                         Debug.Log(sb.ToString());
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
            
        }

        [ClientRpc]
        private void RpcPickupChest(uint pickerId, uint itemId)
        {
            if (ClientHandler)
            {
                if (NetworkClient.spawned.TryGetValue(itemId, out var item))
                {
                    var component = item.GetComponent<TreasureChestComponent>();
                    component.PickUpSuccess().Forget();
                }

                if (NetworkClient.spawned.TryGetValue(pickerId, out var player))
                {
                    var picker = player.GetComponent<Picker>();
                    picker.AddPicked(itemId);
                }
            }
        }

        [Server]
        public void PickerPickupItem(uint pickerId, uint itemId, bool killed)
        {
            if (ServerHandler)
            {
                HandleItemPickup(itemId, pickerId, killed);
                JudgeEndRound();
            }
        }

        private void OnGameStart(string sceneName)
        {
            _gridMap.Clear();
            ObjectInjectProvider.Instance.Inject(MapBoundDefiner.Instance);
            _gridMap = MapBoundDefiner.Instance.GridMap.Distinct().ToDictionary(x => x,_ => new Grid(new HashSet<int>()));
            var res = ResourceManager.Instance.GetMapCollectObject($"Town");
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
                    var matList = ResourceManager.Instance.GetMapCollectObjectMaterial("Town", qualityType.ToString());
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
            
            GetShader().Forget();
        }

        private async UniTask GetShader()
        {
            var data = DataJsonManager.Instance.GetResourceData("DisintegrationShader");
            EffectShader = await ResourceManager.Instance.LoadResourceAsync<Shader>(data);
        }

        private void OnDestroy()
        {
            MapBoundDefiner.Instance.Clear();
            GameObjectContainer.Instance.ClearObjects();
            RepeatedTask.Instance.StopRepeatingTask(UpdateDynamicObjects);
        }

        [Server]
        private void HandleItemPickup(uint itemId, uint pickerId, bool killed)
        {
            if (!ServerHandler)
            {
                return;
            }

            try
            {
                Debug.Log($"Handling item pickup with id: {itemId} by picker: {pickerId}");
                if (_serverItemMap.TryGetValue(itemId, out var itemInfo))
                {
                    if (!_processedItems.Add(itemId))
                    {
                        Debug.Log($"Item with id: {itemId} has been processed");
                        return;
                    }

                    if (!NetworkServer.spawned.TryGetValue(itemId, out var item))
                    {
                        Debug.LogError($"Item with id: {itemId} has not been spawned or is destroyed");
                        return;
                    }

                    if (!NetworkServer.spawned.TryGetValue(pickerId, out var player))
                    {
                        Debug.LogError($"Picker with id: {pickerId} has not been spawned or is destroyed");
                        return;
                    }

                    var itemData = BoxingFreeSerializer.MemoryDeserialize<CollectItemMetaData>(itemInfo);
                    var itemColliderData = _colliderConfigs.GetValueOrDefault(itemData.ItemCollectConfigId);
                    var itemConfigId = _collectObjectDataConfig.GetCollectObjectData(itemData.ItemCollectConfigId)
                        .itemId;
                    var itemPos = item.transform.position;
                    var playerConnectionId = _playerInGameManager.GetPlayerId(pickerId);
                    var playerColliderConfig = _playerInGameManager.PlayerPhysicsData;
                    if (!player)
                    {
                        Debug.LogError($"Cannot find player with netId: {pickerId}");
                        return;
                    }

                    // 验证位置和碰撞
                    if (ValidatePickup(itemPos, player.transform.position, itemColliderData,
                            playerColliderConfig))
                    {
                        var list = new MemoryList<ItemsCommandData>(2);
                        list.Add(new ItemsCommandData()
                        {
                            ItemConfigId =  itemConfigId,
                            Count = 1,
                            ItemUniqueId = new int[]
                                { HybridIdGenerator.GenerateItemId(itemConfigId, GameSyncManager.CurrentTick) }
                        });
                        var getCount = killed ? Random.Range(2, 4) : 1;
                        for (int i = 0; i < getCount; i++)
                        {
                            // 处理拾取逻辑
                            _gameSyncManager.EnqueueServerCommand(new ItemsGetCommand
                            {
                                Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Item),
                                Items = list,
                            });
                        }
                        var identity = NetworkServer.spawned[itemId];

                        var collectObjectController = identity.GetComponent<CollectObjectController>();
                        _networkGameObjectPoolManager.Despawn(identity.gameObject);
                        var picker = player.GetComponent<Picker>();
                        picker.RpcPlayEffect((int)collectObjectController.CollectObjectData.collectObjectClass);
                        //collectObjectController.RpcRecycleItem();
                        Debug.Log(
                            $"Recycle item with id: {itemId} itemConfigid {collectObjectController.CollectConfigId}");
                        //NetworkServer.Destroy(NetworkServer.spawned[itemData.ItemId].gameObject);
                        // 通知客户端
                        RpcPickupItem(itemId, pickerId, (int)collectObjectController.CollectObjectData.collectObjectClass);

                        _processedItems.Remove(itemId);
                        _serverItemMap.Remove(itemId);
                        _serverItemBehaviour.Remove(itemId);
                        //Debug.Log($"Player {player.name} pick up item {itemId}");
                    }
                    else
                    {
                        Debug.Log($"Player {player.name} cannot pick up item {itemId}");
                    }
                }
                // else
                // {
                //     var sb = new StringBuilder();
                //     sb.AppendLine($"Cannot find item with id: {itemId}");
                //     foreach (var item in _serverItemMap.Keys)
                //         sb.AppendLine($"Existing item id: {item}");
                //     Debug.LogError(sb.ToString());
                // }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                _processedItems.Clear();
            }
        }

        [ClientRpc]
        private void RpcPickupItem(uint itemId, uint pickerId, int itemClass)
        {
            if (ClientHandler)
            {
                if (NetworkClient.spawned.TryGetValue(pickerId, out var player))
                {
                    var picker = player.GetComponent<Picker>();
                    picker.AddPicked(itemId);
                }
            }
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
                _gameLoopController ??= FindObjectOfType<GameLoopController>();
                _gameLoopController.IsEndRound = true;
            }
        }

        [Server]
        public async UniTask EndRound()  
        {
            if (!ServerHandler)
            {
                return;
            }
            try
            {
                Debug.Log("Starting EndRound");
                if (NetworkServer.spawned.TryGetValue(_serverTreasureChestMetaData.ItemId, out var chest))
                {
                    _networkGameObjectPoolManager.Despawn(chest.gameObject);
                    Debug.Log($"Recycle chest with id: {_serverTreasureChestMetaData.ItemId}");
                }
                // 清理网格数据
                
                foreach (var vector2Int in _gridMap.Keys)
                {
                    _gridMap[vector2Int].ItemIDs.Clear();
                }

                foreach (var itemId in _serverItemMap.Keys)
                {
                    if (NetworkServer.spawned.TryGetValue(itemId, out var item))
                    {
                        var controller = item.GetComponent<CollectObjectController>();
                        _networkGameObjectPoolManager.Despawn(item.gameObject);
                        Debug.Log($"Recycle item with id: {itemId} itemConfigid {controller.CollectConfigId}");
                    }
                    else
                    {
                        Debug.LogError($"Item with id: {itemId} is not found in spawned objects");
                    }
                    await UniTask.Yield();
                }

                // 通知客户端清理
                //RpcEndRound();
                _serverItemMap.Clear();
                _serverItemBehaviour.Clear();
                Debug.Log("EndRound finished on server");
                await UniTask.Yield();
                
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in EndRound: {e}");
            }
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

        public AttackInfo GetAttackInfo(uint itemId)
        {
            if (_attackInfoMap.TryGetValue(itemId, out var attackInfo))
            {
                return attackInfo;
            }
            Debug.LogError($"No prefab found for itemId: {itemId} in attackInfoMap");
            return default;
        }

        public MoveInfo GetMoveInfo(uint itemId)
        {
            if (_moveInfoMap.TryGetValue(itemId, out var moveInfo))
            {
                return moveInfo;
            }
            Debug.LogError($"No prefab found for itemId: {itemId} in moveInfoMap");
            return default;
        }

        public HiddenItemData GetHiddenItemData(uint itemId)
        {
            if (_hiddenInfoMap.TryGetValue(itemId, out var hiddenItemData))
            {
                return hiddenItemData;
            }
            Debug.LogError($"No prefab found for itemId: {itemId} in hiddenInfoMap");
            return default;
        }

        private AttackInfo GetRandomAttackInfo()
        {
            var attackInfo = new AttackInfo();
            attackInfo.health = _jsonDataConfig.CollectData.healthRange.GetRandomValue();
            attackInfo.damage = _jsonDataConfig.CollectData.attackPowerRange.GetRandomValue();
            attackInfo.speed = _jsonDataConfig.CollectData.bulletSpeedRange.GetRandomValue();
            attackInfo.isRemoteAttack = Random.Range(0,3f) < 1f;
            attackInfo.criticalRate = attackInfo.isRemoteAttack ? _jsonDataConfig.CollectData.criticalRateRange.GetRandomByWeight(0.2f) : _jsonDataConfig.CollectData.criticalRateRange.GetRandomByWeight(0.7f);
            attackInfo.criticalDamage =attackInfo.isRemoteAttack ?  _jsonDataConfig.CollectData.criticalDamageRatioRange.GetRandomByWeight(0.2f) : _jsonDataConfig.CollectData.criticalDamageRatioRange.GetRandomByWeight(0.7f);
            attackInfo.attackRange = attackInfo.isRemoteAttack ? _jsonDataConfig.CollectData.attackRange.GetRandomByWeight(0.7f) : _jsonDataConfig.CollectData.attackRange.GetRandomByWeight(0.2f);
            attackInfo.attackCooldown = attackInfo.isRemoteAttack ? _jsonDataConfig.CollectData.attackCooldown.GetRandomByWeight(0.7f) : _jsonDataConfig.CollectData.attackCooldown.GetRandomByWeight(0.2f);
            attackInfo.lifeTime = _jsonDataConfig.CollectData.lifeTimeRange.GetRandomValue();
            attackInfo.defense = _jsonDataConfig.CollectData.defenseRange.GetRandomValue();
            return attackInfo;
        }
        
        private MoveInfo GetRandomMoveInfo(Vector3 position)
        {
            var dir = GetRandomDirection();
            var moveType = Random.Range((int)MoveType.Bounced, (int)MoveType.Periodic + 1);
            var moveInfo = new MoveInfo();
            moveInfo.patternAmplitude = _jsonDataConfig.CollectData.patternAmplitudeRange.GetRandomValue();
            moveInfo.patternFrequency = _jsonDataConfig.CollectData.patternFrequencyRange.GetRandomValue();
            moveInfo.rotateSpeed = _jsonDataConfig.CollectData.rotateSpeedRange.GetRandomValue();
            moveInfo.speed = _jsonDataConfig.CollectData.speedRange.GetRandomValue();
            moveInfo.TargetPosition = position + dir * Random.Range(3, 5);
            switch ((MoveType)moveType)
            {
                case MoveType.Bounced:
                    moveInfo.bouncingConfig = _jsonDataConfig.CollectData.commonBouncingMovementConfig;
                    break;
                case MoveType.Periodic:
                    moveInfo.periodicConfig = _jsonDataConfig.CollectData.commonPeriodicMovementConfig;
                    break;
                case MoveType.Evasive:
                    moveInfo.evasiveConfig = _jsonDataConfig.CollectData.commonEvasiveMovementConfig;
                    break;
            }
            return moveInfo;
        }

        private HiddenItemData GetRandomHiddenItemData()
        {
            var hiddenItemData = new HiddenItemData();
            hiddenItemData.hideType = (HideType)Random.Range(0, 4);
            hiddenItemData.translucence = _jsonDataConfig.CollectData.translucenceRange.GetRandomValue();
            hiddenItemData.mysteryTime = _jsonDataConfig.CollectData.mysteryTimeRange.GetRandomValue();
            hiddenItemData.translucenceTime = _jsonDataConfig.CollectData.translucenceTimeRange.GetRandomValue();
            return hiddenItemData;
        }

        [Server]
        public async UniTask SpawnManyItems()
        {
            if (!ServerHandler)
            {
                return;
            }
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
                        var type = _jsonDataConfig.GetCollectObjectType();
                        var go = _networkGameObjectPoolManager.Spawn(_collectiblePrefabs[item.Item1].gameObject, item.Item2, Quaternion.identity, null,
                            poolSize: newSpawnInfos.Count);
                        var identity = go.GetComponent<NetworkIdentity>();
                        _serverItemBehaviour.Add(identity.netId, (int)type);
                        switch (type)
                        {
                            case CollectObjectType.Attack:
                                var attackInfo = GetRandomAttackInfo();
                                _attackInfoMap.Add(identity.netId, attackInfo);
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2, new SceneItemInfo
                                {
                                    health = attackInfo.health,
                                    attackDamage = attackInfo.damage,
                                    defense = attackInfo.defense,
                                    sceneItemId = identity.netId,
                                    attackRange = attackInfo.attackRange,
                                    attackInterval = attackInfo.attackCooldown,
                                    maxHealth = attackInfo.health,
                                    Position = item.Item2,
                                    criticalRate = attackInfo.criticalRate,
                                    criticalDamageRatio = attackInfo.criticalDamage
                                }));
                                break;
                            case CollectObjectType.Move:
                                var moveInfo = GetRandomMoveInfo(item.Item2);
                                _moveInfoMap.Add(identity.netId, moveInfo);
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2, new SceneItemInfo
                                {
                                    health = 1,
                                    sceneItemId = identity.netId,
                                    speed = moveInfo.speed,
                                    Position = item.Item2,
                                }));
                                break;
                            case CollectObjectType.Hidden:
                                var hiddenItemData = GetRandomHiddenItemData();
                                _hiddenInfoMap.Add(identity.netId, hiddenItemData);
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2, new SceneItemInfo
                                {
                                    health = 1,
                                    sceneItemId = identity.netId,
                                    Position = item.Item2
                                }));
                                break;
                            case CollectObjectType.AttackMove:
                                attackInfo = GetRandomAttackInfo();
                                moveInfo = GetRandomMoveInfo(item.Item2);
                                _attackInfoMap.Add(identity.netId, attackInfo);
                                _moveInfoMap.Add(identity.netId, moveInfo);
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2, new SceneItemInfo
                                {
                                    health = attackInfo.health,
                                    attackDamage = attackInfo.damage,
                                    defense = attackInfo.defense,
                                    sceneItemId = identity.netId,
                                    attackRange = attackInfo.attackRange,
                                    attackInterval = attackInfo.attackCooldown,
                                    maxHealth = attackInfo.health,
                                    speed = moveInfo.speed,
                                    Position = item.Item2,
                                    criticalRate = attackInfo.criticalRate,
                                    criticalDamageRatio = attackInfo.criticalDamage
                                }));
                                break;
                            case CollectObjectType.AttackHidden:
                                hiddenItemData = GetRandomHiddenItemData();
                                attackInfo = GetRandomAttackInfo();
                                _attackInfoMap.Add(identity.netId, attackInfo);
                                _hiddenInfoMap.Add(identity.netId, hiddenItemData);
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2, new SceneItemInfo
                                {
                                    health = attackInfo.health,
                                    attackDamage = attackInfo.damage,
                                    defense = attackInfo.defense,
                                    sceneItemId = identity.netId,
                                    attackRange = attackInfo.attackRange,
                                    attackInterval = attackInfo.attackCooldown,
                                    maxHealth = attackInfo.health,
                                    Position = item.Item2,
                                    criticalRate = attackInfo.criticalRate,
                                    criticalDamageRatio = attackInfo.criticalDamage
                                }));
                                break;
                            case CollectObjectType.MoveHidden:
                                moveInfo = GetRandomMoveInfo(item.Item2);
                                hiddenItemData = GetRandomHiddenItemData();
                                _moveInfoMap.Add(identity.netId, moveInfo);
                                _hiddenInfoMap.Add(identity.netId, hiddenItemData);
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2, new SceneItemInfo
                                {
                                    sceneItemId = identity.netId,
                                    speed = moveInfo.speed,
                                    Position = item.Item2,
                                }));
                                break;
                            case CollectObjectType.AttackMoveHidden:
                                hiddenItemData = GetRandomHiddenItemData();
                                attackInfo = GetRandomAttackInfo();
                                moveInfo = GetRandomMoveInfo(item.Item2);
                                _attackInfoMap.Add(identity.netId, attackInfo);
                                _moveInfoMap.Add(identity.netId, moveInfo);
                                _hiddenInfoMap.Add(identity.netId, hiddenItemData);
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2,
                                    new SceneItemInfo
                                    {

                                        health = attackInfo.health,
                                        attackDamage = attackInfo.damage,
                                        defense = attackInfo.defense,
                                        sceneItemId = identity.netId,
                                        attackRange = attackInfo.attackRange,
                                        attackInterval = attackInfo.attackCooldown,
                                        maxHealth = attackInfo.health,
                                        speed = moveInfo.speed,
                                        Position = item.Item2,
                                        criticalRate = attackInfo.criticalRate,
                                        criticalDamageRatio = attackInfo.criticalDamage
                                    }));
                                break;
                            default:
                                
                                _gameEventManager.Publish(new SceneItemInfoChanged(identity.netId, item.Item2,
                                    new SceneItemInfo
                                    {
                                        health = 1
                                    }));
                                break;
                        }
                        Debug.Log($"Get Object with id: {identity.netId} itemConfigid {item.Item1}");
                        
                        var buffData = GetBuffExtraData(item.Item1);
                        var buff = _randomBuffConfig.GetRandomBuffData(buffData.buffId);
                        var extraData = new CollectItemCustomData
                        {
                            RandomBuffId = buff.buffId,
                            ItemUniqueId = HybridIdGenerator.GenerateItemId(_collectObjectDataConfig.GetItemId(item.Item1), GameSyncManager.CurrentTick),
                        };
                        var state = (ItemState)0;
                        state = state.AddState(ItemState.IsActive);
                        var itemMetaData = new CollectItemMetaData(identity.netId, 
                            item.Item2,
                            (byte)state, 
                            item.Item1,
                            (uint)Mathf.Abs(GameSyncManager.CurrentTick),
                            30, 
                            (ushort)Random.Range(0, 65535),
                            -1);
                        itemMetaData = itemMetaData.SetCustomData(extraData);
                        var itemInfo = MemoryPackSerializer.Serialize(itemMetaData);
                        Debug.Log($"[SpawnManyItems] Adding item to map with id: {identity.netId} itemConfigid {item.Item1}");
                        _serverItemMap.Add(identity.netId, itemInfo);
                        await UniTask.Yield();
                    }
                    //Debug.Log($"Calculated {spawnedCount} spawn positions");
                }

                // foreach (var iKey in _serverItemMap.Keys)
                // {
                //     var itemInfo = _serverItemMap[iKey];
                //     SpawnManyItemsClientRpc(itemInfo);
                // }
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
        private JsonSerializerSettings _jsonSerializerSettings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Auto, // 改为 Auto
            Converters = new List<JsonConverter> 
            { 
                new StringEnumConverter() // 支持枚举字符串
            }
        };

        [Server]
        public void SpawnTreasureChestServer()
        {
            if (!ServerHandler)
            {
                return;
            }
            var random = Random.Range(0f, 1f);
            var chestData = _chestConfig.RandomOne(random);
            var position = GetRandomStartPoint(0.5f);
            var type = _jsonDataConfig.GetCollectObjectType();
            var randomItems = JsonConvert.DeserializeObject<RandomItemsData>(chestData.randomItems, _jsonSerializerSettings); //chestData.randomItems;
            if (!_treasureChestPrefabs.TryGetValue(randomItems.quality, out var value))
            {
                Debug.LogWarning($"TreasureChestPrefabs not found for quality {randomItems.quality}");
                return;
            }
            var chestGo = _networkGameObjectPoolManager.Spawn(_treasureChestPrefabs.GetValueOrDefault(randomItems.quality).gameObject, position,
                Quaternion.identity, onSpawn: (identity) =>
                {
                    var controller = identity.GetComponent<TreasureChestComponent>();
                    controller.BehaviourType = (int)type;
                });            
            Debug.Log($"[SpawnTreasureChestServer] var identity = chestGo.GetComponent<NetworkIdentity>()");

            var identity = chestGo.GetComponent<NetworkIdentity>();
            // if (identity.netId == 0 || !NetworkServer.spawned.TryGetValue(identity.netId, out var itemInfo))
            // {
            //     NetworkServer.Spawn(chestGo);
            //     itemInfo = chestGo.GetComponent<NetworkIdentity>();
            // }
            Debug.Log($"Spawning treasure chest at position: {position} with id: {identity.netId}");
            //chestGo.transform.position = position;
            var metaData = new CollectItemMetaData(identity.netId,
                position,
                0,
                chestData.chestId,
                (uint)Mathf.Abs(GameSyncManager.CurrentTick),
                30,
                (ushort)Random.Range(0, 65535),
                -1);
            var qualityItems = RandomItemsData.GenerateQualityItems(randomItems, random);
            var shopIds = _shopConfig.GetQualityItems(qualityItems, random);
            var chestUniqueId = HybridIdGenerator.GenerateChestId(chestData.chestId, GameSyncManager.CurrentTick);
            metaData = metaData.SetCustomData(new ChestItemCustomData
            {
                ChestConfigId = chestData.chestId,
                ShopIds = shopIds.ToArray(),
                ChestUniqueId = chestUniqueId,
                Quality = randomItems.quality,
            });
            //var serverTreasureChestMetaDataBytes = MemoryPackSerializer.Serialize(metaData);
            _serverTreasureChestMetaData = metaData;
            //RpcSpawnTreasureChest(serverTreasureChestMetaDataBytes);
            GameItemManager.AddChestData(new GameChestData
            {
                ChestId = chestUniqueId,
                ChestConfigId = chestData.chestId,
            }, netIdentity);
        }

        private void InitializeGrid()
        {
            for (var x = MapBoundDefiner.Instance.MapMinBoundary.x; x <= MapBoundDefiner.Instance.MapMaxBoundary.x; x += _gridSize)
            {
                for (var z = MapBoundDefiner.Instance.MapMinBoundary.z; z <= MapBoundDefiner.Instance.MapMaxBoundary.z; z += _gridSize)
                {
                    var gridPos = GetGridPosition(new Vector3(x, 0, z));
                    _gridMap[gridPos] = new Grid(new HashSet<int>());
                }
            }
        }

        private Vector2Int GetGridPosition(Vector3 position)
        {
            var x = Mathf.FloorToInt(position.x / _gridSize);
            var z = Mathf.FloorToInt(position.z / _gridSize);
            return new Vector2Int(x, z);
        }

        private List<(int, Vector3, Vector2Int)> SpawnItems(int totalWeight, int spawnMode)
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

            return PlaceItems(collectTypes).Shuffle() as List<(int, Vector3, Vector2Int)>;
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
            Debug.Log($"ValidatePickup: {item} {player}");
            return GamePhysicsSystem.CheckIntersectsWithMargin(player, item, playerColliderConfig, itemColliderConfig);
        }
        
        private List<(int configId, Vector3 pos, Vector2Int grid)> _callbackBuffer
            = new List<(int, Vector3, Vector2Int grid)>();
        private ValueTuple<int, Vector3> _itemBuffer;

        
        public Vector3 WorldPosFromGrid(Vector2Int grid)
        {
            var gridOrigin = MapBoundDefiner.Instance.GridOrigin;
            float x = gridOrigin.x + grid.x * _gridSize + _gridSize * 0.5f;
            float z = gridOrigin.z + grid.y * _gridSize + _gridSize * 0.5f;

            float y = MapBoundDefiner.Instance.GetGroundHeight(grid);
            return new Vector3(x, y + _itemHeight, z);
        }
        
        private bool TrySampleGrid(
            Vector2Int origin,
            int stride,
            out Vector2Int grid)
        {
            var dir = GridDirections[Random.Range(0, GridDirections.Length)];
            var step = Random.Range(1, 4); // 给点随机性，避免死线

            grid = origin + dir * (step * stride);
            return true;
        }
        
        private bool ValidatePlacement(
            Vector2Int grid,
            int configId,
            out Vector3 worldPos)
        {
            worldPos = default;

            // 1. Grid 是否存在
            if (!_gridMap.ContainsKey(grid))
                return false;

            // 2. Grid 是否被占
            if (_gridMap[grid].ItemIDs.Count > 0)
                return false;

            // 3. 转世界坐标
            worldPos = WorldPosFromGrid(grid);

            // 4. Map 边界
            if (!MapBoundDefiner.Instance.IsWithinMapBounds(worldPos))
                return false;
            var c = _colliderConfigs[configId];

            if (!GameObjectContainer.Instance.CanSpawnAt(worldPos, c))
                return false;
            if (IntersectsPending(worldPos, c))
                return false;

            return true;
        }

        
        private void CommitPlacement(
            List<(int configId, Vector3 pos, Vector2Int grid)> buffer)
        {
            foreach (var item in buffer)
            {
                _gridMap[item.grid].ItemIDs.Add(item.configId);
            }
        }
        
        private bool IntersectsPending(
            Vector3 pos,
            IColliderConfig c)
        {
            foreach (var item in _callbackBuffer)
            {
                if (GamePhysicsSystem.CheckIntersectsWithMargin(
                        pos,
                        item.pos,
                        c,
                        _colliderConfigs[item.configId],
                        0.0f))
                {
                    return true;
                }
            }
            return false;
        }


        private List<(int configId, Vector3 pos, Vector2Int grid)> PlaceItems(List<int> items)
        {
            _callbackBuffer.Clear();

            // === 1. 起始点（保持你原始语义：随机一个合法起点） ===
            if (!MapBoundDefiner.Instance.TryGetRandomSpawnPoint(out var startGrid))
                return _callbackBuffer;

            // 起始世界坐标（Grid → World，只用于起点）
            var startPos = WorldPosFromGrid(startGrid);

            // 随机方向（和你原来一致）
            var dir = GridDirections[Random.Range(0, GridDirections.Length)].ToVector3XZ();

            int placed = 0;
            int safety = 0;

            // === 2. 顺序推进（这是你原来“不会放不满”的关键） ===
            while (placed < items.Count && safety < items.Count * 20)
            {
                safety++;

                var configId = items[placed];

                // ⚠️ 关键：仍然用 spacing 推进（不是 grid stride）
                var candidatePos =
                    startPos + dir * (_itemSpacing * placed);

                // === 3. Raycast 到地面 / 静态物体（原逻辑） ===
                if (Physics.Raycast(
                        candidatePos + Vector3.up * 50f,
                        Vector3.down,
                        out var hit,
                        100f,
                        _sceneLayer))
                {
                    candidatePos = hit.point + Vector3.up * _itemHeight;
                }
                else
                {
                    continue; // 原来也是失败就跳
                }

                // === 4. Map 边界（原逻辑） ===
                if (!MapBoundDefiner.Instance.IsWithinMapBounds(candidatePos))
                    continue;

                // === 5. Grid 快速过滤（⚡ 新增，但只做 early reject） ===
                var grid = MapBoundDefiner.Instance.GetGridPosition(candidatePos);

                if (!_gridMap.ContainsKey(grid))
                    continue;

                if (_gridMap[grid].ItemIDs.Count > 0)
                    continue;

                // === 6. 精确碰撞（原接口，语义不变） ===
                if (!GameObjectContainer.Instance.CanSpawnAt(
                        candidatePos,
                        _colliderConfigs[configId]))
                    continue;

                // === 7. 接受这个点 ===
                _callbackBuffer.Add((configId, candidatePos, grid));
                placed++;
            }

            // === 8. 一次性提交占用（⚡ 优化点） ===
            foreach (var item in _callbackBuffer)
            {
                _gridMap[item.grid].ItemIDs.Add(item.configId);
            }

            return _callbackBuffer;
        }



        // private List<(int, Vector3)> PlaceItems(List<int> itemsToSpawn)
        // {
        //     _callbackBuffer.Clear();
        //     var startPoint = GetRandomStartPoint(_itemHeight);
        //     if (startPoint == Vector3.zero)
        //     {
        //         Debug.LogWarning("Failed to find valid start point");
        //         return _callbackBuffer;
        //     }
        //
        //     var direction = GetRandomDirection();
        //
        //     for (var i = 0; i < itemsToSpawn.Count; i++)
        //     {
        //         var configId = itemsToSpawn[i];
        //         var attempts = 0;
        //         var validPosition = false;
        //         var position = Vector3.zero;
        //
        //         while (!validPosition && attempts < maxAttempts)
        //         {
        //             position = startPoint + _itemSpacing * _callbackBuffer.Count * direction;
        //     
        //             // 从高处发射射线
        //             var rayStart = position + Vector3.up * 50f;
        //             if (Physics.Raycast(rayStart, Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
        //             {
        //                 position = hit.point + Vector3.up * _itemHeight;
        //                 if (IsPositionValid(position, configId) && 
        //                     IsWithinBoundary(position))
        //                 {
        //                     validPosition = true;
        //                 }
        //             }
        //     
        //             attempts++;
        //             if (!validPosition && attempts < maxAttempts)
        //             {
        //                 // 尝试新的方向
        //                 direction = GetRandomDirection();
        //             }
        //         }
        //
        //         if (validPosition)
        //         {
        //             var gridPos = GetGridPosition(position);
        //             if (_gridMap.TryGetValue(gridPos, out var grid))
        //             {
        //                 var hashSet = grid.ItemIDs;
        //                 hashSet.Add(configId);
        //                 _gridMap[gridPos] = new Grid(hashSet);
        //             }
        //     
        //             _itemBuffer = (configId, position);
        //             _callbackBuffer.Add(_itemBuffer);
        //         }
        //     }
        //
        //     return _callbackBuffer;
        // }
        
        private bool IsPositionValidWithoutItem(Vector3 position)
        {
            return IsPositionValid(position, 0);
        }
    
        private Vector3 GetRandomStartPoint(float height)
        {
            var randomPos = MapBoundDefiner.Instance.GetRandomPoint(IsPositionValidWithoutItem);
            return new Vector3(randomPos.x, randomPos.y + height, randomPos.z);
        }
    
        private Vector3 GetRandomDirection()
        {
            return MapBoundDefiner.Instance.GetRandomDirection();
        }

        private readonly HashSet<GameObjectData> _cache = new HashSet<GameObjectData>();

        private bool IsPositionValid(Vector3 position, int configId)
        {
            var gridPos = GetGridPosition(position);
            if (!_gridMap.TryGetValue(gridPos, out var grid))
                return false;

            // 检查网格内物品数量
            if (grid.ItemIDs != null && grid.ItemIDs.Count >= _maxGridItems)
                return false;

            // 从高处发射射线检查地面
            var rayStart = position + Vector3.up * 50f;
            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, 60f, _sceneLayer))
                return false;

            // 调整位置到地面上方
            position = hit.point + Vector3.up * _itemHeight;
                
            if (_colliderConfigs.TryGetValue(configId, out var colliderConfig))
            {
                if (GameObjectContainer.Instance.IsIntersect(position, colliderConfig, _cache))
                {
                    foreach (var hashSet in _cache)
                    {
                        if (hashSet.Layer != _sceneLayer)
                        {
                            //Debug.LogWarning($"Game object with id | {hashSet.Id} | layer is not equal to scene layer");
                            return false;
                        }
                    }
                }
            }
            else
            {
                return false;
            }
            return _cache.Count == 0;
        }

        private bool IsWithinBoundary(Vector3 position)
        {
            return MapBoundDefiner.Instance.IsWithinMapBounds(position);
        
        }
        
        
        private class Grid
        {
            public HashSet<int> ItemIDs;
            public Grid(HashSet<int> ids)
            {
                ItemIDs = ids;
            }
        }

        private void ReadWriteData()
        {
            Reader<AttackInfo>.read = ReadAttackInfoData;
            Writer<AttackInfo>.write = WriteAttackInfoData;
            Reader<MoveInfo>.read = ReadMoveInfo;
            Writer<MoveInfo>.write = WriteMoveInfo;
            Reader<HiddenItemData>.read = ReadHiddenItemData;
            Writer<HiddenItemData>.write = WriteHiddenItemData;
        }

        private AttackInfo ReadAttackInfoData(NetworkReader reader)
        {
            return new AttackInfo
            {
                health = reader.ReadFloat(),
                damage = reader.ReadFloat(),
                attackRange = reader.ReadFloat(),
                attackCooldown = reader.ReadFloat(),
                isRemoteAttack = reader.ReadBool(),
                speed = reader.ReadFloat(),
                lifeTime = reader.ReadFloat(),
                criticalRate = reader.ReadFloat(),
                criticalDamage = reader.ReadFloat(),
                defense = reader.ReadFloat()
            };
        }
        private void WriteAttackInfoData(NetworkWriter writer, AttackInfo data)
        {
            writer.Write(data.health);
            writer.Write(data.damage);
            writer.Write(data.attackRange);
            writer.Write(data.attackCooldown);
            writer.Write(data.isRemoteAttack);
            writer.Write(data.speed);
            writer.Write(data.lifeTime);
            writer.Write(data.criticalRate);
            writer.Write(data.criticalDamage);
            writer.Write(data.defense);
        }

        private MoveInfo ReadMoveInfo(NetworkReader reader)
        {
            return new MoveInfo
            {
                TargetPosition = reader.ReadVector3(),
                patternAmplitude = reader.ReadFloat(),
                patternFrequency = reader.ReadFloat(),
                speed = reader.ReadFloat(),
                rotateSpeed = reader.ReadFloat(),
                bouncingConfig = reader.Read<BouncingMovementConfig>(),
                evasiveConfig = reader.Read<EvasiveMovementConfig>(),
                periodicConfig = reader.Read<PeriodicMovementConfig>(),
            };
        }
        private void WriteMoveInfo(NetworkWriter writer, MoveInfo data)
        {
            writer.Write(data.TargetPosition);
            writer.Write(data.patternAmplitude);
            writer.Write(data.patternFrequency);
            writer.Write(data.speed);
            writer.Write(data.rotateSpeed);
            writer.Write(data.bouncingConfig);
            writer.Write(data.evasiveConfig);
            writer.Write(data.periodicConfig);
        }

        private HiddenItemData ReadHiddenItemData(NetworkReader reader)
        {
            return new HiddenItemData
            {
                hideType = (HideType)reader.ReadByte(),
                translucence = reader.ReadFloat(),
                mysteryTime = reader.ReadFloat(),
                translucenceTime = reader.ReadFloat(),
            };
        }
        private void WriteHiddenItemData(NetworkWriter writer, HiddenItemData data)
        {
            writer.Write((byte)data.hideType);
            writer.Write(data.translucence);
            writer.Write(data.mysteryTime);
            writer.Write(data.translucenceTime);
        }

        public CollectObjectType GetCollectObjectType(uint id)
        {
            return (CollectObjectType)_serverItemBehaviour.FirstOrDefault(x => x.Key == id).Value;
        }
    }
    
}