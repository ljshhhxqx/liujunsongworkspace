using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotUpdate.Scripts.Tool.ObjectPool
{
    /// <summary>
    /// 全自动化的网络对象池服务
    /// 自动管理对象池的创建、扩展和清理
    /// </summary>
    public class NetworkGameObjectPoolManager : NetworkBehaviour
    {
        [Header("Auto Pool Settings")] [Tooltip("默认对象池初始大小")]
        private const int DefaultPoolSize = 5;
    
        [Tooltip("对象池自动扩展大小")]
        private const int PoolExpandSize = 5;

        // 核心映射：assetId -> 对象池队列
        private Dictionary<uint, Queue<GameObject>> _poolDictionary = new Dictionary<uint, Queue<GameObject>>();
    
        // assetId -> 预制体的映射
        private Dictionary<uint, GameObject> _prefabDictionary = new Dictionary<uint, GameObject>();
    
        // 已注册的预制体集合（用于避免重复注册）
        private HashSet<uint> _registeredPrefabs = new HashSet<uint>();
        
        public Scene CurrentScene { get; set; }

        /// <summary>
        /// 服务器端：从对象池生成网络对象（全自动接口）
        /// </summary>
        [Server]
        public GameObject Spawn(GameObject prefab, Vector3 position = default, Quaternion rotation = default, Transform parent = null, Action<GameObject> onSpawn = null, int poolSize = DefaultPoolSize)
        {
            if (prefab == null)
            {
                Debug.LogError("Cannot spawn null prefab");
                return null;
            }
        
            // 获取预制体的 NetworkIdentity
            NetworkIdentity prefabIdentity = prefab.GetComponent<NetworkIdentity>();
            if (prefabIdentity == null)
            {
                Debug.LogError($"Prefab {prefab.name} has no NetworkIdentity!");
                return null;
            }
        
            uint assetId = prefabIdentity.assetId;
        
            // 确保对象池存在
            EnsurePoolExists(prefab, assetId);
        
            // 从对象池获取或创建对象
            GameObject obj = GetOrCreateObjectFromPool(assetId);
            if (obj == null)
            {
                Debug.LogError($"Failed to get object from pool for assetId {assetId}");
                return null;
            }
        
            // 设置对象位置和旋转
            obj.transform.position = position == default? obj.transform.position : position;
            obj.transform.rotation = rotation == default? obj.transform.rotation : rotation;
            if (obj.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnSelfSpawn();
            }
            if (parent != null)
            {
                obj.transform.SetParent(parent);
            }
            else
            {
                obj.transform.SetParent(transform);
            }
            obj.SetActive(true);
        
            // 使用 Mirror 的网络生成
            NetworkServer.Spawn(obj);
            return obj;
        }
    
        /// <summary>
        /// 服务器端：回收网络对象到对象池（全自动接口）
        /// </summary>
        [Server]
        public void Despawn(GameObject obj)
        {
            if (obj == null)
            {
                Debug.LogError("Cannot despawn null object");
                return;
            }
        
            // 使用 Mirror 的网络销毁（会触发 UnspawnHandler）
            NetworkServer.Destroy(obj);
        }

        /// <summary>
        /// 确保指定预制体的对象池存在
        /// </summary>
        private void EnsurePoolExists(GameObject prefab, uint assetId)
        {
            // 如果池已存在，直接返回
            if (_poolDictionary.ContainsKey(assetId))
            {
                return;
            }
        
            // 创建对象池队列
            Queue<GameObject> objectPool = new Queue<GameObject>();
        
            // 预实例化对象
            for (int i = 0; i < DefaultPoolSize; i++)
            {
                GameObject obj = Instantiate(prefab, parent: transform);
                obj.SetActive(false);
                //obj.transform.SetParent(transform);
                objectPool.Enqueue(obj);
            }
        
            // 注册到字典
            _poolDictionary.Add(assetId, objectPool);
            _prefabDictionary.Add(assetId, prefab);
            var isRegistered = _registeredPrefabs.Contains(assetId);
        
            // 注册生成处理器（只需注册一次）
            if (!isRegistered)
            {
                _registeredPrefabs.Add(assetId);
                //Debug.Log($"Auto-created pool for {prefab.name} (assetId: {assetId}) with size {DefaultPoolSize}");
                RpcRegisterPrefab(assetId);
            }
        
        }

        [ClientRpc]
        public void RpcRegisterPrefab(uint assetId)
        {
            var prefab = NetworkClient.prefabs[assetId];
            NetworkClient.UnregisterPrefab(prefab);
            NetworkClient.RegisterPrefab(prefab, SpawnHandler, UnspawnHandler);
        }

        /// <summary>
        /// 从对象池获取对象，如果池为空则自动扩展
        /// </summary>
        private GameObject GetOrCreateObjectFromPool(uint assetId)
        {
            if (!_poolDictionary.TryGetValue(assetId, out Queue<GameObject> pool))
            {
                Debug.LogError($"No pool found for assetId {assetId}");
                return null;
            }
        
            // 如果池为空，自动扩展
            if (pool.Count == 0)
            {
                ExpandPool(assetId, PoolExpandSize);
            }
        
            // 从池中取出对象
            return pool.Count > 0 ? pool.Dequeue() : null;
        }
    
        /// <summary>
        /// 扩展对象池
        /// </summary>
        private void ExpandPool(uint assetId, int expandBy)
        {
            if (!_prefabDictionary.TryGetValue(assetId, out GameObject prefab))
            {
                Debug.LogError($"Cannot expand pool for assetId {assetId} - prefab not found");
                return;
            }
        
            if (!_poolDictionary.TryGetValue(assetId, out Queue<GameObject> pool))
            {
                Debug.LogError($"Cannot expand pool for assetId {assetId} - pool not found");
                return;
            }
        
            // 实例化新对象并添加到池中
            for (int i = 0; i < expandBy; i++)
            {
                GameObject obj = Instantiate(prefab, parent:transform);
                obj.SetActive(false);
                pool.Enqueue(obj);
            }
        
            Debug.Log($"Expanded pool for assetId {assetId} by {expandBy} objects. New size: {pool.Count}");
        }

        // 自定义生成处理器
        private GameObject SpawnHandler(SpawnMessage msg)
        {
            uint assetId = msg.assetId;
        
            if (_poolDictionary.TryGetValue(assetId, out Queue<GameObject> pool) && pool.Count > 0)
            {
                // 从对象池获取对象
                GameObject obj = pool.Dequeue();
                if (obj.TryGetComponent<IPoolable>(out var poolable))
                {
                    poolable.OnSelfSpawn();
                }
                obj.SetActive(true);
                obj.transform.SetParent( transform);
                return obj;
            }
        
            // 对象池为空，尝试扩展并获取对象
            if (_prefabDictionary.TryGetValue(assetId, out GameObject prefab))
            {
                Debug.LogWarning($"Object pool for assetId {assetId} is empty, expanding pool");
                ExpandPool(assetId, PoolExpandSize);
            
                if (_poolDictionary.TryGetValue(assetId, out pool) && pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    if (obj.TryGetComponent<IPoolable>(out var poolable))
                    {
                        poolable.OnSelfSpawn();
                    }
                    obj.SetActive(true);
                    obj.transform.SetParent( transform);
                    return obj;
                }
            }
            else
            {
                prefab = NetworkClient.prefabs[assetId];
                _prefabDictionary.Add(assetId, prefab);
            }
        
            // 如果扩展后仍然无法获取对象，回退到常规实例化
            Debug.LogWarning($"Failed to get object from pool for assetId {assetId}, instantiating new object");
            return Instantiate(prefab, parent: transform);
        }

        // 自定义取消生成处理器
        private void UnspawnHandler(GameObject obj)
        {
            // 直接从对象的 NetworkIdentity 获取 assetId
            NetworkIdentity identity = obj.GetComponent<NetworkIdentity>();
            if (identity == null)
            {
                Debug.LogError($"Object {obj.name} has no NetworkIdentity, cannot return to pool");
                Destroy(obj);
                return;
            }
        
            uint assetId = identity.assetId;
        
            if (_poolDictionary.TryGetValue(assetId, out Queue<GameObject> pool))
            {
                // 重置对象状态
                ResetPooledObject(obj);
            
                // 返回对象池
                obj.SetActive(false);
                obj.transform.SetParent(transform);
                pool.Enqueue(obj);
            }
            else
            {
                // 没有找到对应的对象池，直接销毁
                Debug.LogWarning($"No pool found for assetId {assetId}, destroying object");
                Destroy(obj);
            }
        }

        // 重置池化对象的状态
        private void ResetPooledObject(GameObject obj)
        {
            // 重置物理状态
            Rigidbody rb = obj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }
        
            // 重置变换
            obj.transform.localScale = Vector3.one;
        
            // 重置自定义组件状态
            IPoolable[] poolables = obj.GetComponents<IPoolable>();
            foreach (IPoolable poolable in poolables)
            {
                poolable.OnSelfDespawn();
            }
        }
    
        /// <summary>
        /// 预创建对象池（可选，用于预先分配内存）
        /// </summary>
        public void PrecreatePool(GameObject prefab, int size = -1)
        {
            if (prefab == null) return;
        
            NetworkIdentity identity = prefab.GetComponent<NetworkIdentity>();
            if (!identity) return;
        
            uint assetId = identity.assetId;
        
            // 如果池已存在，跳过
            if (_poolDictionary.ContainsKey(assetId)) return;
        
            EnsurePoolExists(prefab, assetId);
        
            // 如果指定了不同的大小，调整池大小
            if (size > 0 && size != DefaultPoolSize)
            {
                // 清空当前池
                while (_poolDictionary[assetId].Count > 0)
                {
                    Destroy(_poolDictionary[assetId].Dequeue());
                }
            
                // 重新创建指定大小的池
                for (int i = 0; i < size; i++)
                {
                    GameObject obj = Instantiate(prefab, parent: transform);
                    obj.SetActive(false);
                    //obj.transform.SetParent(transform);
                    _poolDictionary[assetId].Enqueue(obj);
                }
            }
        }

        /// <summary>
        /// 清空所有对象池
        /// </summary>
        public void ClearAllPools()
        {
            foreach (var pool in _poolDictionary.Values)
            {
                while (pool.Count > 0)
                {
                    GameObject obj = pool.Dequeue();
                    Destroy(obj);
                }
            }
        
            _poolDictionary.Clear();
            _prefabDictionary.Clear();
            _registeredPrefabs.Clear();
        
            Debug.Log("All object pools cleared");
        }
    
        /// <summary>
        /// 获取对象池统计信息
        /// </summary>
        public string GetPoolStatistics()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            sb.AppendLine("=== Network Object Pool Statistics ===");
        
            foreach (var kvp in _poolDictionary)
            {
                uint assetId = kvp.Key;
                string prefabName = _prefabDictionary.TryGetValue(assetId, out GameObject prefab) ? 
                    prefab.name : "Unknown Prefab";
                
                sb.AppendLine($"{prefabName} (ID: {assetId}): {kvp.Value.Count} objects in pool");
            }
        
            sb.AppendLine($"Total Pools: {_poolDictionary.Count}");
            return sb.ToString();
        }
    }
}