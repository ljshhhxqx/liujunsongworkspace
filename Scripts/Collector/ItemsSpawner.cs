using Collector;
using Config;
using Cysharp.Threading.Tasks;
using Network.Server.Collect;
using System.Collections.Generic;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

public struct Grid
{
    public List<CollectibleItemData> itemIDs;
}

public class ItemSpawner : MonoBehaviour
{
    private List<CollectibleItemData> _collectiblePrefabs = new List<CollectibleItemData>();
    private IConfigProvider _configProvider;
    private LayerMask _sceneLayer;
    private Dictionary<Vector2Int, Grid> _gridMap = new Dictionary<Vector2Int, Grid>();
    private Dictionary<int, CollectibleItemData> _spawnedItems = new Dictionary<int, CollectibleItemData>();
    private int _currentId = 0;
    private const float _itemHeight = 0.5f;
    private const float _itemSpacing = 0.5f;
    private const int _maxGridItems = 10;
    private const float _gridSize = 10f; // Size of each grid cell
    private const int OnceSpawnCount = 10;
    
    private MapBoundDefiner _mapBoundDefiner;

    [Inject]
    private void Init(MapBoundDefiner mapBoundDefiner, IConfigProvider configProvider, GameEventManager gameEventManager)
    {
        gameEventManager.Subscribe<GameResourceLoadedEvent>(OnGameResourceLoaded);
        _sceneLayer = LayerMask.NameToLayer("Scene");
        _configProvider = configProvider;
        _mapBoundDefiner = mapBoundDefiner;
    }

    private void Start()
    {
        InitializeGrid();
    }
    
    private int GenerateID()
    {
        return _currentId++;
    }

    public async UniTask SpawnManyItems()
    {
        _currentId = 0;
        _spawnedItems.Clear();
        _gridMap.Clear();
        var allSpawnedIDs = new List<CollectibleItemData>();
        for (int i = 0; i < OnceSpawnCount; i++)
        {
            var spawnedIDs = SpawnItems(Random.Range(10, 20), Random.Range(1, 4));
            if (spawnedIDs.Count == 0)
            {
                continue;
            }
            allSpawnedIDs.AddRange(spawnedIDs);
            await UniTask.Yield(); // Yield to spread the work over multiple frames
        }

        foreach (var data in allSpawnedIDs)
        {
            
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
        var config = _configProvider.GetConfig<GameDataConfig>();
        
    }

    public List<CollectibleItemData> SpawnItems(int totalWeight, int spawnMode)
    {
        List<CollectibleItemData> itemsToSpawn = new List<CollectibleItemData>();
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
                remainingWeight -= scoreItem.collectObjectData.Weight;
                scoreCount++;
            }

            // Check if we should add a Buff item
            if (remainingWeight <= 0) break;

            var buffItem = GetRandomItem(CollectObjectClass.Buff);
            if (buffItem != null)
            {
                itemsToSpawn.Add(buffItem);
                remainingWeight -= buffItem.collectObjectData.Weight;
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
                remainingWeight -= scoreItem.collectObjectData.Weight;
            }

            var buffItem = GetRandomItem(CollectObjectClass.Buff);
            if (buffItem != null)
            {
                buffItems.Add(buffItem);
                remainingWeight -= buffItem.collectObjectData.Weight;
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
            var randomType = (Random.value > 0.5f) ? CollectObjectClass.Score : CollectObjectClass.Buff;
            var item = GetRandomItem(randomType);
            if (item != null)
            {
                itemsToSpawn.Add(item);
                remainingWeight -= item.collectObjectData.Weight;
            }
        }
    }
    
    private CollectibleItemData GetRandomItem(CollectObjectClass itemClass)
    {
        var filteredItems = _collectiblePrefabs.FindAll(item => item.collectObjectData.CollectObjectClass == itemClass);
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
            var itemCollider = item.component.GetComponent<BoxCollider>();
            var position = startPoint + _itemSpacing * i * direction + Vector3.up * itemCollider.size.y;

            if (!IsPositionValid(position, itemsToSpawn[i]))
            {
                if (Physics.Raycast(new Vector3(position.x, 1000, position.z), Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
                {
                    position.y = hit.point.y + _itemHeight;
                }
            }

            if (IsWithinBoundary(position))
            {
                var id = GenerateID();
                item.Id = id;
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
        while (true)
        {
            var randomX = Random.Range(_mapBoundDefiner.MapMinBoundary.x, _mapBoundDefiner.MapMaxBoundary.x);
            var randomZ = Random.Range(_mapBoundDefiner.MapMinBoundary.z, _mapBoundDefiner.MapMaxBoundary.z);
            var position = new Vector3(randomX, 1000, randomZ);
            if (Physics.Raycast(position, Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
            {
                var startPoint = new Vector3(randomX, hit.point.y + _itemHeight, randomZ);
                if (IsWithinBoundary(startPoint) && IsPositionValid(startPoint, null))
                {
                    return startPoint;
                }
            }
        }
    }
    
    
    private Vector3 GetRandomDirection()
    {
        float angle = Random.Range(0, 360);
        return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
    }

    private bool IsPositionValid(Vector3 position, CollectibleItemData itemPrefab)
    {
        Vector2Int gridPos = GetGridPosition(position);
        if (_gridMap.TryGetValue(gridPos, out Grid grid))
        {
            if (grid.itemIDs.Count > _maxGridItems)
            {
                return false;
            }
            if (itemPrefab != null)
            {
                var itemCollider = itemPrefab.component.GetComponent<BoxCollider>();
                if (itemCollider != null)
                {
                    var hitColliders = Physics.OverlapBox(position, itemCollider.bounds.extents, Quaternion.identity, _sceneLayer);
                    foreach (var hitCollider in hitColliders)
                    {
                        if (hitCollider.gameObject == itemPrefab.component.gameObject)
                        {
                            return false;
                        }
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
    public int Id;
    public CollectObjectData collectObjectData;
    public CollectInteractComponent component;
    public Vector3 position;
}