using System.Collections.Generic;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace Collector
{
    public class MapBoundDefiner
    {
        private float _safetyMargin = 5.0f;
        private GameObject[] _walls;
        private IConfigProvider _configProvider;
        public Vector3 MapMinBoundary { get; private set; }
        public Vector3 MapMaxBoundary { get; private set; }
        
        private IEnumerable<GameObject> Walls => _walls ??= GameObject.FindGameObjectsWithTag("Wall");

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            gameEventManager.Subscribe<GameResourceLoadedEvent>(OnGameResourceLoaded);
            _configProvider = configProvider;
            CalculateAdjustedBounds();
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

            foreach (var wall in Walls) {
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
            if (MapMinBoundary == default || MapMaxBoundary == default)
                CalculateAdjustedBounds();
            return position.x >= MapMinBoundary.x && position.x <= MapMaxBoundary.x &&
                   position.z >= MapMinBoundary.z && position.z <= MapMaxBoundary.z;
        }

    }
}