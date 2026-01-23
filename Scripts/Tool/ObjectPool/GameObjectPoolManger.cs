using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Tool.ObjectPool
{
    public class GameObjectPoolManger : Singleton<GameObjectPoolManger>
    {
        private readonly Dictionary<int, Queue<GameObject>> _poolDictionary = new Dictionary<int, Queue<GameObject>>();
        
        private Queue<GameObject> CreateOrGetPool(GameObject prefab, int capacity = 5, int assetId = 0)
        {
            int key = assetId != 0 ? assetId : prefab.GetInstanceID();

            if (!_poolDictionary.TryGetValue(key, out var queue))
            {
                queue = new Queue<GameObject>(capacity);
                _poolDictionary.Add(key, queue);
            }

            return queue;
        }

        public void ClearPool(GameObject prefab)
        {
            _poolDictionary.Remove(prefab.GetInstanceID());
        }
        
        public void ClearAllPool()
        {
            var pooledObjects = Object.FindObjectsOfType<PooledObject>(true);
            foreach (var pooledObject in pooledObjects)
            {
                Object.Destroy(pooledObject);
            }

            foreach (var pool in _poolDictionary.Values)
            {
                foreach (var obj in pool)
                {
                    Object.Destroy(obj);
                }
            }

            _poolDictionary.Clear();
        }

        private GameObject CreateGameObject(GameObject prefab, Transform parent = null)
        {
            var newObj = Object.Instantiate(prefab, parent);
            var pooledObj = newObj.AddComponent<PooledObject>();
            pooledObj.PrefabId = prefab.GetInstanceID();
            newObj.SetActive(false);
            return newObj;
        }

        public GameObject GetObject(
            GameObject prefab, 
            Vector3 position = default, 
            Quaternion rotation = default, 
            Transform parent = null, 
            Action<GameObject> onSpawn = null, 
            int capacity = 10,
            int assetId = 0)
        {
            var pool = CreateOrGetPool(prefab, capacity, assetId);
            GameObject obj = null;

            // 安全获取有效对象
            while (pool.Count > 0 && !obj)
            {
                obj = pool.Dequeue();
                // 检查对象是否被销毁（Unity特殊处理）
                if (obj && obj.Equals(null))
                {
                    obj = null;
                }
            }

            // 没有可用对象时创建新对象
            if (!obj)
            {
                obj = CreateGameObject(prefab, parent);
            }

            // 设置对象属性（统一处理）
            if (parent)
            {
                obj.transform.SetParent(parent);
            }
            
            obj.transform.position = position;
            obj.transform.rotation = rotation;
            
            // 确保组件存在（不重复添加）
            var poolObj = obj.GetComponent<PooledObject>();
            if (!poolObj)
            {
                poolObj = obj.AddComponent<PooledObject>();
                poolObj.PrefabId = prefab.GetInstanceID(); // 仅新增组件时设置
            }

            // 激活对象并触发回调
            obj.SetActive(true);
            onSpawn?.Invoke(obj);
            poolObj.OnSelfSpawn?.Invoke();

            return obj;
        }

        public void ReturnObject(GameObject obj, Action onDespawn = null)
        {
            if (!obj || obj.Equals(null))
            {
                Debug.LogWarning("Trying to return a destroyed object.");
                return;
            }

            var pooledObj = obj.GetComponent<PooledObject>();
            if (pooledObj)
            {
                int key = pooledObj.PrefabId;
                
                // 确保对象池存在
                if (!_poolDictionary.TryGetValue(key, out var queue))
                {
                    queue = new Queue<GameObject>(5);
                    _poolDictionary.Add(key, queue);
                }

                // 触发回收事件
                pooledObj.OnSelfDespawn?.Invoke();
                onDespawn?.Invoke();
                
                // 重置并回收
                obj.SetActive(false);
                if (obj.TryGetComponent<NetworkIdentity>(out var identity))
                {
                    Debug.Log($"Recycling {identity.netId}");
                }
                queue.Enqueue(obj);
            }
            else
            {
                Debug.LogWarning("Object wasn't created by pool: " + obj.name);
                Object.Destroy(obj);
            }
        }
    }
}
