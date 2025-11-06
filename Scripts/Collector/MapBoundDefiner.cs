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
        }

        public Vector2Int GetGridPosition(Vector3 worldPos)
        {
            return new Vector2Int(
                Mathf.FloorToInt(worldPos.x / GridSize),
                Mathf.FloorToInt(worldPos.z / GridSize) // 假设使用XZ平面
            );
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
            for (var x = MapMinBoundary.x; x <= MapMaxBoundary.x; x += _gridSize)
            {
                for (var z = MapMinBoundary.z; z <= MapMaxBoundary.z; z += _gridSize)
                {
                    var gridPos = GetGridPosition(new Vector3(x, 0, z));
                    _gridMap.Add(gridPos);
                }
            }
        }
        
        // 获取周围Grid坐标（带边界检查）
        public HashSet<Vector2Int> GetSurroundingGrids(Vector2Int center, int radius)
        {
            var grids = new HashSet<Vector2Int>();
            for (var x = -radius; x <= radius; x++)
            {
                for (var y = -radius; y <= radius; y++)
                {
                    var grid = new Vector2Int(
                        Mathf.Clamp(center.x + x, 0, _gridMap.Count - 1),
                        Mathf.Clamp(center.y + y, 0, _gridMap.Count - 1)
                    );
                    grids.Add(grid);
                }
            }
            return grids;
        }
    
        public Vector3 GetRandomDirection()
        {
            float angle = Random.Range(0, 360);
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
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
    }
}