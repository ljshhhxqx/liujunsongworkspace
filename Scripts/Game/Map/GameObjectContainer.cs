using System;
using System.Collections.Generic;
using System.Threading;
using HotUpdate.Scripts.Collector;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Map
{
    public class GameObjectContainer : Singleton<GameObjectContainer>
    {
        private readonly Dictionary<int, GameObject> _idToGameObject = new Dictionary<int, GameObject>();
        private readonly Dictionary<Vector2Int, List<GameObjectData>> _mapObjectData = new Dictionary<Vector2Int, List<GameObjectData>>();

        private Dictionary<uint, DynamicObjectData> _netIdToDynamicObjectData = new Dictionary<uint, DynamicObjectData>();

        public void UpdateDynamicObjects(bool isServer)
        {
            foreach (var data in _netIdToDynamicObjectData)
            {
                var identity = isServer ? NetworkServer.spawned[data.Key] : NetworkClient.spawned[data.Key];
                if (!identity)
                {
                    RemoveDynamicObject(data.Key);
                }
                else
                {
                    data.Value.Position = identity.transform.position;
                }
            }
        }

        public bool DynamicObjectIntersects(Vector3 position, IColliderConfig colliderConfig, HashSet<DynamicObjectData> intersectedObjects)
        {
            intersectedObjects.Clear();
            var bounds = GamePhysicsSystem.GetWorldBounds(position, colliderConfig);
            var coveredGrids = MapBoundDefiner.Instance.GetBoundsCovered(bounds);
            if (coveredGrids.Count == 0)
            {
                Debug.LogWarning("No covered grids found for " + position);
                return false;
            }
            foreach (var grid in coveredGrids)
            {
                foreach (var data in _netIdToDynamicObjectData)
                {
                    var gridBounds = MapBoundDefiner.Instance.GetGridPosition(data.Value.Position);

                    if (gridBounds == grid)
                    {
                        if (GamePhysicsSystem.FastCheckItemIntersects(position, data.Value.Position, colliderConfig,
                                data.Value.ColliderConfig))
                        {
                            intersectedObjects.Add(data.Value);
                        }
                    }
                }
            }

            return intersectedObjects.Count > 0;
        }

        public bool DynamicObjectIntersects(Vector3 position, IColliderConfig colliderConfig)
        {
            var bounds = GamePhysicsSystem.GetWorldBounds(position, colliderConfig);
            var coveredGrids = MapBoundDefiner.Instance.GetBoundsCovered(bounds);
            if (coveredGrids.Count == 0)
            {
                Debug.LogWarning("No covered grids found for " + position);
                return false;
            }
            foreach (var grid in coveredGrids)
            {
                foreach (var data in _netIdToDynamicObjectData)
                {
                    var gridBounds = MapBoundDefiner.Instance.GetGridPosition(data.Value.Position);

                    if (gridBounds == grid)
                    {
                        if (GamePhysicsSystem.FastCheckItemIntersects(position, data.Value.Position, colliderConfig,
                                data.Value.ColliderConfig))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public void AddDynamicObject(uint netId, Vector3 position, IColliderConfig colliderConfig, ObjectType type, int layer)
        {
            var data = new DynamicObjectData
            {
                NetId = netId,
                Position = position,
                ColliderConfig = colliderConfig,
                Type = type,
                Layer = layer
            };
            _netIdToDynamicObjectData.TryAdd(netId, data);
        }

        public void RemoveDynamicObject(uint netId)
        {
            _netIdToDynamicObjectData.Remove(netId);
        }

        public DynamicObjectData GetDynamicObjectData(uint netId)
        {
            _netIdToDynamicObjectData.TryGetValue(netId, out var data);
            return data;
        }
        
        public bool IsIntersect(Vector3 position, IColliderConfig colliderConfig)
        {
            var bounds = GamePhysicsSystem.GetWorldBounds(position, colliderConfig);
            var coveredGrids = MapBoundDefiner.Instance.GetBoundsCovered(bounds);
            if (coveredGrids.Count == 0)
            {
                Debug.LogWarning("No covered grids found for " + position);
                return false;
            }
            foreach (var grid in coveredGrids)
            {
                if (_mapObjectData.TryGetValue(grid, out var value))
                {
                    foreach (var data in value)
                    {
                        if (GamePhysicsSystem.FastCheckItemIntersects(position, data.Position, colliderConfig,
                                data.ColliderConfig))
                        {
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        public bool IsIntersect(Vector3 position, IColliderConfig colliderConfig, HashSet<GameObjectData> intersectedObjects)
        {
            intersectedObjects.Clear();
            var bounds = GamePhysicsSystem.GetWorldBounds(position, colliderConfig);
            var coveredGrids = MapBoundDefiner.Instance.GetBoundsCovered(bounds);
            if (coveredGrids.Count == 0)
            {
                Debug.LogWarning("No covered grids found for " + position);
                return false;
            }
            foreach (var grid in coveredGrids)
            {
                if (_mapObjectData.TryGetValue(grid, out var value))
                {
                    foreach (var data in value)
                    {
                        if (GamePhysicsSystem.FastCheckItemIntersects(position, data.Position, colliderConfig,
                                data.ColliderConfig))
                        {
                            intersectedObjects.Add(data);
                        }
                    }
                }
            }
            return intersectedObjects.Count > 0;
        }

        public void AddStaticObject(GameObject gameObject)
        {
            var collider = gameObject.GetComponent<Collider>();
            var staticObject = gameObject.GetComponent<GameStaticObject>();
            var position = gameObject.transform.position;
            var grid = MapBoundDefiner.Instance.GetGridPosition(position);
            var colliderConfig = GamePhysicsSystem.CreateColliderConfig(collider);
            var data = new GameObjectData
            {
                Id = staticObject.Id,
                Position = collider.transform.position,
                ColliderConfig = colliderConfig,
                Grid = grid,
                Layer = gameObject.layer,
                Tag = gameObject.tag
            };
            if (!_mapObjectData.ContainsKey(grid))
            {
                _mapObjectData.Add(grid, new List<GameObjectData> {data});
            }
            _mapObjectData[grid].Add(data);
            _idToGameObject.TryAdd(staticObject.Id, gameObject);
        }

        public void ClearObjects()
        {
            _mapObjectData.Clear();
            _idToGameObject.Clear();
            _netIdToDynamicObjectData.Clear();
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

    public enum ObjectType
    {
        SceneObject,
        Base,
        Player,
        Collectable,
        Bullet,
        Chest
    }

    public class DynamicObjectData
    {
        public uint NetId;
        public Vector3 Position;
        public IColliderConfig ColliderConfig;
        public ObjectType Type;
        public LayerMask Layer;
        
        public override string ToString()
        {
            
            return $"NetId: {NetId}, Position: {Position}, ColliderConfig: {ColliderConfig}";
        }
    }

    public struct GameObjectData : IEquatable<GameObjectData>
    {
        public int Id;
        public Vector3 Position;
        public IColliderConfig ColliderConfig;
        public Vector2Int Grid;
        public int Layer;
        public string Tag;

        public bool Equals(GameObjectData other)
        {
            return Id == other.Id && Position.Equals(other.Position) && Equals(ColliderConfig, other.ColliderConfig) 
                   && Layer == other.Layer && Tag == other.Tag && Grid.Equals(other.Grid);
        }

        public override bool Equals(object obj)
        {
            return obj is GameObjectData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Position, ColliderConfig, Grid, Layer, Tag);
        }

        public static bool operator ==(GameObjectData left, GameObjectData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameObjectData left, GameObjectData right)
        {
            return !left.Equals(right);
        }

        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("Id: ").Append(Id).Append(", Position: ").Append(Position).Append(", ColliderConfig: ").
                Append(ColliderConfig).Append(", Grid: ").Append(Grid)
                .Append(", Layer: ").Append(Layer).Append(", Tag: ").Append(Tag);
            return sb.ToString();
        }
    }
}