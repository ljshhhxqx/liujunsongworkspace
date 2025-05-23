using System.Collections.Generic;
using HotUpdate.Scripts.Collector;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Map
{
    public class GameStaticObjectContainer : Singleton<GameStaticObjectContainer>
    {
        private readonly Dictionary<int, GameStaticObject> _staticObjects = new Dictionary<int, GameStaticObject>();
        private readonly Dictionary<Vector2Int, List<GameStaticObjectData>> _mapObjectData = new Dictionary<Vector2Int, List<GameStaticObjectData>>();
        
        public void ClientAddStaticObject(GameObject gameObject)
        {
            var staticObject = gameObject.GetComponent<GameStaticObject>();
            _staticObjects.Add(staticObject.Id, staticObject);
        }

        public void ServerAddStaticObject(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            var staticObject = gameObject.GetComponent<GameStaticObject>();
            var position = gameObject.transform.position;
            var grid = MapBoundDefiner.Instance.GetGridPosition(position);
            var colliderConfig = GamePhysicsSystem.CreateColliderConfig(collider);
            var data = new GameStaticObjectData
            {
                Id = staticObject.Id,
                Position = collider.transform.position,
                ColliderConfig = colliderConfig,
                Grid = grid
            };
            if (!_mapObjectData.ContainsKey(grid))
            {
                _mapObjectData.Add(grid, new List<GameStaticObjectData> {data});
            }
            _mapObjectData[grid].Add(data);
        }

        public void ClearStaticObjects()
        {
            _staticObjects.Clear();
            _mapObjectData.Clear();
        }

        public List<int> GetStaticObjectIds(Vector2Int position)
        {
            if (_mapObjectData.TryGetValue(position, out var value))
            {
                return value.ConvertAll(data => data.Id);
            }
            return null;
        }
    }

    public struct GameStaticObjectData
    {
        public int Id;
        public Vector3 Position;
        public IColliderConfig ColliderConfig;
        public Vector2Int Grid;
    }
}