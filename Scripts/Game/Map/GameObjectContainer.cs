using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Collector;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Map
{
    public class GameObjectContainer : Singleton<GameObjectContainer>
    {
        private readonly Dictionary<int, GameObject> _idToGameObject = new Dictionary<int, GameObject>();
        private readonly Dictionary<Vector2Int, List<GameStaticObjectData>> _mapObjectData = new Dictionary<Vector2Int, List<GameStaticObjectData>>();

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

        public bool IsIntersect(Vector3 position, IColliderConfig colliderConfig, out HashSet<GameStaticObjectData> intersectedObjects)
        {
            intersectedObjects = new HashSet<GameStaticObjectData>(5);
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
            var data = new GameStaticObjectData
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
                _mapObjectData.Add(grid, new List<GameStaticObjectData> {data});
            }
            _mapObjectData[grid].Add(data);
            _idToGameObject.Add(staticObject.Id, gameObject);
        }

        public void ClearStaticObjects()
        {
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

    public struct GameStaticObjectData : IEquatable<GameStaticObjectData>
    {
        public int Id;
        public Vector3 Position;
        public IColliderConfig ColliderConfig;
        public Vector2Int Grid;
        public int Layer;
        public string Tag;

        public bool Equals(GameStaticObjectData other)
        {
            return Id == other.Id && Position.Equals(other.Position) && Equals(ColliderConfig, other.ColliderConfig) 
                   && Layer == other.Layer && Tag == other.Tag && Grid.Equals(other.Grid);
        }

        public override bool Equals(object obj)
        {
            return obj is GameStaticObjectData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, Position, ColliderConfig, Grid, Layer, Tag);
        }

        public static bool operator ==(GameStaticObjectData left, GameStaticObjectData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(GameStaticObjectData left, GameStaticObjectData right)
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