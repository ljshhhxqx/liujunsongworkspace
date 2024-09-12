using System;
using System.Collections.Generic;
using Tool.GameEvent;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector
{
    public class MapBoundDefiner
    {
        private float _safetyMargin = 5.0f;
        private GameObject[] _walls;
        private IConfigProvider _configProvider;
        private LayerMask _sceneLayer;
        private List<Vector2Int> _gridMap = new List<Vector2Int>();
        private static float _gridSize = 10f;
        public Vector3 MapMinBoundary { get; private set; }
        public Vector3 MapMaxBoundary { get; private set; }
        public List<Vector2Int> GridMap => _gridMap;
        
        private IEnumerable<GameObject> Walls => _walls ??= GameObject.FindGameObjectsWithTag("Wall");

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            gameEventManager.Subscribe<GameResourceLoadedEvent>(OnGameResourceLoaded);
            _configProvider = configProvider;
            _sceneLayer = LayerMask.NameToLayer("Scene");
            CalculateAdjustedBounds();
            InitializeGrid();
        }

        private void OnGameResourceLoaded(GameResourceLoadedEvent gameResourceLoadedEvent)
        {
            var config = _configProvider.GetConfig<GameDataConfig>();
            _safetyMargin = config.GameConfigData.SafetyMargin;
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
            return position.x >= MapMinBoundary.x && position.x <= MapMaxBoundary.x &&
                   position.z >= MapMinBoundary.z && position.z <= MapMaxBoundary.z;
        }

        private Vector2Int GetGridPosition(Vector3 position)
        {
            var x = Mathf.FloorToInt(position.x / _gridSize);
            var z = Mathf.FloorToInt(position.z / _gridSize);
            return new Vector2Int(x, z);
        }
        
        private void InitializeGrid()
        {
            for (var x = MapMinBoundary.x; x <= MapMinBoundary.x; x += _gridSize)
            {
                for (var z = MapMinBoundary.z; z <= MapMinBoundary.z; z += _gridSize)
                {
                    var gridPos = GetGridPosition(new Vector3(x, 0, z));
                    _gridMap.Add(gridPos);
                }
            }
        }
    
        public Vector3 GetRandomDirection()
        {
            float angle = Random.Range(0, 360);
            return new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)).normalized;
        }
        
        public Vector3 GetRandomPoint(Func<Vector3, bool> isObstacle = null)
        {
            while (true)
            {
                if (_gridMap.Count == 0)
                {
                    Debug.LogError("No grid position available.");
                    return Vector3.zero;
                }
                var index = Random.Range(0, _gridMap.Count - 1);
                var gridPos = _gridMap[index];
                var randomX = Random.Range(gridPos.x * _gridSize- _gridSize/2, gridPos.x * _gridSize + _gridSize/2);
                var randomZ = Random.Range(gridPos.y * _gridSize- _gridSize/2, gridPos.y * _gridSize + _gridSize/2);
                var position = new Vector3(randomX, 1000, randomZ);
                if (Physics.Raycast(position, Vector3.down, out var hit, Mathf.Infinity, _sceneLayer))
                {
                    var startPoint = new Vector3(randomX, hit.point.y, randomZ);
                    if (IsWithinMapBounds(startPoint) && isObstacle != null && isObstacle(startPoint))
                    {
                        return startPoint;
                    }
                }
            }
        }
    }
}