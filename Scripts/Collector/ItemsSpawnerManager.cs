using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using Collector;
using Config;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Game;
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
        public struct Grid
        {
            public List<CollectibleItemData> itemIDs;
        }
        private List<CollectibleItemData> _collectiblePrefabs = new List<CollectibleItemData>();
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
        private SyncDictionary<int, CollectibleItemData> _spawnedItems = new SyncDictionary<int, CollectibleItemData>();
        private static float _itemSpacing = 0.5f;
        private static int _maxGridItems = 10;
        private static float _itemHeight = 1f;
        private static float _gridSize = 10f; // Size of each grid cell
        private static int _onceSpawnCount = 50;
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
            _messageCenter.Register<PickerPickUpChestMessage>(OnPickerPickUpChestMessage);
            gameEventManager.Subscribe<GameResourceLoadedEvent>(OnGameResourceLoaded);
            _collectObjectDataConfig = _configProvider.GetConfig<CollectObjectDataConfig>();
            _buffDatabase = _configProvider.GetConfig<BuffDatabase>();
            var config = _configProvider.GetConfig<GameDataConfig>();
            _chestConfig = _configProvider.GetConfig<ChestDataConfig>();
            _sceneLayer = config.GameConfigData.GroundSceneLayer;
            _messageCenter.Register<GameStartMessage>(OnGameStart);
            _messageCenter.Register<PickerPickUpMessage>(OnPickUpItem);
            _gameLoopController = FindObjectOfType<GameLoopController>();
            _spawnedParent = transform;
            _spawnedItems.OnChange += OnSpawnItemsChange;
            CollectItemReaderWriter.RegisterReaderWriter();
            InitializeGrid();
        }

        private void OnPickerPickUpChestMessage(PickerPickUpChestMessage message)
        {
            if (isServer)
            {
                var player = _playerInGameManager.GetPlayerPropertyComponent(message.PickerId);
                if (player.PlayerState == PlayerState.Dead)
                {
                    return; 
                }
                var configData = _chestConfig.GetChestConfigData(player.CurrentChestType);
                player.CurrentChestType = _treasureChest.ChestType;
                if(configData.ChestPropertyData.BuffExtraData.buffType != BuffType.None)
                    _buffManager.AddBuffToPlayer(player, configData.ChestPropertyData.BuffExtraData);
                PickUpTreasureChest();
                RpcPickUpTreasureChest();
                JudgeEndRound();
            }
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
                GameObjectPoolManger.Instance.ReturnObject(_treasureChest.gameObject);
                _treasureChest = null;
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

        private void OnSpawnItemsChange(SyncIDictionary<int, CollectibleItemData>.Operation operation, int id, CollectibleItemData data)
        {
            switch (operation)
            {
                case SyncIDictionary<int, CollectibleItemData>.Operation.OP_ADD:
                    Debug.Log($"Spawned item {id} {data.component.gameObject.name}");
                    break;
                case SyncIDictionary<int, CollectibleItemData>.Operation.OP_CLEAR:
                    Debug.Log("Clear all spawned items");
                    break;
                case SyncIDictionary<int, CollectibleItemData>.Operation.OP_REMOVE:
                    if (_spawnedItems.Count == 0)
                    {
                        if (isServer)
                        {
                            _gameLoopController.IsEndRound = true;
                        }
                    }
                    Debug.Log($"Remove item {id} {data.component.gameObject.name}");
                    break;
                case SyncIDictionary<int, CollectibleItemData>.Operation.OP_SET:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
            }
        }

        private void OnGameStart(GameStartMessage message)
        {
            _gridMap.Clear();
            _gridMap = _mapBoundDefiner.GridMap.ToDictionary(x => x,_ => new Grid());
            var res = ResourceManager.Instance.GetMapCollectObject(message.GameInfo.SceneName);
            if (_collectiblePrefabs.Count > 0)
            {
                _collectiblePrefabs = res.Select(x => new CollectibleItemData
                {
                    component = x.GetComponent<CollectObjectController>(),
                }).ToList();
            }

            if (!_treasureChestPrefab)
            {
                _treasureChestPrefab = res.Find(x => x.GetComponent<TreasureChestComponent>()!= null).GetComponent<TreasureChestComponent>();
            }
        }

        [Server]
        public void HandleItemPickup(int itemId, int pickerId)
        {
            if (_spawnedItems.TryGetValue(itemId, out var item))
            {
                if (CanPickup(item.component, pickerId))
                {
                    _spawnedItems.Remove(itemId);
                    GameObjectPoolManger.Instance.ReturnObject(item.component.gameObject);
                    var configData = _collectObjectDataConfig.GetCollectObjectData(item.component.CollectData.CollectType);
                    var player = _playerInGameManager.GetPlayerPropertyComponent(pickerId);
                    switch (configData.CollectObjectClass)
                    {
                        case CollectObjectClass.Score:
                            var buff = _buffDatabase.GetBuff(configData.BuffExtraData);
                            player.IncreaseProperty(PropertyTypeEnum.Score, buff.increaseDataList);
                            break;
                        case CollectObjectClass.Buff:
                            _buffManager.AddBuffToPlayer(player, configData.BuffExtraData);
                            break;
                        case CollectObjectClass.TreasureChest:
                            // TODO: Treasure Chest
                            break;
                        default:
                            throw new Exception("Invalid collect object class");
                    }
                    RpcPickupItem(item.component);
                }
            }
        }

        [ClientRpc]
        private void RpcPickupItem(CollectObjectController item)
        {
            item.CollectSuccess();
            GameObjectPoolManger.Instance.ReturnObject(item.gameObject);
        }

        private bool CanPickup(CollectObjectController item, int connectionId)
        {
            var player = _playerInGameManager.GetPlayerPropertyComponent(connectionId);
            // 获取 item 的 Collider
            var itemCollider = item.GetComponent<Collider>();
            var playerCollider = player.GetComponent<Collider>();
            //NetworkIdentity.GetSceneIdentity(connectionId);
            // 判定 item 的 collider 是否与 player 的 collider 重叠
            return itemCollider.bounds.Intersects(playerCollider.bounds);
        }


        private int GenerateID()
        {
            return _currentId++;
        }

        [Server]
        private void JudgeEndRound()
        {
            var endRound = _spawnedItems.Count == 0 && _treasureChest == null;
            if (endRound)
            {
                _gameLoopController.IsEndRound = true;
            }
        }

        [Server]
        public async UniTask SpawnItemsAndChest()
        {
            await SpawnManyItems();
            SpawnTreasureChestServer();
        }

        [Server]
        public async UniTask SpawnManyItems()
        {
            _spawnedItems.Clear();
            _currentId = 0;
            var allSpawnedItems = new List<CollectibleItemData>();
            var spawnedCount = 0;
            while (spawnedCount < _onceSpawnCount)
            {
                var spawnedItems = SpawnItems(Mathf.RoundToInt(Random.Range(20f, 40f)), Random.Range(1, 4));
                if (spawnedItems.Count == 0)
                {
                    continue;
                }
                spawnedCount += spawnedItems.Count;
                allSpawnedItems.AddRange(spawnedItems);
                await UniTask.Yield();
            }
            _spawnedItems = new SyncDictionary<int, CollectibleItemData>(allSpawnedItems.ToDictionary(x => GenerateID(), x => x));
            SpawnItems(allSpawnedItems);
            SpawnManyItemsClientRpc(allSpawnedItems);
        }

        [Server]
        public void SpawnTreasureChestServer()
        {
            var chestType = (ChestType)Mathf.RoundToInt(Random.Range(0f, (float)ChestType.Score));
            var position = GetRandomStartPoint();
            SpawnTreasureChest(chestType, position);
            RpcSpawnTreasureChest(chestType, position);
        }

        [ClientRpc]
        public void RpcSpawnTreasureChest(ChestType chestType, Vector3 position)
        {
            SpawnTreasureChest(chestType, position);
        }

        private void SpawnTreasureChest(ChestType chestType, Vector3 position)
        {
            if (_treasureChest)
            {
                GameObjectPoolManger.Instance.ReturnObject(_treasureChest.gameObject);
            }
            _treasureChest = GameObjectPoolManger.Instance.GetObject(_treasureChestPrefab.gameObject, position, Quaternion.identity, _spawnedParent).GetComponent<TreasureChestComponent>();
            _treasureChest.ChestType = chestType;
        }

        [ClientRpc]
        private void SpawnManyItemsClientRpc(List<CollectibleItemData> allSpawnedItems)
        {
            SpawnItems(allSpawnedItems);
        }

        private void SpawnItems(List<CollectibleItemData> allSpawnedItems)
        {
            foreach (var data in allSpawnedItems)
            {
                var go = GameObjectPoolManger.Instance.GetObject(data.component.gameObject, data.position, Quaternion.identity, _spawnedParent);
                var component = go.GetComponent<CollectObjectController>();
                component.CollectId = data.id;
            }
        }

        private void InitializeGrid()
        {
            for (var x = _mapBoundDefiner.MapMinBoundary.x; x <= _mapBoundDefiner.MapMinBoundary.x; x += _gridSize)
            {
                for (var z = _mapBoundDefiner.MapMinBoundary.z; z <= _mapBoundDefiner.MapMinBoundary.z; z += _gridSize)
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

        private void OnGameResourceLoaded(GameResourceLoadedEvent gameResourceLoadedEvent)
        {
            var config = _configProvider.GetConfig<CollectObjectDataConfig>();
            _itemSpacing = config.CollectData.ItemSpacing;
            _maxGridItems = config.CollectData.MaxGridItems;
            _itemHeight = config.CollectData.ItemHeight;
            _gridSize = config.CollectData.GridSize;
        }

        private List<CollectibleItemData> SpawnItems(int totalWeight, int spawnMode)
        {
            var itemsToSpawn = new List<CollectibleItemData>();
            var remainingWeight = totalWeight;

            switch (spawnMode)
            {
                case 1:
                    SpawnMode1(itemsToSpawn, ref remainingWeight);
                    break;
                case 2:
                    SpawnMode2(itemsToSpawn, ref remainingWeight);
                    break;
                case 3:
                    SpawnMode3(itemsToSpawn, ref remainingWeight);
                    break;
            }

            return PlaceItems(itemsToSpawn);
        }

        private void SpawnMode1(List<CollectibleItemData> itemsToSpawn, ref int remainingWeight)
        {
            var scoreCount = 0;
            while (remainingWeight > 0)
            {
                // Generate Score item
                var scoreItem = GetRandomItem(CollectObjectClass.Score);
                if (scoreItem != null)
                {
                    itemsToSpawn.Add(scoreItem);
                    remainingWeight -= scoreItem.component.CollectData.Weight;
                    scoreCount++;
                }

                // Check if we should add a Buff item
                if (remainingWeight <= 0) break;

                var buffItem = GetRandomItem(CollectObjectClass.Buff);
                if (buffItem != null)
                {
                    itemsToSpawn.Add(buffItem);
                    remainingWeight -= buffItem.component.CollectData.Weight;
                    break; // Buff item added last
                }
            }

            // If no Buff item was added, ensure one is added at the end
            if (scoreCount > 0 && remainingWeight > 0)
            {
                var buffItem = GetRandomItem(CollectObjectClass.Buff);
                if (buffItem != null)
                {
                    itemsToSpawn.Add(buffItem);
                }
            }
        }

        private void SpawnMode2(List<CollectibleItemData> itemsToSpawn, ref int remainingWeight)
        {
            var scoreItems = new List<CollectibleItemData>();
            var buffItems = new List<CollectibleItemData>();

            while (remainingWeight > 0)
            {
                var scoreItem = GetRandomItem(CollectObjectClass.Score);
                if (scoreItem != null)
                {
                    scoreItems.Add(scoreItem);
                    remainingWeight -= scoreItem.component.CollectData.Weight;
                }

                var buffItem = GetRandomItem(CollectObjectClass.Buff);
                if (buffItem != null)
                {
                    buffItems.Add(buffItem);
                    remainingWeight -= buffItem.component.CollectData.Weight;
                }

                if (scoreItems.Count + buffItems.Count >= 100) break;
            }

            // Combine and order items based on weights
            itemsToSpawn.AddRange(scoreItems);
            itemsToSpawn.AddRange(buffItems);
        }

        private void SpawnMode3(List<CollectibleItemData> itemsToSpawn, ref int remainingWeight)
        {
            while (remainingWeight > 0)
            {
                var randomType = (Random.Range(0, 1) > 0.5f) ? CollectObjectClass.Score : CollectObjectClass.Buff;
                var item = GetRandomItem(randomType);
                if (item != null)
                {
                    itemsToSpawn.Add(item);
                    remainingWeight -= item.component.CollectData.Weight;
                }
            }
        }
    
        private CollectibleItemData GetRandomItem(CollectObjectClass itemClass)
        {
            var filteredItems = _collectiblePrefabs.FindAll(item => item.component.CollectData.CollectObjectClass == itemClass);
            if (filteredItems.Count > 0)
            {
                var randomIndex = Random.Range(0, filteredItems.Count);
                return filteredItems[randomIndex];
            }
            return null;
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
                    _spawnedItems[id] = item;

                    var gridPos = GetGridPosition(position);
                    if (_gridMap.TryGetValue(gridPos, out Grid grid))
                    {
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
            if (_gridMap.TryGetValue(gridPos, out Grid grid))
            {
                if (grid.itemIDs.Count > _maxGridItems)
                {
                    return false;
                }
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

    public static class CollectItemReaderWriter
    {
        public static void RegisterReaderWriter()
        {
            Reader<CollectibleItemData>.read = ReadCollectibleItemDataData;
            Writer<CollectibleItemData>.write= WriteCollectibleItemDataData;
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