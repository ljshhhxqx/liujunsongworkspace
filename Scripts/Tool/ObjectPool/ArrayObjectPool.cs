using System;
using System.Collections.Concurrent;
using System.Collections.Generic;

namespace AOTScripts.Tool.ObjectPool
{
    public class ArrayObjectPool : Singleton<ArrayObjectPool>
    {
        // 双重字典：外层按类型分组，内层按数组长度分组
        private readonly ConcurrentDictionary<Type, object> _collectionPools = 
            new ConcurrentDictionary<Type, object>();
        
        private readonly ConcurrentDictionary<Type, ConcurrentDictionary<int, ConcurrentStack<Array>>> _arrayPools =
            new ConcurrentDictionary<Type, ConcurrentDictionary<int, ConcurrentStack<Array>>>();

        // 获取对象（集合类型）
        public T GetCollection<T>() where T : class, new()
        {
            var pool = GetCollectionPool<T>();
            if (pool.TryPop(out T item)) return item;
            return new T();
        }

        // 归还集合对象
        public void Return<T>(T collection) where T : class
        {
            if (collection is ICollectionClearable clearable)
            {
                clearable.Clear();
                GetCollectionPool<T>().Push(collection);
            }
            else
            {
                throw new InvalidOperationException("Type must implement ICollectionClearable");
            }
        }

        // 获取数组对象
        public T[] GetArray<T>(int length)
        {
            var typeDict = _arrayPools.GetOrAdd(typeof(T), 
                _ => new ConcurrentDictionary<int, ConcurrentStack<Array>>());
            
            if (typeDict.TryGetValue(length, out var stack) && stack.TryPop(out var array))
            {
                return (T[])array;
            }
            
            return new T[length];
        }

        // 归还数组对象
        public void Return<T>(T[] array, bool clearArray = true)
        {
            if (clearArray) Array.Clear(array, 0, array.Length);
            
            var typeDict = _arrayPools.GetOrAdd(typeof(T), 
                _ => new ConcurrentDictionary<int, ConcurrentStack<Array>>());
            
            var stack = typeDict.GetOrAdd(array.Length, 
                _ => new ConcurrentStack<Array>());
            
            stack.Push(array);
        }

        // 内部方法：获取特定类型的集合池
        private ConcurrentStack<T> GetCollectionPool<T>()
        {
            var type = typeof(T);
            if (!_collectionPools.TryGetValue(type, out var poolObj))
            {
                poolObj = new ConcurrentStack<T>();
                _collectionPools[type] = poolObj;
            }
            return (ConcurrentStack<T>)poolObj;
        }
    }

    // 用于支持Clear操作的集合接口
    public interface ICollectionClearable
    {
        void Clear();
    }

    // 常用集合的扩展实现
    public class PoolableList<T> : List<T>, ICollectionClearable { }
    public class PoolableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ICollectionClearable { }
    public class PoolableQueue<T> : Queue<T>, ICollectionClearable { }
    public class PoolableStack<T> : Stack<T>, ICollectionClearable { }
}