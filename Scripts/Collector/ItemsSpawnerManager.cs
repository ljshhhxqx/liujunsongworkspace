using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.ObjectPool;
using Collector;
using Config;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Game;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server.Collect;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class ItemsSpawnerManager : ServerNetworkComponent
    {
        [SyncVar]
        private int _currentId;
        [SyncVar(hook = nameof(OnTreasureChestInfoChanged))]
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
        private PlayerInGameManager _playerInGameManager;
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
        private BuffDatabase _buffDatabase;
        private GameLoopController _gameLoopController;

        [Inject]
        private void Init(MapBoundDefiner mapBoundDefiner, IConfigProvider configProvider, BuffManager buffManager, PlayerInGameManager playerInGameManager,GameEventManager gameEventManager, MessageCenter messageCenter)
        {
            _playerInGameManager = playerInGameManager;
            _buffManager = buffManager;
            _configProvider = configProvider;
            _mapBoundDefiner = mapBoundDefiner;
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
        
        private void OnPickerPickUpChestMessage(PickerPickUpChestMessage message)
        {
            if (!isServer) return;

            // 通过netId找到实际的宝箱物体
            var networkIdentity = NetworkIdentity.GetSceneIdentity(_treasureChestInfo.netId);
            if (networkIdentity == null)
            {
                Debug.LogError($"Cannot find treasure chest with netId: {_treasureChestInfo.netId}");
                return;
            }

            var treasureChest = networkIdentity.GetComponent<TreasureChestComponent>();
    
            // 获取玩家实例
            var playerIdentity = NetworkIdentity.GetSceneIdentity(message.PickerId);
            if (playerIdentity == null)
            {
                Debug.LogError($"Cannot find player with netId: {message.PickerId}");
                return;
            }
    
            var player = playerIdentity.GetComponent<PlayerPropertyComponent>();
            if (player.PlayerState == PlayerState.Dead)
            {
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
                var configData = _chestConfig.GetChestConfigData(player.CurrentChestType);
                player.CurrentChestType = treasureChest.ChestType;
                if (configData.ChestPropertyData.BuffExtraData.buffType != BuffType.None)
                {
                    _buffManager.AddBuffToPlayer(player, configData.ChestPropertyData.BuffExtraData);
                }

                // 回收物体
                NetworkServer.UnSpawn(networkIdentity.gameObject);
                GameObjectPoolManger.Instance.ReturnObject(networkIdentity.gameObject);

                JudgeEndRound();
            }
        }
        
        private bool ValidateChestPickup(TreasureChestComponent chest, PlayerPropertyComponent player, Vector3 originalPosition)
        {
            // 基本碰撞检测
            var chestCollider = chest.GetComponent<Collider>();
            var playerCollider = player.GetComponent<Collider>();
            if (!chestCollider.bounds.Intersects(playerCollider.bounds))
                return false;

            // 可选：检查当前位置是否在原始位置的合理范围内
            var currentPosition = chest.transform.position;
            var distanceFromOriginal = Vector3.Distance(currentPosition, originalPosition);
            if (distanceFromOriginal > 1f) // 定义一个合理的最大距离
                return false;

            return true;
        }

        [ClientRpc]
        private void RpcPickUpTreasureChest()
        {
            PickUpTreasureChest();
        }

        private void PickUpTreasureChest()
        {
            if (_treasureChest)
            {
                _treasureChest.PickUpSuccess();
            }
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
        }

        [Server]
        public void HandleItemPickup(int itemId, int pickerId)
        {
            if (_spawnedItems.TryGetValue(itemId, out var itemInfo))
            {
                // 通过netId找到实际物体
                var networkIdentity = NetworkIdentity.GetSceneIdentity(itemInfo.netId);
                if (networkIdentity == null)
                {
                    Debug.LogError($"Cannot find spawned item with netId: {itemInfo.netId}");
                    return;
                }

                var item = networkIdentity.GetComponent<CollectObjectController>();
                var player = _playerInGameManager.GetPlayerPropertyComponent(pickerId);

                // 验证位置和碰撞
                if (ValidatePickup(item, player, itemInfo.position))
                {
                    _spawnedItems.Remove(itemId);
            
                    // 处理拾取逻辑
                    var configData = _collectObjectDataConfig.GetCollectObjectData(itemInfo.collectType);
                    switch (configData.CollectObjectClass)
                    {
                        case CollectObjectClass.Score:
                            var buff = _buffDatabase.GetBuff(configData.BuffExtraData);
                            player.IncreaseProperty(PropertyTypeEnum.Score, buff.increaseDataList);
                            break;
                        case CollectObjectClass.Buff:
                            _buffManager.AddBuffToPlayer(player, configData.BuffExtraData);
                            break;
                    }

                    // 通知客户端
                    RpcPickupItem(item);
            
                    // 回收物体
                    NetworkServer.UnSpawn(networkIdentity.gameObject);
                    GameObjectPoolManger.Instance.ReturnObject(networkIdentity.gameObject);
                }
            }
        }

        // 移除旧的RPC方法，使用SyncVar自动同步
        private void OnTreasureChestInfoChanged(TreasureChestInfo oldInfo, TreasureChestInfo newInfo)
        {
            if (!isServer) // 客户端处理
            {
                if (_treasureChest == null)
                {
                    var chest = NetworkClient.spawned[newInfo.netId];
                    _treasureChest = chest.GetComponent<TreasureChestComponent>();
                    _treasureChest.transform.position = newInfo.position;
                    _treasureChest.ChestType = newInfo.chestType;
                }
        
                if (newInfo.isPicked && !oldInfo.isPicked)
                {
                    _treasureChest.PickUpSuccess();
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
        public void EndRound()
        {
            // 清理宝箱
            if (_treasureChest)
            {
                NetworkServer.UnSpawn(_treasureChest.gameObject);
                GameObjectPoolManger.Instance.ReturnObject(_treasureChest.gameObject);
                _treasureChest = null;
            }

            // 清理所有生成物
            ClearAllSpawnedItems();

            // 通知客户端清理
            RpcEndRound();
        }

        [ClientRpc]
        private void RpcEndRound()
        {
            if (isServer) return; // 服务器已经处理过了

            // 清理客户端的生成物
            ClearAllSpawnedItems();

            // 清理宝箱
            if (_treasureChest)
            {
                GameObjectPoolManger.Instance.ReturnObject(_treasureChest.gameObject);
                _treasureChest = null;
            }
        }
        
        private void SafeReturnToPool(GameObject go)
        {
            if (go && go.scene.name != null) // 确保对象还在场景中
            {
                try
                {
                    GameObjectPoolManger.Instance.ReturnObject(go);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error returning object to pool: {e}");
                }
            }
        }

        private void ClearAllSpawnedItems()
        {
            if (isServer)
            {
                foreach (var item in _spawnedItems.Values.ToList())
                {
                    var identity = NetworkIdentity.GetSceneIdentity(item.netId);
                    if (identity != null)
                    {
                        NetworkServer.UnSpawn(netIdentity.gameObject);
                        SafeReturnToPool(netIdentity.gameObject);
                    }
                }
            }
            else
            {
                foreach (var item in _spawnedItems.Values)
                {
                    if (NetworkClient.spawned.TryGetValue(item.netId, out var netIdentity))
                    {
                        SafeReturnToPool(netIdentity.gameObject);
                    }
                }
            }
            _spawnedItems.Clear();
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
                    Debug.Log($"Calculated {spawnedCount} spawn positions");
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
                        _spawnedParent
                    );

                    var networkIdentity = spawnedObject.GetComponent<NetworkIdentity>();
                    NetworkServer.Spawn(spawnedObject); // 确保网络同步

                    _spawnedItems.Add(info.id, new SpawnItemInfo
                    {
                        id = info.id,
                        netId = networkIdentity.netId,
                        collectType = info.collectType,
                        position = info.position
                    });
                }
                // 添加这行：通知客户端生成物品
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
            var chestType = (ChestType)Mathf.RoundToInt(Random.Range(0f, (float)ChestType.Score));
            var position = GetRandomStartPoint();

            // 在服务器端生成宝箱
            var spawnedChest = GameObjectPoolManger.Instance.GetObject(
                _treasureChestPrefab.gameObject, 
                position, 
                Quaternion.identity, 
                _spawnedParent
            );

            var networkIdentity = spawnedChest.GetComponent<NetworkIdentity>();
            NetworkServer.Spawn(spawnedChest);

            var treasureChest = spawnedChest.GetComponent<TreasureChestComponent>();
            treasureChest.ChestType = chestType;

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
                if (prefab == null)
                {
                    Debug.LogError($"Failed to find prefab for CollectType: {info.collectType}");
                    continue;
                }

                var go = GameObjectPoolManger.Instance.GetObject(prefab.gameObject, info.position, Quaternion.identity, _spawnedParent);
                if (go == null)
                {
                    Debug.LogError("Failed to get object from pool");
                    continue;
                }

                var component = go.GetComponent<CollectObjectController>();
                component.CollectId = info.id;
        
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
            
    
            // 使用字典直接查找替代Join
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
            var itemCollider = item.GetComponent<Collider>();
            var playerCollider = player.GetComponent<Collider>();
            if (!itemCollider.bounds.Intersects(playerCollider.bounds))
                return false;

            // 可选：检查当前位置是否在原始位置的合理范围内
            // var currentPosition = item.transform.position;
            // var distanceFromOriginal = Vector3.Distance(currentPosition, originalPosition);
            // if (distanceFromOriginal > 1) // 定义一个合理的最大距离
            //     return false;

            return true;
        }
        private List<CollectibleItemData> PlaceItems(List<CollectibleItemData> itemsToSpawn)
        {
            var startPoint = GetRandomStartPoint();
            var direction = GetRandomDirection();

            var spawnedIDs = new List<CollectibleItemData>();

            for (int i = 0; i < itemsToSpawn.Count; i++)
            {
                var item = itemsToSpawn[i];
                var position = startPoint + _itemSpacing * i * direction + Vector3.up * _itemHeight;

                if (!IsPositionValid(position, itemsToSpawn[i].component.GetComponent<Collider>()))
                {
                    if (Physics.Raycast(new Vector3(position.x, 1000, position.z), Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
                    {
                        position.y = hit.point.y + _itemHeight;
                    }
                }

                if (IsWithinBoundary(position))
                {
                    var id = GenerateID();
                    item.id = id;
                    item.position = position;
                    _spawnedItems[id] = new SpawnItemInfo
                    {
                        id = id,
                        netId = 0,
                        collectType = item.component.CollectType,
                        position = position
                    };

                    var gridPos = GetGridPosition(position);
                    if (_gridMap.TryGetValue(gridPos, out var grid))
                    {
                        grid.itemIDs ??= new List<CollectibleItemData>();
                        grid.itemIDs.Add(item);
                        _gridMap[gridPos] = grid; // Update the grid with the new itemID
                    }

                    spawnedIDs.Add(item);
                }
                else
                {
                    Debug.LogWarning("Item out of boundary, retrying spawn.");
                    SpawnItems(Random.Range(10, 20), Random.Range(1, 4));
                    return new List<CollectibleItemData>(); // Return an empty list if the placement fails
                }
            }

            return spawnedIDs;
        }
        
        private bool IsPositionValidWithoutItem(Vector3 position)
        {
            return IsPositionValid(position, null);
        }
    
        private Vector3 GetRandomStartPoint()
        {

            var randomPos = _mapBoundDefiner.GetRandomPoint(IsPositionValidWithoutItem);
            return new Vector3(randomPos.x, randomPos.y + _itemHeight, randomPos.z);
        }
    
        private Vector3 GetRandomDirection()
        {
            return _mapBoundDefiner.GetRandomDirection();
        }

        private bool IsPositionValid(Vector3 position, Collider itemPrefab)
        {
            var gridPos = GetGridPosition(position);
            if (_gridMap.TryGetValue(gridPos, out var grid))
            {
                if (itemPrefab != null)
                {
                    Collider[] hitColliders =null ;
                    if (itemPrefab is BoxCollider boxCollider)
                    {
                        hitColliders = Physics.OverlapBox(position, boxCollider.bounds.extents, Quaternion.identity, _sceneLayer);
                    }
                    else if (itemPrefab is SphereCollider sphereCollider)
                    {
                        hitColliders = Physics.OverlapSphere(position, sphereCollider.radius, _sceneLayer);
                    }
                    else if (itemPrefab is CapsuleCollider capsuleCollider)
                    {
                        hitColliders = Physics.OverlapCapsule(position, capsuleCollider.transform.position, capsuleCollider.radius, _sceneLayer);
                    }
                    else
                    {
                        throw new Exception("Unsupported collider type");
                    }
                    foreach (var hitCollider in hitColliders)
                    {
                        if (hitCollider.gameObject.GetInstanceID() == itemPrefab.gameObject.GetInstanceID())
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    if (grid.itemIDs != null && grid.itemIDs.Count > _maxGridItems)
                    {
                        return false;
                    }
                }
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
        }

        private static TreasureChestInfo ReadTreasureChestInfoData(NetworkReader reader)
        {
            return new TreasureChestInfo
            {
                netId = reader.ReadUInt(),
                chestType = (ChestType)reader.ReadInt(),
                position = reader.ReadVector3(),
                isPicked = reader.ReadBool()
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