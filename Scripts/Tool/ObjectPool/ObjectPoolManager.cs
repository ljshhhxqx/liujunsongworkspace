using System;
using System.Collections.Generic;

namespace HotUpdate.Scripts.Tool.ObjectPool
{
    public class ObjectPoolManager<T> : Singleton<ObjectPoolManager<T>>  where T : IPoolObject, new()
    {
        private Dictionary<Type, Stack<T>> _pool;
        private readonly int _maxSize = 10;
        private readonly int _initialSize = 5;

        public ObjectPoolManager()
        {
            Init();
        }

        public ObjectPoolManager(int initialSize, int maxSize)
        {
            _initialSize = initialSize;
            _maxSize = maxSize;
            Init();
        }

        private void Init()
        {
            _pool = new Dictionary<Type, Stack<T>>(_initialSize);
            _pool.Add(typeof(T), new Stack<T>(_initialSize));
        }

        public T Get(int size = 0)
        {
            var type = typeof(T);
            T obj;
            if (_pool.TryGetValue(type, out var stack))
            {
                if (stack.Count > 0)
                {
                    obj = stack.Pop();
                    obj.Clear();
                    obj.Init();
                    return obj;
                }
                obj = new T();
                obj.Init();
                return obj;
            }
            obj = new T();
            stack = new Stack<T>(size);
            obj.Init();
            _pool.Add(type, stack);
            return obj;
        }

        public void Return(T item)
        {
            if (_pool.Count < _maxSize)
            {
                item.Clear();
                var type = typeof(T);
                if (_pool.TryGetValue(type, out var stack))
                {
                    stack.Push(item);
                }
                else
                {
                    stack = new Stack<T>();
                    stack.Push(item);
                    _pool.Add(type, stack);
                }
            }
        }
    }

    public interface IPoolObject
    {
        public void Init();
        public void Clear();
    }
}