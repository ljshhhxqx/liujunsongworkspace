using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Tool.Resource;
using HotUpdate.Scripts.Game.Inject;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HotUpdate.Scripts.Tool.ObjectPool
{
    /// <summary>
    /// 全自动化的网络对象池服务
    /// 自动管理对象池的创建、扩展和清理
    /// </summary>
    public class NetworkGameObjectPoolManager : NetworkAutoInjectHandlerBehaviour
    {
        [Header("Auto Pool Settings")] [Tooltip("默认对象池初始大小")]
        private const int DefaultPoolSize = 5;
    
        [Tooltip("对象池自动扩展大小")]
        private const int PoolExpandSize = 5;

        // 核心映射：uid -> 对象池队列
        private Dictionary<uint, Queue<GameObject>> _poolDictionary = new Dictionary<uint, Queue<GameObject>>();
    
        // uid -> 预制体的映射
        private Dictionary<uint, GameObject> _prefabDictionary = new Dictionary<uint, GameObject>();
    
        // 已注册的预制体集合（用于避免重复注册）
        private HashSet<uint> _registeredPrefabs = new HashSet<uint>();
        private readonly SyncDictionary<uint, uint> serverAssetIdToUid = new SyncDictionary<uint, uint>();
        private bool mappingReceived = false;
        private Queue<SpawnMessage> pendingSpawnMessages = new Queue<SpawnMessage>();

        public Scene CurrentScene { get; set; }
        
        protected override void InjectServerCallback()
        {
            BuildMappingOnServer();
        }
        
        protected override void InjectClientCallback()
        {
            
            serverAssetIdToUid.OnChange += OnMappingChanged;

            // 如果字典已经有一些数据（极少情况），立即处理缓存
            if (serverAssetIdToUid.Count > 0)
            {
                mappingReceived = true;

            }
            var prefabs = NetworkClient.prefabs.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            
            foreach (var kvp in prefabs)
            {
                uint assetId = kvp.Key;
                GameObject prefab = kvp.Value;
                // 取消默认注册，改用自定义
                NetworkClient.UnregisterPrefab(prefab);
                NetworkClient.RegisterSpawnHandler(assetId, SpawnHandler, UnspawnHandler);
            }
        }

        private void OnMappingChanged(SyncIDictionary<uint, uint>.Operation op, uint key, uint value)
        {
            // 只要字典有内容，就认为映射已接收（可根据需要更精细控制）
            if (!mappingReceived && serverAssetIdToUid.Count > 0)
            {
                mappingReceived = true;
            }
        } 
        public bool TryGetUid(uint assetId, out uint uid, SpawnMessage msg = default)
        {
            if (mappingReceived)
            {
                return serverAssetIdToUid.TryGetValue(assetId, out uid);
            }
            else
            {
                // 映射尚未到达，缓存消息以便后续处理
                if (msg.assetId != 0)
                {
                    lock (pendingSpawnMessages)
                    {
                        pendingSpawnMessages.Enqueue(msg);
                    }
                }
                uid = 0;
                return false;
            }
        }
        
        private void ProcessPendingSpawns()
        {
            lock (pendingSpawnMessages)
            {
                while (pendingSpawnMessages.Count > 0)
                {
                    SpawnMessage msg = pendingSpawnMessages.Dequeue();
                    // 重新触发自定义 SpawnHandler（需通过某种方式调用）
                    // 这里可以直接调用自定义的生成逻辑，例如通过事件或直接调用对象池的 SpawnHandler
                    SpawnHandler(msg);
                }
            }
        }
        [Server]
        private void PrecreateAllPools()
        {
            foreach (var kvp in _prefabDictionary)
                EnsurePoolExists(kvp.Key, kvp.Value);
        }

        [Server]
        private void BuildMappingOnServer()
        {
            // 遍历所有已注册的预制体（NetworkServer.prefabs 包含所有通过 NetworkManager 注册的预制体）
            foreach (var prefab in NetworkManager.singleton.spawnPrefabs)
            {
                var identity = prefab.GetComponent<NetworkIdentity>();
                if (identity == null) continue;
                uint assetId = identity.assetId;
                uint uid = DataJsonManager.Instance.GetUid(prefab.name); // 确保名称唯一
                if (uid != 0)
                {
                    serverAssetIdToUid[assetId] = uid;
                    _prefabDictionary[uid] = prefab;
                }
            }
        }

        /// <summary>
        /// 服务器端：从对象池生成网络对象（全自动接口）
        /// </summary>
        [Server]
        public GameObject Spawn(GameObject prefab, Vector3 position = default, Quaternion rotation = default, Transform parent = null, Action<GameObject> onSpawn = null, int poolSize = DefaultPoolSize)       
        {
            uint uid = DataJsonManager.Instance.GetUid(prefab.name);
            if (uid == 0) return null;
            EnsurePoolExists(uid, prefab, poolSize);
            GameObject obj = GetOrCreateObjectFromPool(uid);
            if (obj == null) return null;
            obj.transform.SetPositionAndRotation(position, rotation);
            if (parent != null) obj.transform.SetParent(parent);
            else obj.transform.SetParent(transform);
            obj.SetActive(true);            
            if (obj.TryGetComponent<IPoolable>(out var poolable))
            {
                poolable.OnSelfSpawn();
            }
            NetworkServer.Spawn(obj);
            return obj;
        }

        /// <summary>
        /// 服务器端：回收网络对象到对象池（全自动接口）
        /// </summary>
        [Server]
        public void Despawn(GameObject obj)
        {
            if (obj == null) return;
            NetworkServer.UnSpawn(obj); // 从网络系统移除，不销毁
            ReturnToPool(obj);           // 归还到池
        }

        /// <summary>
        /// 确保指定预制体的对象池存在
        /// </summary>
        private void EnsurePoolExists(uint uid, GameObject prefab, int poolSize = 5)
        {
            if (_poolDictionary.ContainsKey(uid)) return;
            var pool = new Queue<GameObject>();
            for (int i = 0; i < DefaultPoolSize; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                obj.AddComponent<PooledObject>().Uid = uid;
                pool.Enqueue(obj);
            }
            _poolDictionary[uid] = pool;
        }

        /// <summary>
        /// 从对象池获取对象，如果池为空则自动扩展
        /// </summary>
        private GameObject GetOrCreateObjectFromPool(uint uid)
        {
            if (!_poolDictionary.TryGetValue(uid, out var pool))
                return null;
            if (pool.Count == 0) ExpandPool(uid, PoolExpandSize);
            return pool.Count > 0 ? pool.Dequeue() : null;
        }
    
        /// <summary>
        /// 扩展对象池
        /// </summary>
        
        private void ExpandPool(uint uid, int expandBy)
        {
            if (!_prefabDictionary.TryGetValue(uid, out var prefab)) return;
            if (!_poolDictionary.TryGetValue(uid, out var pool))
                pool = _poolDictionary[uid] = new Queue<GameObject>();
            for (int i = 0; i < expandBy; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                obj.SetActive(false);
                obj.AddComponent<PooledObject>().Uid = uid;
                pool.Enqueue(obj);
            }
        }
        
        public GameObject CustomSpawnHandler(SpawnMessage msg)
        {
            uint uid = 0;
            if (mappingReceived && serverAssetIdToUid.TryGetValue(msg.assetId, out uid))
            {
                GameObject obj = GetPooledObject(uid);
                if (obj == null)
                {
                    // 池空，回退实例化
                    var resData = DataJsonManager.Instance.GetResourceData(uid);
                    var prefab = ResourceManager.Instance.GetResource<GameObject>(resData);
                    if (prefab == null) return null;
                    obj = Instantiate(prefab);
                    obj.AddComponent<PooledObject>().Uid = uid;
                }
                ApplyTransformAndPayload(obj, msg);
                obj.SetActive(true);
                return obj;
            }
            else
            {
                // 映射未就绪，回退到默认预制体
                if (NetworkClient.prefabs.TryGetValue(msg.assetId, out var fallback))
                {
                    GameObject obj = Instantiate(fallback);
                    ApplyTransformAndPayload(obj, msg);
                    return obj;
                }
                return null;
            }
        }
        private void ApplyTransformAndPayload(GameObject obj, SpawnMessage msg)
        {
            obj.transform.position = msg.position;
            obj.transform.rotation = msg.rotation;
            obj.transform.localScale = msg.scale;
        }
        // 从池中获取对象
        public GameObject GetPooledObject(uint uid)
        {
            if (_poolDictionary.TryGetValue(uid, out Queue<GameObject> pool) && pool.Count > 0)
            {
                return pool.Dequeue();
            }
            return null;
        }

        // 将对象归还池中（需在 UnspawnHandler 中调用）
        public void ReturnToPool(GameObject obj)
        {
            var pooled = obj.GetComponent<PooledObject>();
            if (pooled == null) { Destroy(obj); return; }
            uint uid = pooled.Uid;
            if (!_poolDictionary.TryGetValue(uid, out var pool)) { Destroy(obj); return; }
            ResetPooledObject(obj);
            obj.SetActive(false);
            obj.transform.SetParent(transform);
            pool.Enqueue(obj);
        }

        
        // 自定义生成处理器
        private GameObject SpawnHandler(SpawnMessage msg)
        {
            return CustomSpawnHandler(msg);
        }

        // 自定义取消生成处理器
        private void UnspawnHandler(GameObject obj)
        {
            ReturnToPool(obj);
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