using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Tool.GameEvent;
using Sirenix.OdinInspector;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class MapBoundDefiner : Singleton<MapBoundDefiner>
    {
        private float _safetyMargin = 5.0f;
        private GameObject[] _walls;
        private IConfigProvider _configProvider;
        private JsonDataConfig _jsonDataConfig;
        private LayerMask _sceneLayer;
        private readonly List<Vector2Int> _gridMap = new List<Vector2Int>();
        private readonly Dictionary<Vector2Int, List<Vector3>> _gridGameObjectMap = new Dictionary<Vector2Int, List<Vector3>>();
        // Grid → 地面高度
        private readonly Dictionary<Vector2Int, float> _gridGroundHeight = new Dictionary<Vector2Int, float>();

        // Grid → 是否可生成（是否被静态物体占满）
        private readonly HashSet<Vector2Int> _blockedGrids = new HashSet<Vector2Int>();
        public Vector3 GridOrigin { get; private set; }
        private readonly List<Vector2Int> _spawnableGrids = new List<Vector2Int>();



        private float _gridSize = 2f;
        public float GridSize => _gridSize;
        public Vector3 MapMinBoundary { get; private set; }
        public Vector3 MapMaxBoundary { get; private set; }
        public List<Vector2Int> GridMap => _gridMap;
        private Bounds _mapBounds;

        public Bounds MapBounds
        {
            get
            {
                if (_mapBounds.Equals(default))
                {
                    _mapBounds = new Bounds((MapMinBoundary + MapMaxBoundary)/2, MapMaxBoundary - MapMinBoundary);
                }
                return _mapBounds;
            }
        }

        private IEnumerable<GameObject> Walls => _walls ??= GameObject.FindGameObjectsWithTag("Wall");

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            _configProvider = configProvider;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _safetyMargin = _jsonDataConfig.GameConfig.safetyMargin;
            _gridSize = _jsonDataConfig.GameConfig.gridSize;
            _sceneLayer = _jsonDataConfig.GameConfig.groundSceneLayer;
            Debug.Log("MapBoundDefiner init");
            CalculateAdjustedBounds();
            InitializeGrid();
            CacheGroundHeight();
        }
        
        public Vector3[] GetWaypointPositions(Vector3 position)
        {
            var count = Random.Range(1, 6);
            var positions = new Vector3[count];
            for (var i = 0; i < count; i++)
            {
                var direction = GetRandomDirection();
                positions[i] = position + direction * Random.Range(0f, _safetyMargin);
            }
            return positions;
        }
        
        private void CacheGroundHeight()
        {
            foreach (var grid in _gridMap)
            {
                var center = new Vector3(
                    grid.x * _gridSize + _gridSize * 0.5f,
                    50f,
                    grid.y * _gridSize + _gridSize * 0.5f
                );

                if (Physics.Raycast(center, Vector3.down, out var hit, 100f, _sceneLayer))
                {
                    _gridGroundHeight[grid] = hit.point.y;
                }
            }
        }


        public HashSet<Vector2Int> GetBoundsCovered(Bounds bounds)
        {
            var coveredGrids = new HashSet<Vector2Int>();
        
            // 计算边界框在XZ平面上的覆盖范围
            Vector3 min = bounds.min;
            Vector3 max = bounds.max;
        
            // 获取覆盖的网格范围
            int minGridX = Mathf.FloorToInt((min.x - GridOrigin.x) / _gridSize);
            int maxGridX = Mathf.FloorToInt((max.x - GridOrigin.x) / _gridSize);
            int minGridZ = Mathf.FloorToInt((min.z - GridOrigin.z) / _gridSize);
            int maxGridZ = Mathf.FloorToInt((max.z - GridOrigin.z) / _gridSize);
        
            // 遍历所有覆盖的网格
            for (int x = minGridX; x <= maxGridX; x++)
            {
                for (int z = minGridZ; z <= maxGridZ; z++)
                {
                    coveredGrids.Add(new Vector2Int(x, z));
                }
            }
        
            return coveredGrids;
        }

        public Vector2Int GetGridPosition(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt((worldPos.x - GridOrigin.x) / GridSize),
                Mathf.FloorToInt((worldPos.z - GridOrigin.z) / GridSize)
            );
        }

        
        public Vector2Int FindClosestGrid(Vector3 worldPos)
        {
            // 先计算出理论上的网格坐标
            Vector2Int theoreticalGrid = new Vector2Int(
                Mathf.FloorToInt(worldPos.x / GridSize),
                Mathf.FloorToInt(worldPos.z / GridSize)
            );
    
            // 如果_gridMap包含这个网格，直接返回
            if (_gridMap.Contains(theoreticalGrid))
            {
                return theoreticalGrid;
            }
    
            // 否则，找到最接近的有效网格
            Vector2Int closestGrid = _gridMap[0];
            float minDistance = float.MaxValue;
    
            foreach (var grid in _gridMap)
            {
                // 计算网格中心点位置
                Vector3 gridCenter = new Vector3(
                    grid.x * GridSize + GridSize * 0.5f,
                    0,
                    grid.y * GridSize + GridSize * 0.5f
                );
        
                // 计算与世界位置的距离
                float distance = Vector3.Distance(worldPos, gridCenter);
        
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closestGrid = grid;
                }
            }
    
            return closestGrid;
        }

        private void CalculateAdjustedBounds() 
        {
            var minX = float.MaxValue;
            var maxX = float.MinValue;
            var minZ = float.MaxValue;
            var maxZ = float.MinValue;

            foreach (var wall in Walls) 
            {
                var position = wall.transform.position;
                minX = Mathf.Min(minX, position.x);
                maxX = Mathf.Max(maxX, position.x);
                minZ = Mathf.Min(minZ, position.z);
                maxZ = Mathf.Max(maxZ, position.z);
            }

            // 在原始边界的基础上添加安全边距
            MapMinBoundary = new Vector3(minX + _safetyMargin, 0, minZ + _safetyMargin);
            MapMaxBoundary = new Vector3(maxX - _safetyMargin, 0, maxZ - _safetyMargin);
            GridOrigin = new Vector3(
                MapMinBoundary.x,
                0f,
                MapMinBoundary.z
            );

            Debug.Log($"Adjusted Map Boundaries: Min({MapMinBoundary}) Max({MapMaxBoundary})");
        }
        
        public bool IsWithinMapBounds(Vector3 position) 
        {
            var xMinInMap = position.x >= MapMinBoundary.x;
            var xMaxInMap = position.x <= MapMaxBoundary.x;
            var zMinInMap = position.z >= MapMinBoundary.z;
            var zMaxInMap = position.z <= MapMaxBoundary.z;
            //Debug.Log($"IsWithinMapBounds: xMinInMap-xMaxInMap-zMinInMap-zMaxInMap: {xMinInMap} {xMaxInMap} {zMinInMap} {zMaxInMap}");
            return xMinInMap && xMaxInMap && zMinInMap && zMaxInMap;
        }
        
        private void InitializeGrid()
        {
            _gridMap.Clear();

            int xCount = Mathf.FloorToInt((MapMaxBoundary.x - MapMinBoundary.x) / _gridSize);
            int zCount = Mathf.FloorToInt((MapMaxBoundary.z - MapMinBoundary.z) / _gridSize);

            for (int x = 0; x <= xCount; x++)
            {
                for (int z = 0; z <= zCount; z++)
                {
                    _gridMap.Add(new Vector2Int(x, z));
                }
            }
            _spawnableGrids.Clear();

            foreach (var grid in _gridMap)
            {
                if (_blockedGrids.Contains(grid))
                    continue;

                if (!_gridGroundHeight.ContainsKey(grid))
                    continue;

                _spawnableGrids.Add(grid);
            }

        }

        
        // 获取周围Grid坐标（带边界检查）
        public HashSet<Vector2Int> GetSurroundingGrids(Vector2Int center, int radius)
        {
            var grids = new HashSet<Vector2Int>();
    
            // 先找出所有可能的网格
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    var potentialGrid = new Vector2Int(center.x + x, center.y + y);
            
                    // 检查这个网格是否在有效网格列表中
                    if (_gridMap.Contains(potentialGrid))
                    {
                        grids.Add(potentialGrid);
                    }
                }
            }
    
            return grids;
        }
    
        public Vector3 GetRandomDirection()
        {
            float angle = Random.Range(0, 360);
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
        }
        
        public bool TryGetRandomSpawnPoint(
            out Vector2Int grid,
            Func<Vector2Int, bool> gridFilter = null)
        {
            grid = default;

            if (_spawnableGrids.Count == 0)
                return false;

            // 最多尝试 N 次，N << 原来的失败率
            for (int i = 0; i < 5; i++)
            {
                var candidate = _spawnableGrids[Random.Range(0, _spawnableGrids.Count)];

                if (gridFilter != null && !gridFilter(candidate))
                    continue;

                grid = candidate;
                return true;
            }

            return false;
        }


        [Button]
        public Vector3 GetRandomPoint(Func<Vector3, bool> isObstacle = null)
        {
            var count = 0;
            while (count < 10)
            {
                //Debug.Log($"GetRandomPoint count: {count}");
                if (_gridMap.Count == 0)
                {
                    Debug.LogError("No grid position available.");
                    return Vector3.zero;
                }
                var index = Random.Range(0, _gridMap.Count - 1);
                var gridPos = _gridMap[index];
                var randomX = Random.Range(gridPos.x * _gridSize- _gridSize/2, gridPos.x * _gridSize + _gridSize/2);
                var randomZ = Random.Range(gridPos.y * _gridSize- _gridSize/2, gridPos.y * _gridSize + _gridSize/2);
                var position = new Vector3(randomX, 20, randomZ);
                //Debug.Log($"GetRandomPoint position: {position}");
                if (Physics.Raycast(position, Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
                {
                    var startPoint = new Vector3(randomX, hit.point.y, randomZ);
                    //Debug.Log($"GetRandomPoint startPoint: {startPoint}");
                    if (isObstacle != null)
                    {
                        if (isObstacle(startPoint) && IsWithinMapBounds(startPoint))
                        {
                            //Debug.Log($"GetRandomPoint isObstacle: {isObstacle(startPoint)}");
                            return startPoint;
                        }
                    }
                    if (IsWithinMapBounds(startPoint))
                    {
                        //Debug.Log($"GetRandomPoint IsWithinMapBounds: {IsWithinMapBounds(startPoint)}");
                        return startPoint;
                    }
                }
                count++;
            }
            return Vector3.zero;
        }

        public void Clear()
        {
            _gridMap.Clear();
            _walls = null;
            MapMinBoundary = Vector3.zero;
            MapMaxBoundary = Vector3.zero;
            Debug.Log("MapBoundDefiner cleared");
        }
        public bool TryGetGroundHeight(Vector2Int grid, out float height)
        {
            return _gridGroundHeight.TryGetValue(grid, out height);
        }

        public float GetGroundHeight(Vector2Int grid)
        {
            if (_gridGroundHeight.TryGetValue(grid, out var h))
                return h;

            // 防御性：极端情况下兜底
            //Debug.LogWarning($"No ground height cached for grid {grid}");
            return MapMinBoundary.y;
        }

    }
}