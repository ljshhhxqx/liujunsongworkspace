using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.ObjectPool;
using Config;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Game;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.Collect;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.UI.UIs.Overlay;
using Mirror;
using Tool.GameEvent;
using Tool.Message;
using UI.UIBase;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class ItemsSpawnerManager : ServerNetworkComponent
    {
        [SyncVar]
        private int _currentId;
        [SyncVar]
        private TreasureChestInfo _treasureChestInfo;
        public struct Grid
        {
            public List<CollectibleItemData> itemIDs;
        }
        private Dictionary<CollectType, CollectibleItemData> _collectiblePrefabs = new Dictionary<CollectType, CollectibleItemData>();
        private TreasureChestComponent _treasureChestPrefab;
        private TreasureChestComponent _treasureChest;
        private IConfigProvider _configProvider;
        private MapBoundDefiner _mapBoundDefiner;
        private ChestDataConfig _chestConfig;
        private MessageCenter _messageCenter;
        private GameMapInjector _gameMapInjector;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private LayerMask _sceneLayer;
        private Dictionary<Vector2Int, Grid> _gridMap = new Dictionary<Vector2Int, Grid>();
        private SyncDictionary<int, SpawnItemInfo> _spawnedItems = new SyncDictionary<int, SpawnItemInfo>();
        private static float _itemSpacing;
        private static int _maxGridItems;
        private static float _itemHeight;
        private static float _gridSize; // Size of each grid cell
        private static int _onceSpawnCount;
        private static int _onceSpawnWeight;
        private Transform _spawnedParent;
        private BuffManager _buffManager;
        private UIManager _uiManager;
        private BuffDatabase _buffDatabase;
        private GameLoopController _gameLoopController;

        [Inject]
        private void Init(MapBoundDefiner mapBoundDefiner, UIManager uiManager, IConfigProvider configProvider, BuffManager buffManager, GameMapInjector gameMapInjector, PlayerInGameManager playerInGameManager,GameEventManager gameEventManager, MessageCenter messageCenter)
        {
            _buffManager = buffManager;
            _uiManager = uiManager;
            _configProvider = configProvider;
            _mapBoundDefiner = mapBoundDefiner;
            _gameMapInjector = gameMapInjector;
            _messageCenter = messageCenter;
            gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnGameSceneResourcesLoadedLoaded);
            _collectObjectDataConfig = _configProvider.GetConfig<CollectObjectDataConfig>();
            _buffDatabase = _configProvider.GetConfig<BuffDatabase>();
            var config = _configProvider.GetConfig<GameDataConfig>();
            _chestConfig = _configProvider.GetConfig<ChestDataConfig>();
            _sceneLayer = config.GameConfigData.GroundSceneLayer;
            _messageCenter.Register<PickerPickUpChestMessage>(OnPickerPickUpChestMessage);
            _messageCenter.Register<PickerPickUpMessage>(OnPickUpItem);
            _gameLoopController = FindObjectOfType<GameLoopController>();
            _spawnedParent = transform;
            _spawnedItems.OnChange += OnSpawnItemsChange;
            CollectItemReaderWriter.RegisterReaderWriter();
        }

        private void OnGameSceneResourcesLoadedLoaded(GameSceneResourcesLoadedEvent gameSceneResourcesLoadedEvent)
        {
            var config = _configProvider.GetConfig<CollectObjectDataConfig>();
            _itemSpacing = config.CollectData.ItemSpacing;
            _maxGridItems = config.CollectData.MaxGridItems;
            _itemHeight = config.CollectData.ItemHeight;
            _gridSize = config.CollectData.GridSize;
            _onceSpawnCount = config.CollectData.OnceSpawnCount;
            _onceSpawnWeight = config.CollectData.OnceSpawnWeight;
            OnGameStart(gameSceneResourcesLoadedEvent.SceneName);
            InitializeGrid();
        }

        private void OnDisable()
        {
            Debug.Log("ItemsSpawnerManager OnDisable");
        }

        private void OnPickerPickUpChestMessage(PickerPickUpChestMessage message)
        {
            if (!isServer || _treasureChestInfo.isPicking) return;
            _treasureChestInfo.isPicking = true;

            // 通过netId找到实际的宝箱物体
            var networkIdentity = NetworkServer.spawned[_treasureChestInfo.netId];
            if (!networkIdentity)
            {
                Debug.LogError($"Cannot find treasure chest with netId: {_treasureChestInfo.netId}");
                _treasureChestInfo.isPicking = false;
                return;
            }

            var treasureChest = networkIdentity.GetComponent<TreasureChestComponent>();
    
            // 获取玩家实例
            var playerIdentity = NetworkServer.spawned[message.PickerId];
            if (!playerIdentity)
            {
                Debug.LogError($"Cannot find player with netId: {message.PickerId}");
                _treasureChestInfo.isPicking = false;
                return;
            }
    
            var player = playerIdentity.GetComponent<PlayerPropertyComponent>();
            if (player.CurrentAnimationState == AnimationState.Dead)
            {
                _treasureChestInfo.isPicking = false;
                return;
            }

            // 验证位置和碰撞
            if (ValidateChestPickup(treasureChest, player, _treasureChestInfo.position))
            {
                // 更新宝箱状态
                _treasureChestInfo = new TreasureChestInfo
                {
                    netId = _treasureChestInfo.netId,
                    chestType = _treasureChestInfo.chestType,
                    position = _treasureChestInfo.position,
                    isPicked = true
                };

                // 处理buff和属性
                player.CurrentChestType = treasureChest.chestType;
                var configData = _chestConfig.GetChestConfigData(treasureChest.chestType);
                if (configData.ChestPropertyData.BuffExtraData.buffType != BuffType.None)
                {
                    _buffManager.AddBuffToPlayer(player, configData.ChestPropertyData.BuffExtraData);
                    Debug.Log($"Add buff {configData.ChestPropertyData.BuffExtraData.buffType} to player {player.name}");
                }

                treasureChest.PickUpSuccess(() =>
                {
                    GameObjectPoolManger.Instance.ReturnObject(networkIdentity.gameObject);
                    NetworkServer.UnSpawn(networkIdentity.gameObject);
                    _treasureChestInfo.isPicking = false;
                }).Forget();
                JudgeEndRound();
            }
            _treasureChestInfo.isPicking = false;
        }
        
        private bool ValidateChestPickup(TreasureChestComponent chest, PlayerPropertyComponent player, Vector3 originalPosition)
        {
            // 基本碰撞检测
            var chestCollider = chest.ChestCollider;
            var playerCollider = player.GetComponent<Collider>();
            if (!chestCollider.bounds.Intersects(playerCollider.bounds))
                return false;

            // 可选：检查当前位置是否在原始位置的合理范围内
            // var currentPosition = chest.transform.position;
            // var distanceFromOriginal = Vector3.Distance(currentPosition, originalPosition);
            // if (distanceFromOriginal > 1f) // 定义一个合理的最大距离
            //     return false;

            return true;
        }

        private void OnPickUpItem(PickerPickUpMessage message)
        {
            if (isServer)
            {
                HandleItemPickup(message.ItemId, message.PickerId);
                JudgeEndRound();
            }
        }

        private void OnSpawnItemsChange(SyncIDictionary<int, SpawnItemInfo>.Operation operation, int id, SpawnItemInfo data)
        {
            switch (operation)
            {
                case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_ADD:
                    //Debug.Log($"Spawned item {id} {data.component.gameObject.name}");
                    break;
                case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_CLEAR:
                    //Debug.Log("Clear all spawned items");
                    break;
                case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_REMOVE:
                    if (_spawnedItems.Count == 0)
                    {
                        if (isServer)
                        {
                            _gameLoopController.IsEndRound = true;
                        }
                    }
                    Debug.Log($"Remove item {id} {data.collectType}");
                    break;
                case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_SET:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        private void OnGameStart(string sceneName)
        {
            _gridMap.Clear();
            _gridMap = _mapBoundDefiner.GridMap.ToDictionary(x => x,_ => new Grid());
            var res = ResourceManager.Instance.GetMapCollectObject(sceneName);
            if (_collectiblePrefabs.Count == 0)
            {
                // 使用字典初始化
                _collectiblePrefabs = res
                    .Where(x => x.GetComponent<CollectObjectController>() != null)
                    .ToDictionary(
                        x => x.GetComponent<CollectObjectController>().CollectType,
                        x => new CollectibleItemData
                        {
                            component = x.GetComponent<CollectObjectController>()
                        }
                    );
            }

            if (!_treasureChestPrefab)
            {
                _treasureChestPrefab = res.Find(x => x.GetComponent<TreasureChestComponent>()!= null).GetComponent<TreasureChestComponent>();
            }

            _uiManager.SwitchUI<TargetShowOverlay>();
        }

        private void OnDestroy()
        {
            _uiManager.CloseUI(UIType.TargetShowOverlay);
        }

        [Server]
        public void HandleItemPickup(int itemId, uint pickerId)
        {
            if (_spawnedItems.TryGetValue(itemId, out var itemInfo))
            {
                // 通过netId找到实际物体
                var networkIdentity = NetworkServer.spawned[itemInfo.netId];
                if (networkIdentity == null)
                {
                    Debug.LogError($"Cannot find spawned item with netId: {itemInfo.netId}");
                    return;
                }

                var item = networkIdentity.GetComponent<CollectObjectController>();
                var player =  NetworkServer.spawned[pickerId];
                if (player == null)
                {
                    Debug.LogError($"Cannot find player with netId: {pickerId}");
                    return;
                }

                var playerProperty = player.GetComponent<PlayerPropertyComponent>();
                // 验证位置和碰撞
                if (ValidatePickup(item, playerProperty, itemInfo.position))
                {
                    _spawnedItems.Remove(itemId);
            
                    // 处理拾取逻辑
                    var configData = _collectObjectDataConfig.GetCollectObjectData(itemInfo.collectType);
                    switch (configData.CollectObjectClass)
                    {
                        case CollectObjectClass.Score:
                            var buff = _buffDatabase.GetBuff(configData.BuffExtraData);
                            playerProperty.IncreaseProperty(PropertyTypeEnum.Score, buff.increaseDataList);
                            break;
                        case CollectObjectClass.Buff:
                            _buffManager.AddBuffToPlayer(playerProperty, configData.BuffExtraData);
                            break;
                    }

                    // 通知客户端
                    RpcPickupItem(item);
            
                    // 回收物体
                    GameObjectPoolManger.Instance.ReturnObject(networkIdentity.gameObject);
                    NetworkServer.UnSpawn(networkIdentity.gameObject);
                }
            }
        }
        
        [ClientRpc]
        private void RpcPickupItem(CollectObjectController item)
        {
            item.CollectSuccess();
            GameObjectPoolManger.Instance.ReturnObject(item.gameObject);
        }

        private int GenerateID()
        {
            return _currentId++;
        }
        
        public int GenerateRandomWeight()
        {
            // 根据需要生成随机权重
            return Mathf.FloorToInt(Random.Range(_onceSpawnWeight-10, _onceSpawnWeight+10));
        }

        public int GenerateRandomSpawnMethod()
        {
            // 随机选择生成方式，0-3分别对应四种生成方式
            return Random.Range(0, 4);
        }

        [Server]
        private void JudgeEndRound()
        {
            var endRound = _spawnedItems.Count == 0 && _treasureChest.isPicked;
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
                // 清理宝箱
                if (_treasureChestInfo.netId != 0 && !_treasureChestInfo.isPicked)
                {
                    var chestIdentity = NetworkServer.spawned[_treasureChestInfo.netId];
                    if (chestIdentity)
                    {
                        GameObjectPoolManger.Instance.ReturnObject(chestIdentity.gameObject);
                        NetworkServer.UnSpawn(chestIdentity.gameObject);
                    }
                }
                _treasureChestInfo = new TreasureChestInfo();

                // 清理所有生成物
                foreach (var item in _spawnedItems.Values.ToList())
                {
                    var identity = NetworkServer.spawned[item.netId];
                    if (identity)
                    {
                        var pooledComponent = identity.GetComponent<CollectObjectController>();
                        if (pooledComponent)
                        {
                            GameObjectPoolManger.Instance.ReturnObject(pooledComponent.gameObject);
                        }
                        NetworkServer.UnSpawn(identity.gameObject);
                    }
                }

                // 清理网格数据
                foreach (var grid in _gridMap.Values)
                {
                    grid.itemIDs?.Clear();
                }

                Debug.Log("EndRound finished on server");
                // 通知客户端清理
                RpcEndRound();
                _spawnedItems.Clear();
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
                try
                {
                    // 清理客户端的生成物
                    foreach (var item in _spawnedItems.Values)
                    {
                        if (NetworkClient.spawned.TryGetValue(item.netId, out var networkIdentity))
                        {
                            var pooledComponent = networkIdentity.GetComponent<CollectObjectController>();
                            if (pooledComponent)
                                GameObjectPoolManger.Instance.ReturnObject(networkIdentity.gameObject);
                        }
                    }
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in RpcEndRound: {e}");
                }
            } // 服务器已经处理过了

            Debug.Log("EndRound finished on client");
        }

        [Server]
        public async UniTask SpawnItemsAndChest()
        {
            await SpawnManyItems();
            SpawnTreasureChestServer();
        }
        
        private CollectObjectController GetPrefabByCollectType(CollectType type)
        {
            if (_collectiblePrefabs.TryGetValue(type, out var itemData))
            {
                return itemData.component;
            }
            Debug.LogError($"No prefab found for CollectType: {type}");
            return null;
        }

        [Server]
        public async UniTask SpawnManyItems()
        {
            try 
            {
                _spawnedItems.Clear();
                _currentId = 0;
                var spawnInfos = new List<SpawnItemInfo>();
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
                        spawnInfos.Add(new SpawnItemInfo
                        {
                            id = GenerateID(),
                            netId = 0,
                            collectType = item.component.CollectType,
                            position = item.position
                        });
                    }
                    //Debug.Log($"Calculated {spawnedCount} spawn positions");
                    await UniTask.Yield();
                }

                // 在服务器端生成物品
                foreach (var info in spawnInfos)
                {
                    var prefab = GetPrefabByCollectType(info.collectType);
                    var spawnedObject = GameObjectPoolManger.Instance.GetObject(
                        prefab.gameObject, 
                        info.position, 
                        Quaternion.identity, 
                        _spawnedParent,
                        go => _gameMapInjector.InjectGameObject(go)
                    );

                    var networkIdentity = spawnedObject.GetComponent<NetworkIdentity>();
                    NetworkServer.Spawn(spawnedObject); // 确保网络同步
                    var component = spawnedObject.GetComponent<CollectObjectController>();
                    component.CollectId = info.id;
                    _spawnedItems.Add(info.id, new SpawnItemInfo
                    {
                        id = info.id,
                        netId = networkIdentity.netId,
                        collectType = info.collectType,
                        position = info.position
                    });
                }
                SpawnManyItemsClientRpc(spawnInfos.ToArray());
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in SpawnManyItems: {e}");
                throw;
            }
        }

        [Server]
        public void SpawnTreasureChestServer()
        {
            var chestType = (ChestType)Random.Range(1, (int)ChestType.Score + 1);
            var position = GetRandomStartPoint(0.75f);

            // 在服务器端生成宝箱
            var spawnedChest = GameObjectPoolManger.Instance.GetObject(
                _treasureChestPrefab.gameObject, 
                position, 
                Quaternion.identity, 
                _spawnedParent,
                go => _gameMapInjector.InjectGameObject(go)
            );
            var networkIdentity = spawnedChest.GetComponent<NetworkIdentity>();
            NetworkServer.Spawn(spawnedChest);

            var treasureChest = spawnedChest.GetComponent<TreasureChestComponent>();
            treasureChest.chestType = chestType;

            // 更新同步数据
            _treasureChestInfo = new TreasureChestInfo
            {
                netId = networkIdentity.netId,
                chestType = chestType,
                position = position,
                isPicked = false
            };
        }

        [ClientRpc]
        private void SpawnManyItemsClientRpc(SpawnItemInfo[] allSpawnedItems)
        {
            SpawnItems(allSpawnedItems);
        }

        private void SpawnItems(SpawnItemInfo[] allSpawnedItems)
        {
            foreach (var info in allSpawnedItems)
            {
                Debug.Log($"Client spawning item at position: {info.position}"); // 添加日志
                var prefab = GetPrefabByCollectType(info.collectType);
                if (!prefab)
                {
                    Debug.LogError($"Failed to find prefab for CollectType: {info.collectType}");
                    continue;
                }

                var go = GameObjectPoolManger.Instance.GetObject(prefab.gameObject, info.position, Quaternion.identity, _spawnedParent,
                    go => _gameMapInjector.InjectGameObject(go));
                if (!go)
                {
                    Debug.LogError("Failed to get object from pool");
                    continue;
                }

                var component = go.GetComponent<CollectObjectController>();
                component.CollectId = info.id;
                Debug.Log($"Spawning item at position: {info.position} with id: {info.id}");
        
                // 确保位置正确设置
                go.transform.position = info.position;
            }
        }

        private void InitializeGrid()
        {
            for (var x = _mapBoundDefiner.MapMinBoundary.x; x <= _mapBoundDefiner.MapMaxBoundary.x; x += _gridSize)
            {
                for (var z = _mapBoundDefiner.MapMinBoundary.z; z <= _mapBoundDefiner.MapMaxBoundary.z; z += _gridSize)
                {
                    var gridPos = GetGridPosition(new Vector3(x, 0, z));
                    _gridMap[gridPos] = new Grid();
                }
            }
        }

        private Vector2Int GetGridPosition(Vector3 position)
        {
            var x = Mathf.FloorToInt(position.x / _gridSize);
            var z = Mathf.FloorToInt(position.z / _gridSize);
            return new Vector2Int(x, z);
        }

        private List<CollectibleItemData> SpawnItems(int totalWeight, int spawnMode)
        {
            var collectTypes = new List<CollectType>();
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
            
            var itemToSpawn = collectTypes
                .Select(type => _collectiblePrefabs[type])
                .ToList();
            return PlaceItems(itemToSpawn);
        }

        private void SpawnMode1(List<CollectType> itemsToSpawn, ref int remainingWeight)
        {
            var scoreCount = 0;
            while (remainingWeight > 0)
            {
                // Generate Score item
                var scoreItem = GetRandomItem(CollectObjectClass.Score);
                if (scoreItem != -1)
                {
                    var type = (CollectType)scoreItem;
                    var configData = _collectObjectDataConfig.GetCollectObjectData(type);
                    itemsToSpawn.Add(type);
                    remainingWeight -= configData.Weight;
                    scoreCount++;
                }

                // Check if we should add a Buff item
                if (remainingWeight <= 0) break;

                var buffItem = GetRandomItem(CollectObjectClass.Buff);
                if (buffItem != -1)
                {
                    var type = (CollectType)buffItem;
                    var configData = _collectObjectDataConfig.GetCollectObjectData(type);
                    itemsToSpawn.Add(type);
                    remainingWeight -= configData.Weight;
                    break; // Buff item added last
                }
            }

            // If no Buff item was added, ensure one is added at the end
            if (scoreCount > 0 && remainingWeight > 0)
            {
                var buffItem = GetRandomItem(CollectObjectClass.Buff);
                if (buffItem != -1)
                {
                    itemsToSpawn.Add((CollectType)buffItem);
                }
            }
        }

        private void SpawnMode2(List<CollectType> itemsToSpawn, ref int remainingWeight)
        {
            var scoreItems = new List<CollectType>();
            var buffItems = new List<CollectType>();

            while (remainingWeight > 0)
            {
                var scoreItem = GetRandomItem(CollectObjectClass.Score);
                if (scoreItem != -1)
                {
                    var type = (CollectType)scoreItem;
                    var configData = _collectObjectDataConfig.GetCollectObjectData(type);
                    scoreItems.Add(type);
                    remainingWeight -= configData.Weight;
                }

                var buffItem = GetRandomItem(CollectObjectClass.Buff);
                if (buffItem != -1)
                {
                    var type = (CollectType)buffItem;
                    var configData = _collectObjectDataConfig.GetCollectObjectData(type);
                    buffItems.Add(type);
                    remainingWeight -= configData.Weight;
                }

                if (scoreItems.Count + buffItems.Count >= 100) break;
            }

            // Combine and order items based on weights
            itemsToSpawn.AddRange(scoreItems);
            itemsToSpawn.AddRange(buffItems);
        }

        private void SpawnMode3(List<CollectType> itemsToSpawn, ref int remainingWeight)
        {
            while (remainingWeight > 0)
            {
                var randomType = (Random.Range(0, 1) > 0.5f) ? CollectObjectClass.Score : CollectObjectClass.Buff;
                var item = GetRandomItem(randomType);
                if (item != -1)
                {
                    var type = (CollectType)item;
                    var configData = _collectObjectDataConfig.GetCollectObjectData(type);
                    itemsToSpawn.Add(type);
                    remainingWeight -= configData.Weight;
                }
                
            }
        }
    
        private int GetRandomItem(CollectObjectClass itemClass)
        {
            var configData = _collectObjectDataConfig.GetCollectObjectDataWithCondition(x => x.CollectObjectClass == itemClass)
                .Select(x => x.CollectType)
                .ToArray();
            var count = configData.Length;
            if (count > 0)
            {
                var randomIndex = Random.Range(0, count);
                return (int)configData[randomIndex];
            }
            return -1;
        }
        
        private bool ValidatePickup(CollectObjectController item, PlayerPropertyComponent player, Vector3 originalPosition)
        {
            // 基本碰撞检测
            var itemCollider = item.Collider;
            var playerCollider = player.GetComponent<Collider>();
            if (!itemCollider.bounds.Intersects(playerCollider.bounds))
                return false;

            return true;
        }
        
        private List<CollectibleItemData> PlaceItems(List<CollectibleItemData> itemsToSpawn)
        {
            var startPoint = GetRandomStartPoint(_itemHeight);
            if (startPoint == Vector3.zero)
            {
                Debug.LogWarning("Failed to find valid start point");
                return new List<CollectibleItemData>();
            }

            var direction = GetRandomDirection();
            var spawnedIDs = new List<CollectibleItemData>();

            foreach (var item in itemsToSpawn)
            {
                var attempts = 0;
                var maxAttempts = 5;
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
                        if (IsPositionValid(position, item.component.GetComponent<Collider>()) && 
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
                    item.position = position;
                    var id = GenerateID();
                    item.id = id;
            
                    var gridPos = GetGridPosition(position);
                    if (_gridMap.TryGetValue(gridPos, out var grid))
                    {
                        grid.itemIDs ??= new List<CollectibleItemData>();
                        grid.itemIDs.Add(item);
                        _gridMap[gridPos] = grid;
                    }
            
                    spawnedIDs.Add(item);
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
            if (grid.itemIDs != null && grid.itemIDs.Count >= _maxGridItems)
                return false;

            // 从高处发射射线检查地面
            var rayStart = position + Vector3.up * 1000f;
            if (!Physics.Raycast(rayStart, Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
                return false;

            // 调整位置到地面上方
            position = hit.point + Vector3.up * _itemHeight;

            // 检查碰撞
            if (itemPrefab != null)
            {
                Collider[] hitColliders = null;
                Vector3 checkSize = Vector3.zero;
                float checkRadius = 0f;

                if (itemPrefab is BoxCollider boxCollider)
                {
                    checkSize = boxCollider.bounds.extents * 1.1f; // 添加一些余量
                    hitColliders = Physics.OverlapBox(position, checkSize, Quaternion.identity, _sceneLayer);
                }
                else if (itemPrefab is SphereCollider sphereCollider)
                {
                    checkRadius = sphereCollider.radius * 1.1f;
                    hitColliders = Physics.OverlapSphere(position, checkRadius, _sceneLayer);
                }
                else if (itemPrefab is CapsuleCollider capsuleCollider)
                {
                    checkRadius = capsuleCollider.radius * 1.1f;
                    var point2 = position + Vector3.up * capsuleCollider.height;
                    hitColliders = Physics.OverlapCapsule(position, point2, checkRadius, _sceneLayer);
                }
                else
                {
                    Debug.LogError("Unsupported collider type");
                    return false;
                }

                return hitColliders == null || hitColliders.Length == 0;
            }

            return true;
        }

        private bool IsWithinBoundary(Vector3 position)
        {
            return _mapBoundDefiner.IsWithinMapBounds(position);
        
        }
    }

    [Serializable]
    public class CollectibleItemData
    {
        public int id;
        public CollectObjectController component;
        public Vector3 position;
    }

    [Serializable]
    public struct SpawnItemInfo
    {
        public int id;
        public uint netId;
        public CollectType collectType;
        public Vector3 position;
    }

    [Serializable]
    public struct TreasureChestInfo
    {
        public uint netId;
        public ChestType chestType;
        public Vector3 position;
        public bool isPicked;
        public bool isPicking;
    }

    public static class CollectItemReaderWriter
    {
        public static void RegisterReaderWriter()
        {
            // Reader<CollectibleItemData>.read = ReadCollectibleItemDataData;
            // Writer<CollectibleItemData>.write= WriteCollectibleItemDataData;
            Reader<SpawnItemInfo>.read = ReadSpawnedItemDataData;
            Writer<SpawnItemInfo>.write = WriteSpawnedItemDataData;
            Reader<TreasureChestInfo>.read = ReadTreasureChestInfoData;
            Writer<TreasureChestInfo>.write = WriteTreasureChestInfoData;
        }
        
        private static void WriteTreasureChestInfoData(NetworkWriter writer, TreasureChestInfo info)
        {
            writer.WriteUInt(info.netId);
            writer.WriteInt((int)info.chestType);
            writer.WriteVector3(info.position);
            writer.WriteBool(info.isPicked);
            writer.WriteBool(info.isPicking);
        }

        private static TreasureChestInfo ReadTreasureChestInfoData(NetworkReader reader)
        {
            return new TreasureChestInfo
            {
                netId = reader.ReadUInt(),
                chestType = (ChestType)reader.ReadInt(),
                position = reader.ReadVector3(),
                isPicked = reader.ReadBool(),
                isPicking = reader.ReadBool()
            };
        }
        
        private static void WriteSpawnedItemDataData(NetworkWriter writer, SpawnItemInfo info)
        {
            writer.WriteInt(info.id);
            writer.WriteInt((int)info.collectType);
            writer.WriteUInt(info.netId);
            writer.WriteVector3(info.position);
        }

        private static SpawnItemInfo ReadSpawnedItemDataData(NetworkReader reader)
        {
            return new SpawnItemInfo
            {
                id = reader.ReadInt(),
                collectType = (CollectType)reader.ReadInt(),
                position = reader.ReadVector3(),
                netId = reader.ReadUInt()
            };
        }

        private static void WriteCollectibleItemDataData(NetworkWriter writer, CollectibleItemData data)
        {
            writer.WriteInt(data.id);
            writer.WriteNetworkIdentity(data.component.GetComponent<NetworkIdentity>());
            writer.WriteVector3(data.position);
        }

        private static CollectibleItemData ReadCollectibleItemDataData(NetworkReader reader)
        {
            return new CollectibleItemData
            {
                id = reader.ReadInt(),
                component = reader.ReadNetworkIdentity().GetComponent<CollectObjectController>(),
                position = reader.ReadVector3()
            };
        }
        
        public static void UnregisterReaderWriter()
        {
            Reader<CollectibleItemData>.read = null;
            Writer<CollectibleItemData>.write = null;
        }
    }
}