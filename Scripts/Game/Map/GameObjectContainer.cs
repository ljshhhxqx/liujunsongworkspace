using System;
using System.Collections.Generic;
using System.Text;
using HotUpdate.Scripts.Collector;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Map
{
    public class GameObjectContainer : Singleton<GameObjectContainer>
    {
        private readonly Dictionary<int, GameObject> _idToGameObject = new Dictionary<int, GameObject>();
        private readonly Dictionary<Vector2Int, List<GameObjectData>> _mapObjectData = new Dictionary<Vector2Int, List<GameObjectData>>();

        private List<DynamicObjectData> _dynamicObjectData = new List<DynamicObjectData>();
        private Dictionary<uint, int> _dynamicObjectIds = new Dictionary<uint, int>();// Grid → dynamic object index
        private readonly Dictionary<Vector2Int, List<int>> _dynamicGridIndex = new Dictionary<Vector2Int, List<int>>();


        public void UpdateDynamicObjects(bool isServer)
        {
            for (int i = 0; i < _dynamicObjectData.Count; i++)
            {
                var data = _dynamicObjectData[i];
                NetworkIdentity identity;

                if (NetworkServer.spawned.TryGetValue(data.NetId, out identity))
                {
                    
                }
                else if (NetworkClient.spawned.TryGetValue(data.NetId, out identity))
                {
                    
                }
                if (identity)
                {
                    var newGrid = MapBoundDefiner.Instance.GetGridPosition(identity.transform.position);

                    if (newGrid != data.Grid)
                    {
                        _dynamicGridIndex[data.Grid].Remove(i);

                        if (!_dynamicGridIndex.TryGetValue(newGrid, out var list))
                            _dynamicGridIndex[newGrid] = list = new List<int>();

                        list.Add(i);
                        data.Grid = newGrid;
                    }
                    data.Position = identity.transform.position;
                    _dynamicObjectData[i] = data;
                }
                else
                {
                    RemoveObject(data.NetId);
                }
            }
        }

        private bool RemoveObject(uint netId)
        {
            if (!_dynamicObjectIds.TryGetValue(netId, out int index))
                return false;

            int lastIndex = _dynamicObjectData.Count - 1;
            var removedData = _dynamicObjectData[index];

            // 1. 从原 GridIndex 移除 index（防御式）
            if (_dynamicGridIndex.TryGetValue(removedData.Grid, out var removeList))
            {
                for (int i = removeList.Count - 1; i >= 0; i--)
                {
                    if (removeList[i] == index)
                        removeList.RemoveAt(i);
                }

                if (removeList.Count == 0)
                    _dynamicGridIndex.Remove(removedData.Grid);
            }

            // 2. 如果删的不是最后一个，执行 swap
            if (index != lastIndex)
            {
                var lastData = _dynamicObjectData[lastIndex];

                _dynamicObjectData[index] = lastData;
                _dynamicObjectIds[lastData.NetId] = index;

                // 修正 lastData 在 GridIndex 中的 index
                if (_dynamicGridIndex.TryGetValue(lastData.Grid, out var lastList))
                {
                    for (int i = 0; i < lastList.Count; i++)
                    {
                        if (lastList[i] == lastIndex)
                        {
                            lastList[i] = index;
                            break;
                        }
                    }
                }
            }

            // 3. 移除尾部
            _dynamicObjectData.RemoveAt(lastIndex);
            _dynamicObjectIds.Remove(netId);

            return true;
        }


        public HashSet<DynamicObjectData> GetIntersectedDynamicObjects(uint uid, Vector3 position,
            IColliderConfig colliderConfig)
        {
            var result = new HashSet<DynamicObjectData>();
            DynamicObjectIntersects(uid, position, colliderConfig, result);
            return result;
        }
        
        public bool CanSpawnAt(Vector3 position, IColliderConfig cfg)
        {
            var bounds = GamePhysicsSystem.GetWorldBounds(position, cfg);
            var grids = MapBoundDefiner.Instance.GetBoundsCovered(bounds);

            foreach (var grid in grids)
            {
                if (_mapObjectData.TryGetValue(grid, out var list))
                {
                    foreach (var obj in list)
                    {
                        if (GamePhysicsSystem.CheckIntersectsWithMargin(
                                position, obj.Position,
                                cfg, obj.ColliderConfig, 0.15f))
                        {
                            return false;
                        }
                    }
                }
            }
            return true;
        }


        public bool DynamicObjectIntersects(uint uid, Vector3 position, IColliderConfig colliderConfig,
            HashSet<DynamicObjectData> intersectedObjects, Func<DynamicObjectData, bool> onIntersected = null)
        {
            intersectedObjects.Clear();
            var bounds = GamePhysicsSystem.GetWorldBounds(position, colliderConfig);
            var grids = MapBoundDefiner.Instance.GetBoundsCovered(bounds);

            foreach (var grid in grids)
            {
                if (!_dynamicGridIndex.TryGetValue(grid, out var indices))
                    continue;

                foreach (var i in indices)
                {
                    if (i >= _dynamicObjectData.Count)
                        continue;
                    var data = _dynamicObjectData[i];
                    if (data.NetId == uid)
                        continue;
                    if (GamePhysicsSystem.CheckIntersectsWithMargin(
                            position, data.Position,
                            colliderConfig, data.ColliderConfig, 0.2f))
                    {
                        intersectedObjects.Add(data);
                        onIntersected?.Invoke(data);
                    }
                }
            }
            return intersectedObjects.Count > 0;
        }

        public HashSet<uint> GetDynamicObjectIdsByGrids(HashSet<Vector2Int> grids)
        {
            var hashSet = new HashSet<uint>();
            foreach (var grid in grids)
            {
                for (int i = 0; i < _dynamicObjectData.Count; i++)
                {
                    var data = _dynamicObjectData[i];
                    var gridBounds = MapBoundDefiner.Instance.GetGridPosition(data.Position);

                    if (gridBounds == grid)
                    {
                        hashSet.Add(data.NetId);
                    }
                }
            }
            return hashSet;
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
                for (int i = 0; i < _dynamicObjectData.Count; i++)
                {
                    var data = _dynamicObjectData[i];
                    var gridBounds = MapBoundDefiner.Instance.GetGridPosition(data.Position);

                    if (gridBounds == grid)
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

        public void AddDynamicObject(uint netId, Vector3 position, IColliderConfig colliderConfig,
            ObjectType type, int layer, string tag)
        {
            if (netId == 0 || _dynamicObjectIds.ContainsKey(netId))
                return;

            var grid = MapBoundDefiner.Instance.GetGridPosition(position);

            var data = new DynamicObjectData
            {
                NetId = netId,
                Position = position,
                Grid = grid,
                ColliderConfig = colliderConfig,
                Type = type,
                Layer = layer,
                Tag = tag,
            };

            _dynamicObjectIds[netId] = _dynamicObjectData.Count;
            _dynamicObjectData.Add(data);

            if (!_dynamicGridIndex.TryGetValue(grid, out var list))
            {
                list = new List<int>();
                _dynamicGridIndex[grid] = list;
            }

            list.Add(_dynamicObjectData.Count - 1);
        }


        public void RemoveDynamicObject(uint netId)
        {
            if (netId == 0)
            {
                return;
            }

            if (RemoveObject(netId))
            {
                //Debug.Log("[GameObjectContainer] RemoveDynamicObject: " + netId);
            }
        }

        public DynamicObjectData GetDynamicObjectData(uint netId)
        {
            if (!_dynamicObjectIds.TryGetValue(netId, out var index))
            {
                return default;
            }
            return _dynamicObjectData[index];
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

        public void AddStaticObject(GameObject gameObject, Collider collider, bool isMesh)
        {
            var staticObject = gameObject.GetComponent<GameStaticObject>();
            var position = gameObject.transform.position;
            var grid = MapBoundDefiner.Instance.GetGridPosition(position);
            if (!collider)
            {
                collider = gameObject.GetComponent<Collider>();
            }
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
            if (isMesh)
            {
                collider.enabled = false;
            }
            
        }

        public void ClearObjects()
        {
            _mapObjectData.Clear();
            _idToGameObject.Clear();
            _dynamicObjectData.Clear();
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
        Chest,
        Train,
        Rocket,
        Well,
        Death,
    }

    public struct DynamicObjectData : IEquatable<DynamicObjectData>
    {
        public uint NetId;
        public Vector3 Position;
        public Vector2Int Grid;
        public IColliderConfig ColliderConfig;
        public ObjectType Type;
        public LayerMask Layer;
        public string Tag;
        
        public override string ToString()
        {
            
            return $"NetId: {NetId}, Position: {Position}, ColliderConfig: {ColliderConfig} Type: {Type}, Layer: {Layer}, Tag: {Tag}, Grid: {Grid}";
        }
        
        public override bool Equals(object obj)
        {
            return obj is DynamicObjectData other && Equals(other);
        }

        public bool Equals(DynamicObjectData other)
        {
            return NetId == other.NetId && Position.Equals(other.Position) && Grid.Equals(other.Grid) && Equals(ColliderConfig, other.ColliderConfig) && Type == other.Type && Layer.Equals(other.Layer) && Tag == other.Tag;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(NetId, Position, Grid, ColliderConfig, (int)Type, Layer, Tag);
        }

        public static bool operator ==(DynamicObjectData left, DynamicObjectData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(DynamicObjectData left, DynamicObjectData right)
        {
            return !left.Equals(right);
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
            var sb = new StringBuilder();
            sb.Append("Id: ").Append(Id).Append(", Position: ").Append(Position).Append(", ColliderConfig: ").
                Append(ColliderConfig).Append(", Grid: ").Append(Grid)
                .Append(", Layer: ").Append(Layer).Append(", Tag: ").Append(Tag);
            return sb.ToString();
        }
    }
}