using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using Config;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Network.Server.Collect;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class ItemsSpawnerManager : NetworkMonoComponent
    {
        public struct Grid
        {
            public List<CollectibleItemData> itemIDs;
        }
        private List<CollectibleItemData> _collectiblePrefabs = new List<CollectibleItemData>();
        private IConfigProvider _configProvider;
        private MapBoundDefiner _mapBoundDefiner;
        private MessageCenter _messageCenter;
        private LayerMask _sceneLayer;
        private Dictionary<Vector2Int, Grid> _gridMap = new Dictionary<Vector2Int, Grid>();
        private Dictionary<int, CollectibleItemData> _spawnedItems = new Dictionary<int, CollectibleItemData>();
        private int _currentId;
        private static float _itemSpacing = 0.5f;
        private static int _maxGridItems = 10;
        private static float _itemHeight = 1f;
        private static float _gridSize = 10f; // Size of each grid cell
        private static int _onceSpawnCount = 10;
        private Transform _spawnedParent;
    
        public int CurrentRound { get; set; }

        [Inject]
        private void Init(MapBoundDefiner mapBoundDefiner, IConfigProvider configProvider, GameEventManager gameEventManager, MessageCenter messageCenter)
        {
            gameEventManager.Subscribe<GameResourceLoadedEvent>(OnGameResourceLoaded);
            _messageCenter = messageCenter;
            _messageCenter.Register<PlayerTouchedCollectMessage>(OnPlayerTouchedCollect);
            _sceneLayer = LayerMask.NameToLayer("Scene");
            _configProvider = configProvider;
            _mapBoundDefiner = mapBoundDefiner;
            _spawnedParent = transform;
            InitializeGrid();
        }

        private void OnPlayerTouchedCollect(PlayerTouchedCollectMessage message)
        {
            if (_spawnedItems.ContainsKey(message.CollectID))
            {
                _spawnedItems.Remove(message.CollectID);
                if (_spawnedItems.Count == 0)
                {
                    _messageCenter.Post(new CollectObjectsEmptyMessage(CurrentRound));
                }
            }
        }

        private int GenerateID()
        {
            return _currentId++;
        }

        public async UniTask SpawnManyItems(string mapName)
        {
            _currentId = 0;
            _spawnedItems.Clear();
            _gridMap.Clear();
            _gridMap = _mapBoundDefiner.GridMap.ToDictionary(x => x,_ => new Grid());
            if (_collectiblePrefabs.Count > 0)
            {
                var res = ResourceManager.Instance.GetMapCollectObject(mapName);
                _collectiblePrefabs = res.Select(x => new CollectibleItemData
                {
                    component = x.GetComponent<CollectObjectController>(),
                }).ToList();
            }
            var allSpawnedItems = new List<CollectibleItemData>();
            for (int i = 0; i < _onceSpawnCount; i++)
            {
                var spawnedItems = SpawnItems(Random.Range(10, 20), Random.Range(1, 4));
                if (spawnedItems.Count == 0)
                {
                    continue;
                }
                allSpawnedItems.AddRange(spawnedItems);
                await UniTask.Yield(); // Yield to spread the work over multiple frames
            }

            foreach (var data in allSpawnedItems)
            {
                var go = GameObjectPoolManger.Instance.GetObject(data.component.gameObject, data.position, Quaternion.identity, _spawnedParent);
                if (go.GetComponentInChildren<CollectParticlePlayer>() == null)
                {
                    // var particlePlayer = ResourceManager.Instance.GetResource<GameObject>()
                    //await ResourceManager.Instance.LoadResourceAsync<GameObject>();
                }
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
            int remainingWeight = totalWeight;

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
                int randomIndex = Random.Range(0, filteredItems.Count);
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
    
        private Vector3 GetRandomStartPoint()
        {
            bool IsPositionValidWithoutItem(Vector3 position)
            {
                return IsPositionValid(position, null);
            }

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

    public class CollectibleItemData
    {
        public int id;
        public CollectObjectController component;
        public Vector3 position;
    }
}