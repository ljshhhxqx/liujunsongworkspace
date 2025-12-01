using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace HotUpdate.Scripts.Tool
{
    public class DoubleModifiedDictionary<TKey, TValue> where TKey : notnull
    {
        // 主字典和备字典
        private Dictionary<TKey, TValue> _activeDict = new();
        private Dictionary<TKey, TValue> _bufferDict = new();
        private readonly object _lock = new object();
        private volatile bool _isSwapping = false;
    
        // 添加元素（线程安全）
        public void Add(TKey key, TValue value)
        {
            lock (_lock)
            {
                _bufferDict[key] = value;
            }
        }
    
        // 批量添加
        public void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            lock (_lock)
            {
                foreach (var item in items)
                {
                    _bufferDict[item.Key] = item.Value;
                }
            }
        }
    
        // 删除元素
        public bool Remove(TKey key)
        {
            lock (_lock)
            {
                // 同时从两个字典删除（如果存在）
                bool removedFromBuffer = _bufferDict.Remove(key);
                bool removedFromActive = _activeDict.Remove(key);
                return removedFromBuffer || removedFromActive;
            }
        }
    
        // 执行交换和获取数据
        public Dictionary<TKey, TValue> SwapAndGetData()
        {
            lock (_lock)
            {
                _isSwapping = true;
            
                try
                {
                    // 1. 交换引用
                    (_activeDict, _bufferDict) = (_bufferDict, _activeDict);

                    // 2. 清空新的buffer字典
                    _bufferDict.Clear();
                
                    // 3. 返回深拷贝（确保线程安全）
                    return new Dictionary<TKey, TValue>(_activeDict);
                }
                finally
                {
                    _isSwapping = false;
                }
            }
        }
    
        // 获取当前活跃字典的只读视图
        public IReadOnlyDictionary<TKey, TValue> GetReadOnlyView()
        {
            lock (_lock)
            {
                return new ReadOnlyDictionary<TKey, TValue>(_activeDict);
            }
        }

        public void Clear()
        {
            _activeDict.Clear();
            _bufferDict.Clear();
        }
    }
}