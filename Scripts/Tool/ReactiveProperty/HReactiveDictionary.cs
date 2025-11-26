using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.ReactiveProperty
{
    /// <summary>
    /// 完全在热更新内部工作的可观察字典
    /// </summary>
    public class HReactiveDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();
        
        // 使用Action而不是接口，确保热更新内部调用
        private event Action<DictionaryChangeArgs<TKey, TValue>> DictionaryChanged;

        public TValue this[TKey key]
        {
            get => _dictionary[key];
            set
            {
                if (_dictionary.TryGetValue(key, out var oldValue))
                {
                    if (!EqualityComparer<TValue>.Default.Equals(oldValue, value))
                    {
                        _dictionary[key] = value;
                        OnDictionaryChanged(DictionaryChangeType.Updated, key, value, oldValue);
                    }
                }
                else
                {
                    _dictionary[key] = value;
                    OnDictionaryChanged(DictionaryChangeType.Added, key, value, default(TValue));
                }
            }
        }

        public ICollection<TKey> Keys => _dictionary.Keys;
        public ICollection<TValue> Values => _dictionary.Values;
        public int Count => _dictionary.Count;
        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
            if (_dictionary.ContainsKey(key))
            {
                return;
            }
            OnDictionaryChanged(DictionaryChangeType.Added, key, value, default(TValue));
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
            if (_dictionary.ContainsKey(item.Key))
            {
                return;
            }
            Add(item.Key, item.Value);
        }

        public bool Remove(TKey key)
        {
            if (_dictionary.TryGetValue(key, out var oldValue))
            {
                _dictionary.Remove(key);
                OnDictionaryChanged(DictionaryChangeType.Removed, key, default(TValue), oldValue);
                return true;
            }
            return false;
        }

        public bool Remove(KeyValuePair<TKey, TValue> item)
        {
            if (_dictionary.TryGetValue(item.Key, out var value) && 
                EqualityComparer<TValue>.Default.Equals(value, item.Value))
            {
                return Remove(item.Key);
            }
            return false;
        }

        public void Clear()
        {
            var oldItems = _dictionary.ToArray();
            _dictionary.Clear();
            OnDictionaryChanged(DictionaryChangeType.Reset, oldItems);
        }

        public bool ContainsKey(TKey key) => _dictionary.ContainsKey(key);
        public bool Contains(KeyValuePair<TKey, TValue> item) => _dictionary.Contains(item);
        public bool TryGetValue(TKey key, out TValue value) => _dictionary.TryGetValue(key, out value);
        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
            ((ICollection<KeyValuePair<TKey, TValue>>)_dictionary).CopyTo(array, arrayIndex);
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator() => _dictionary.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _dictionary.GetEnumerator();

        /// <summary>
        /// 观察字典变化
        /// </summary>
        public IDisposable Observe(Action<DictionaryChangeArgs<TKey, TValue>> handler)
        {
            DictionaryChanged += handler;
            return new DictionarySubscription(this, handler);
        }

        /// <summary>
        /// 观察字典变化
        /// </summary>
        public IDisposable ObserveClear(Action<DictionaryChangeArgs<TKey, TValue>> onReset)
        {
            return Observe(args =>
            {
                if (args.ChangeType == DictionaryChangeType.Reset)
                {
                    onReset?.Invoke(args);
                }
            });
        }

        /// <summary>
        /// 观察添加操作
        /// </summary>
        public IDisposable ObserveAdd(Action<TKey, TValue> onAdded)
        {
            return Observe(args =>
            {
                if (args.ChangeType == DictionaryChangeType.Added)
                {
                    onAdded?.Invoke(args.Key, args.NewValue);
                }
            });
        }

        /// <summary>
        /// 观察移除操作
        /// </summary>
        public IDisposable ObserveRemove(Action<TKey, TValue> onRemoved)
        {
            return Observe(args =>
            {
                if (args.ChangeType == DictionaryChangeType.Removed)
                {
                    onRemoved?.Invoke(args.Key, args.OldValue);
                }
            });
        }

        /// <summary>
        /// 观察更新操作
        /// </summary>
        public IDisposable ObserveUpdate(Action<TKey, TValue, TValue> onUpdated)
        {
            return Observe(args =>
            {
                if (args.ChangeType == DictionaryChangeType.Updated)
                {
                    onUpdated?.Invoke(args.Key, args.NewValue, args.OldValue);
                }
            });
        }

        /// <summary>
        /// 观察特定键的变化
        /// </summary>
        public IDisposable ObserveKey(TKey key, Action<DictionaryChangeType, TValue, TValue> onKeyChanged)
        {
            return Observe(args =>
            {
                if (EqualityComparer<TKey>.Default.Equals(args.Key, key))
                {
                    onKeyChanged?.Invoke(args.ChangeType, args.NewValue, args.OldValue);
                }
            });
        }

        /// <summary>
        /// 尝试添加，如果键不存在
        /// </summary>
        public bool TryAdd(TKey key, TValue value)
        {
            if (!_dictionary.ContainsKey(key))
            {
                Add(key, value);
                return true;
            }
            return false;
        }

        /// <summary>
        /// 添加或更新
        /// </summary>
        public void AddOrUpdate(TKey key, TValue value)
        {
            this[key] = value;
        }

        private void OnDictionaryChanged(DictionaryChangeType changeType, TKey key, TValue newValue, TValue oldValue)
        {
            var args = new DictionaryChangeArgs<TKey, TValue>(changeType, key, newValue, oldValue);
            OnDictionaryChanged(args);
        }

        private void OnDictionaryChanged(DictionaryChangeType changeType, KeyValuePair<TKey, TValue>[] oldItems)
        {
            var args = new DictionaryChangeArgs<TKey, TValue>(changeType, oldItems);
            OnDictionaryChanged(args);
        }

        private void OnDictionaryChanged(DictionaryChangeArgs<TKey, TValue> args)
        {
            try
            {
                DictionaryChanged?.Invoke(args); // 热更新内部调用
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in dictionary change notification: {e}");
            }
        }

        private void RemoveObserver(Action<DictionaryChangeArgs<TKey, TValue>> handler)
        {
            DictionaryChanged -= handler;
        }

        private class DictionarySubscription : IDisposable
        {
            private HReactiveDictionary<TKey, TValue> _dictionary;
            private Action<DictionaryChangeArgs<TKey, TValue>> _handler;

            public DictionarySubscription(HReactiveDictionary<TKey, TValue> dictionary, Action<DictionaryChangeArgs<TKey, TValue>> handler)
            {
                _dictionary = dictionary;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_dictionary != null && _handler != null)
                {
                    _dictionary.RemoveObserver(_handler);
                    _dictionary = null;
                    _handler = null;
                }
            }
        }
    }

    public enum DictionaryChangeType
    {
        Added,
        Removed,
        Updated,
        Reset
    }

    public struct DictionaryChangeArgs<TKey, TValue>
    {
        public DictionaryChangeType ChangeType { get; }
        public TKey Key { get; }
        public TValue NewValue { get; }
        public TValue OldValue { get; }
        public IReadOnlyList<KeyValuePair<TKey, TValue>> OldItems { get; }

        public DictionaryChangeArgs(DictionaryChangeType changeType, TKey key, TValue newValue, TValue oldValue)
        {
            ChangeType = changeType;
            Key = key;
            NewValue = newValue;
            OldValue = oldValue;
            OldItems = null;
        }

        public DictionaryChangeArgs(DictionaryChangeType changeType, KeyValuePair<TKey, TValue>[] oldItems)
        {
            ChangeType = changeType;
            Key = default(TKey);
            NewValue = default(TValue);
            OldValue = default(TValue);
            OldItems = oldItems;
        }
    }
}