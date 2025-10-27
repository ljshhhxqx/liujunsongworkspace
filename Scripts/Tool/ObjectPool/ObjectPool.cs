using System;
using System.Collections.Generic;

namespace HotUpdate.Scripts.Tool.ObjectPool
{
    // 通用对象池，支持不同类型的对象池管理
    public static class ObjectPool<T> where T : new()
    {
        private static Dictionary<Type, Queue<T>> pools = new Dictionary<Type, Queue<T>>();

        public static T Get()
        {
            Type type = typeof(T);
            if (!pools.ContainsKey(type) || pools[type].Count == 0)
            {
                return new T();
            }
            return pools[type].Dequeue();
        }

        public static void Return(T item)
        {
            Type type = typeof(T);
            if (!pools.ContainsKey(type))
            {
                pools[type] = new Queue<T>();
            }
            pools[type].Enqueue(item);
        }
        
        public static void Clear()
        {
            pools.Clear();
        }
        
        public static void Clear(Type type)
        {
            if (pools.ContainsKey(type))
            {
                pools[type].Clear();
            }
        }
    }
}