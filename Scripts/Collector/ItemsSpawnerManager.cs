using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using HotUpdate.Scripts.UI.UIs.Overlay;
using Mirror;
using Tool.GameEvent;
using Tool.Message;
using UI.UIBase;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class ItemsSpawnerManager : ServerNetworkComponent
    {
        [SyncVar]
        private int _currentId;
        [SyncVar]
        private TreasureChestInfo _treasureChestInfo;
        private Dictionary<int, CollectibleItemData> _collectiblePrefabs = new Dictionary<int, CollectibleItemData>();
        private readonly Dictionary<CollectObjectBuffSize, Dictionary<PropertyTypeEnum, Material>> _collectibleMaterials = new Dictionary<CollectObjectBuffSize, Dictionary<PropertyTypeEnum, Material>>();
        private TreasureChestComponent _treasureChestPrefab;
        private TreasureChestComponent _treasureChest;
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
        //private readonly SyncDictionary<int, SpawnItemInfo> _spawnedItems = new SyncDictionary<int, SpawnItemInfo>();
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
        private GameLoopController _gameLoopController;
        private GameSyncManager _gameSyncManager;
        private PlayerInGameManager _playerInGameManager;
        
        // 服务器维护的核心数据
        private readonly SyncDictionary<uint, CollectItemMetaData> _serverItemMap = new SyncDictionary<uint, CollectItemMetaData>();
        
        

        [Inject]
        private void Init(MapBoundDefiner mapBoundDefiner, UIManager uiManager, IConfigProvider configProvider ,  GameSyncManager gameSyncManager, GameMapInjector gameMapInjector, PlayerInGameManager playerInGameManager,GameEventManager gameEventManager, MessageCenter messageCenter)
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
            _sceneLayer = _jsonDataConfig.GameConfig.groundSceneLayer;
            _messageCenter.Register<PickerPickUpChestMessage>(OnPickerPickUpChestMessage);
            _messageCenter.Register<PickerPickUpMessage>(OnPickUpItem);
            _gameLoopController = FindObjectOfType<GameLoopController>();
            _spawnedParent = transform;
            //_spawnedItems.OnChange += OnSpawnItemsChange;
            CollectItemReaderWriter.RegisterReaderWriter();
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
            var playerIdentity = _playerInGameManager.GetPlayer(message.ConnectionId);
            if (playerIdentity == null)
            {
                Debug.LogError($"Cannot find player with netId: {message.ConnectionId}");
                _treasureChestInfo.isPicking = false;
                return;
            }
    
            var player = playerIdentity.networkIdentity.GetComponent<PlayerAnimationComponent>();
            var playerProperty = player.GetComponent<PlayerPropertyComponent>();
            if (player.CurrentState.Value == AnimationState.Dead)
            {
                _treasureChestInfo.isPicking = false;
                return;
            }

            // 验证位置和碰撞
            if (ValidateChestPickup(treasureChest, playerProperty, _treasureChestInfo.position))
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
                playerProperty.CurrentChestType = treasureChest.chestType;
                var configData = _chestConfig.GetChestConfigData(treasureChest.chestType);
                if (configData.BuffExtraData.buffType != BuffType.None)
                {
                    //_buffManager.AddBuffToPlayer(playerProperty, configData.BuffExtraData, CollectObjectBuffSize.Small);
                    Debug.Log($"Add buff {configData.BuffExtraData.buffType} to player {player.name}");
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
            var currentPosition = chest.transform.position;
            var distanceFromOriginal = Vector3.Distance(currentPosition, originalPosition);
            if (distanceFromOriginal > 1f) // 定义一个合理的最大距离
                return false;

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

        // private void OnSpawnItemsChange(SyncIDictionary<int, SpawnItemInfo>.Operation operation, int id, SpawnItemInfo data)
        // {
        //     switch (operation)
        //     {
        //         case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_ADD:
        //             break;
        //         case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_CLEAR:
        //             break;
        //         case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_REMOVE:
        //             if (_spawnedItems.Count == 0)
        //             {
        //                 if (isServer)
        //                 {
        //                     _gameLoopController.IsEndRound = true;
        //                 }
        //             }
        //             Debug.Log($"Remove item {id} {data.id}");
        //             break;
        //         case SyncIDictionary<int, SpawnItemInfo>.Operation.OP_SET:
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        //     }
        // }

        private void OnGameStart(string sceneName)
        {
            _gridMap.Clear();
            _gridMap = _mapBoundDefiner.GridMap.ToDictionary(x => x,_ => new Grid());
            var res = ResourceManager.Instance.GetMapCollectObject(sceneName);
            if (_collectiblePrefabs.Count == 0)
            {
                // 使用字典初始化
                _collectiblePrefabs = res
                    .Where(x => x.GetComponent<CollectObjectController>())
                    .ToDictionary(
                        x => x.GetComponent<CollectObjectController>().CollectConfigId,
                        x => new CollectibleItemData
                        {
                            component = x.GetComponent<CollectObjectController>()
                        }
                    );
            }

            if (_collectibleMaterials.Count == 0)
            {
                for (var i = (int)CollectObjectBuffSize.Small; i <= (int)CollectObjectBuffSize.Large; i++)
                {
                    var collectObjectSize = (CollectObjectBuffSize)i;
                    var matList = ResourceManager.Instance.GetMapCollectObjectMaterial(sceneName, collectObjectSize.ToString());
                    _collectibleMaterials.Add(collectObjectSize, new Dictionary<PropertyTypeEnum, Material>());
                    foreach (var material in matList)
                    {
                        if (Enum.TryParse(material.name, out PropertyTypeEnum propertyType))
                        {
                            _collectibleMaterials[collectObjectSize].Add(propertyType, material);
                        }
                    }
                }
            }

            if (!_treasureChestPrefab)
            {
                foreach (var go in res)
                {
                    if (go.TryGetComponent<TreasureChestComponent>(out var treasureChest))
                    {
                        _treasureChestPrefab = treasureChest;
                        break;
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
        
        private readonly HashSet<uint> _processedItems = new HashSet<uint>();

        [Server]
        public void HandleItemPickup(uint itemId, uint pickerId)
        {
            if (_serverItemMap.TryGetValue(itemId, out var itemInfo))
            {
                if (!_processedItems.Add(itemId))
                {
                    return;
                }

                // 通过netId找到实际物体
                Debug.Log($"HandleItemPickup: itemId - {itemId} pickerId - {pickerId} - netId- {itemInfo.netId}");
                var networkIdentity = NetworkServer.spawned[itemInfo.netId];
                if (!networkIdentity)
                {
                    Debug.LogError($"Cannot find spawned item with netId: {itemInfo.netId}");
                    return;
                }

                var item = networkIdentity.GetComponent<CollectObjectController>();
                var player =  NetworkServer.spawned[pickerId];
                if (!player)
                {
                    Debug.LogError($"Cannot find player with netId: {pickerId}");
                    return;
                }

                var playerProperty = player.GetComponent<PlayerPropertyComponent>();
                // 验证位置和碰撞
                if (ValidatePickup(item, playerProperty, itemInfo.position))
                {
                    // 处理拾取逻辑
                    var configData = _collectObjectDataConfig.GetCollectObjectData(itemInfo.collectConfigId);
                    switch (configData.collectObjectClass)
                    {
                        case CollectObjectClass.Score:
                            var buff = configData.buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(configData.buffExtraData) : _randomBuffConfig.GetBuff(configData.buffExtraData);
                            playerProperty.IncreaseProperty(PropertyTypeEnum.Score, buff.increaseDataList);
                            break;
                        case CollectObjectClass.Buff:
                            // if (configData.isRandomBuff)
                            // {
                            //     _buffManager.AddBuffToPlayer(playerProperty, item.BuffData, itemInfo.buffSize);
                            // }
                            // else
                            // {
                            //     _buffManager.AddBuffToPlayer(playerProperty, configData.buffExtraData, itemInfo.buffSize);
                            // }
                            break;
                    }

                    // 通知客户端
                    RpcPickupItem(item);
            
                    // 回收物体
                    GameObjectPoolManger.Instance.ReturnObject(networkIdentity.gameObject);
                    NetworkServer.UnSpawn(networkIdentity.gameObject);
                    _processedItems.Remove(itemId);
                    _spawnedItems.Remove(itemId);
                    
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
            var endRound = _serverItemMap.Count == 0 && _treasureChest.isPicked;
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
                foreach (var item in _serverItemMap.Values.ToList())
                {
                    // var identity = NetworkServer.spawned[item.netId];
                    // if (identity)
                    // {
                    //     var pooledComponent = identity.GetComponent<CollectObjectController>();
                    //     if (pooledComponent)
                    //     {
                    //         GameObjectPoolManger.Instance.ReturnObject(pooledComponent.gameObject);
                    //     }
                    //     NetworkServer.UnSpawn(identity.gameObject);
                    // }
                }

                // 清理网格数据
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
                try
                {
                    // 清理客户端的生成物
                    foreach (var item in _serverItemMap.Values)
                    {
                        // if (NetworkClient.spawned.TryGetValue(item.netId, out var networkIdentity))
                        // {
                        //     var pooledComponent = networkIdentity.GetComponent<CollectObjectController>();
                        //     if (pooledComponent)
                        //         GameObjectPoolManger.Instance.ReturnObject(networkIdentity.gameObject);
                        // }
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
        
        private CollectObjectController GetPrefabByCollectType(int configId)
        {
            if (_collectiblePrefabs.TryGetValue(configId, out var itemData))
            {
                return itemData.component;
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
                _currentId = 0;
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

                        var prefab = GetPrefabByCollectType(item.Item1);
                        var buffData = GetBuffExtraData(item.Item1);
                        var configData = _collectObjectDataConfig.GetCollectObjectData(item.Item1);
                        var buff = _randomBuffConfig.GetRandomBuffData(buffData.buffId);
                        itemInfo = new CollectItemMetaData(id, 
                            item.Item2,
                            0, 
                            item.Item1,
                            (uint)Mathf.Abs(_gameSyncManager.CurrentTick),
                            30, 
                            (ushort)Random.Range(0, 65535),
                            -1,
                            buffData.collectObjectBuffSize,
                            buff.buffId,
                            0);
                        itemInfo = itemInfo.AddState(ItemState.IsActive);
                        _serverItemMap.Add(id, itemInfo);
                    }
                    //Debug.Log($"Calculated {spawnedCount} spawn positions");
                    await UniTask.Yield();
                }

                // 在服务器端生成物品
                // foreach (var info in _serverItemMap.Values)
                // {
                //     // var spawnedObject = GameObjectPoolManger.Instance.GetObject(
                //     //     prefab.gameObject, 
                //     //     info.Position.ToVector3(), 
                //     //     Quaternion.identity, 
                //     //     _spawnedParent,
                //     //     go => _gameMapInjector.InjectGameObject(go)
                //     // );
                //     // NetworkServer.Spawn(spawnedObject); // 确保网络同步
                //     // var component = spawnedObject.GetComponent<CollectObjectController>();
                //     //
                //     // var networkIdentity = spawnedObject.GetComponent<NetworkIdentity>();
                //     
                //     // component.CollectId = info.id;
                //     // if (component.CollectObjectData.collectObjectClass == CollectObjectClass.Buff)
                //     // {
                //     //     //Debug.Log($"Spawning buff item at position: {info.position} with id: id-{info.id} netId-{networkIdentity.netId} configId-{info.collectConfigId} buff.propertyType-{buff.propertyType} buffId-{buff.buffId} buffSize-{info.buffSize} buffPropertyType-{buff.propertyType}");
                //     //     component.SetBuffData(buffData);
                //     //     component.SetMaterial(_collectibleMaterials[configData.buffSize][buff.propertyType]);
                //     // }
                //     // _spawnedItems.Add(info.id, new SpawnItemInfo
                //     // {
                //     //     id = info.id,
                //     //     netId = networkIdentity.netId,
                //     //     collectConfigId = info.collectConfigId,
                //     //     position = info.position,
                //     //     buffExtraData = info.buffExtraData,
                //     // });
                // }
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
            if (configData.isRandomBuff)
            {
                var propertyTypeEnum = PropertyTypeEnum.Score;
                while (propertyTypeEnum == PropertyTypeEnum.Score)
                {
                    propertyTypeEnum = (PropertyTypeEnum)Random.Range((int)PropertyTypeEnum.Speed, (int)PropertyTypeEnum.CriticalDamageRatio + 1);
                }
                var buff = _randomBuffConfig.GetCollectBuff(propertyTypeEnum);
                return buff;
            }

            return configData.buffExtraData;
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
        private void SpawnManyItemsClientRpc(CollectItemMetaData[] allSpawnedItems)
        {
            SpawnItems(allSpawnedItems);
        }

        private void SpawnItems(CollectItemMetaData[] allSpawnedItems)
        {
            foreach (var info in allSpawnedItems)
            {
                var position = info.Position;
                var pos = position.ToVector3();
                Debug.Log($"Client spawning item at position: {pos}"); // 添加日志
                var prefab = GetPrefabByCollectType(info.ItemConfigId);
                if (!prefab)
                {
                    Debug.LogError($"Failed to find prefab for CollectType: {info.ItemConfigId}");
                    continue;
                }

                var go = GameObjectPoolManger.Instance.GetObject(prefab.gameObject, pos, Quaternion.identity, _spawnedParent,
                    go => _gameMapInjector.InjectGameObject(go));
                //var networkIdentity = go.GetComponent<NetworkIdentity>();
                if (!go)
                {
                    Debug.LogError("Failed to get object from pool");
                    continue;
                }
                var configData = _collectObjectDataConfig.GetCollectObjectData(info.ItemConfigId);
                var buff = _randomBuffConfig.GetRandomBuffData(info.RandomBuffId);
                var component = go.GetComponent<CollectObjectController>();
                var material = _collectibleMaterials[configData.buffSize][buff.propertyType];
                component.CollectId = (int)info.ItemId;
                if (component.CollectObjectData.collectObjectClass == CollectObjectClass.Buff)
                {
                    component.SetBuffData(new BuffExtraData
                    {
                        buffId = buff.buffId,
                        buffType = BuffType.Random,
                        collectObjectBuffSize = info.BuffSize,
                    });
                    component.SetMaterial(material);
                }
                Debug.Log($"Spawning item at position: {pos} with id: {info.ItemId}-{info.OwnerId}-{info.ItemConfigId}-{buff.propertyType}-{buff.buffId}-{info.BuffSize}-{material.name}");
        
                // 确保位置正确设置
                go.transform.position = pos;
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

            return PlaceItems(collectTypes);
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
            while (remainingWeight > 0)
            {
                var scoreItem = GetRandomItem(CollectObjectClass.Score);
                if (scoreItem != -1)
                {
                    var configData = _collectObjectDataConfig.GetCollectObjectData(scoreItem);
                    itemsToSpawn.Add(scoreItem);
                    remainingWeight -= configData.weight;
                    scoreCount++;
                    count++;
                }

                if (count == 2 && remainingWeight > 0)
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

            if (scoreCount > 0 && remainingWeight > 0)
            {
                var buffItem = GetRandomItem(CollectObjectClass.Score);
                if (buffItem != -1)
                {
                    itemsToSpawn.Add(buffItem);
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
                
                var scoreItem2 = GetRandomItem(CollectObjectClass.Score);
                if (scoreItem2 != -1)
                {
                    var configData = _collectObjectDataConfig.GetCollectObjectData(scoreItem2);
                    scoreItems.Add(scoreItem2);
                    remainingWeight -= configData.weight;
                }
            }

            itemsToSpawn.AddRange(scoreItems);
            itemsToSpawn.AddRange(buffItems);
        }

        private void SpawnMode3(List<int> itemsToSpawn, ref int remainingWeight)
        {
            while (remainingWeight > 0)
            {
                var randomType = Random.Range(0, 1) > 0.66f ? CollectObjectClass.Score : CollectObjectClass.Buff;
                var item = GetRandomItem(randomType);
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
        
        private bool ValidatePickup(CollectObjectController item, PlayerPropertyComponent player)
        {
            // 基本碰撞检测
            var itemCollider = item.Collider;
            var playerCollider = player.GetComponent<Collider>();
            if (!itemCollider.bounds.Intersects(playerCollider.bounds))
                return false;

            return true;
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
                var itemPrefab = _collectiblePrefabs[configId].component;
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

    [Serializable]
    public class CollectibleItemData
    {
        public int id;
        public CollectObjectController component;
        public Vector3 position;
    }

    // [Serializable]
    // public struct SpawnItemInfo
    // {
    //     public int id;
    //     public uint netId;
    //     public int collectConfigId;
    //     public Vector3 position;
    //     public BuffExtraData buffExtraData;
    // }

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
            // Reader<SpawnItemInfo>.read = ReadSpawnedItemDataData;
            // Writer<SpawnItemInfo>.write = WriteSpawnedItemDataData;
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
        
        // private static void WriteSpawnedItemDataData(NetworkWriter writer, SpawnItemInfo info)
        // {
        //     writer.WriteInt(info.id);
        //     writer.WriteInt((int)info.collectConfigId);
        //     writer.WriteUInt(info.netId);
        //     writer.WriteVector3(info.position);
        // }
        //
        // private static SpawnItemInfo ReadSpawnedItemDataData(NetworkReader reader)
        // {
        //     return new SpawnItemInfo
        //     {
        //         id = reader.ReadInt(),
        //         collectConfigId = reader.ReadInt(),
        //         position = reader.ReadVector3(),
        //         netId = reader.ReadUInt()
        //     };
        // }

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