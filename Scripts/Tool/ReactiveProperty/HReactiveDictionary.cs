using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.ReactiveProperty
{
    /// <summary>
    /// 完全在热更新内部工作的可观察字典
    /// </summary>
    public class HReactiveDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _dictionary = new Dictionary<TKey, TValue>();

        // 使用并发队列存储监听器以提高性能
        private ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>> _activeHandlers =
            new ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>>();

        private ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>> _bufferHandlers =
            new ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>>();

        private ConcurrentDictionary<Action<DictionaryChangeArgs<TKey, TValue>>, bool> _handlerSet =
            new ConcurrentDictionary<Action<DictionaryChangeArgs<TKey, TValue>>, bool>();

        private int _isNotifying = 0;

        private  ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>> _pendingAdditions =
            new ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>>();

        private  ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>> _pendingRemovals =
            new ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>>();

        // 使用Action而不是接口，确保热更新内部调用
        public event Action<DictionaryChangeArgs<TKey, TValue>> DictionaryChanged
        {
            add
            {
                if (value == null) return;

                if (Interlocked.CompareExchange(ref _isNotifying, 1, 1) == 1)
                {
                    _pendingAdditions.Enqueue(value);
                }
                else
                {
                    AddHandlerDirectly(value);
                }
            }
            remove
            {
                if (value == null) return;

                if (Interlocked.CompareExchange(ref _isNotifying, 1, 1) == 1)
                {
                    _pendingRemovals.Enqueue(value);
                }
                else
                {
                    RemoveHandlerDirectly(value);
                }
            }
        }

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
            if (_dictionary.TryGetValue(key, out var oldValue))
            {
                // 键已存在，触发 Updated 事件
                if (!EqualityComparer<TValue>.Default.Equals(oldValue, value))
                {
                    _dictionary[key] = value;
                    OnDictionaryChanged(DictionaryChangeType.Updated, key, value, oldValue);
                }
            }
            else
            {
                // 键不存在，触发 Added 事件
                _dictionary.TryAdd(key, value);
                OnDictionaryChanged(DictionaryChangeType.Added, key, value, default(TValue));
            }
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
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

        public void Update(TKey key, TValue value)
        {
            if (_dictionary.TryGetValue(key, out var oldValue))
            {
                _dictionary[key] = value;
                OnDictionaryChanged(DictionaryChangeType.Updated, key, value, oldValue);
            }
            else
            {
                _dictionary[key] = value;
                OnDictionaryChanged(DictionaryChangeType.Added, key, value, default(TValue));
            }
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
            if (handler == null) return null;

            if (Interlocked.CompareExchange(ref _isNotifying, 1, 1) == 1)
            {
                _pendingAdditions.Enqueue(handler);
            }
            else
            {
                AddHandlerDirectly(handler);
            }

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

        /// <summary>
        /// 批量操作，减少通知次数
        /// </summary>
        public void BatchUpdate(Action<HReactiveDictionary<TKey, TValue>> updateAction)
        {
            if (updateAction == null) return;

            // 创建新的空队列作为缓冲区
            var originalActiveQueue = _activeHandlers;
            var originalBufferQueue = _bufferHandlers;

            // 清空当前队列，批量操作期间不通知
            _activeHandlers.Clear();
            _bufferHandlers.Clear();
            _handlerSet.Clear();

            try
            {
                updateAction(this);

                // 批量操作结束后，触发一次Reset事件
                var args = new DictionaryChangeArgs<TKey, TValue>(DictionaryChangeType.Reset, _dictionary.ToArray());
                NotifyHandlers(originalActiveQueue, args);
            }
            finally
            {
                // 恢复原来的队列
                Interlocked.Exchange(ref _activeHandlers, originalActiveQueue);
                Interlocked.Exchange(ref _bufferHandlers, originalBufferQueue);

                // 恢复handler集合（需要重新构建）
                _handlerSet.Clear();
                foreach (var handler in originalActiveQueue)
                {
                    _handlerSet.TryAdd(handler, true);
                }

                foreach (var handler in originalBufferQueue)
                {
                    _handlerSet.TryAdd(handler, true);
                }
            }
        }

        /// <summary>
        /// 添加或更新多个键值对
        /// </summary>
        public void AddOrUpdateRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
        {
            if (items == null) return;

            foreach (var item in items)
            {
                this[item.Key] = item.Value;
            }
        }

        /// <summary>
        /// 获取所有键值对快照
        /// </summary>
        public KeyValuePair<TKey, TValue>[] ToArray()
        {
            return _dictionary.ToArray();
        }

        private void AddHandlerDirectly(Action<DictionaryChangeArgs<TKey, TValue>> handler)
        {
            if (_handlerSet.TryAdd(handler, true))
            {
                _activeHandlers.Enqueue(handler);
            }
        }

        private void RemoveHandlerDirectly(Action<DictionaryChangeArgs<TKey, TValue>> handler)
        {
            if (_handlerSet.TryRemove(handler, out _))
            {
                // 不需要立即从队列中移除，等待下次通知时清理
            }
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
            if (_activeHandlers.IsEmpty && _bufferHandlers.IsEmpty)
                return;

            // 使用原子操作设置通知标志
            Interlocked.Exchange(ref _isNotifying, 1);

            try
            {
                // 清空缓冲区并交换队列
                _bufferHandlers.Clear();

                // 从活动队列移动到缓冲区，同时清理已移除的处理器
                while (_activeHandlers.TryDequeue(out var handler))
                {
                    if (_handlerSet.ContainsKey(handler))
                    {
                        _bufferHandlers.Enqueue(handler);
                        try
                        {
                            handler?.Invoke(args);
                        }
                        catch (Exception e)
                        {
                            Debug.LogError($"Error in dictionary change notification: {e}");
                        }
                    }
                }

                // 交换队列
                SwapQueues();

                // 处理待添加的监听器
                ProcessPendingAdditions();

                // 处理待移除的监听器
                ProcessPendingRemovals();
            }
            finally
            {
                Interlocked.Exchange(ref _isNotifying, 0);
            }
        }

        private void SwapQueues()
        {
            // 交换active和buffer队列
            var temp = _activeHandlers;
            _activeHandlers = _bufferHandlers;
            _bufferHandlers = temp;
        }

        private void ProcessPendingAdditions()
        {
            while (_pendingAdditions.TryDequeue(out var handler))
            {
                AddHandlerDirectly(handler);
            }
        }

        private void ProcessPendingRemovals()
        {
            while (_pendingRemovals.TryDequeue(out var handler))
            {
                RemoveHandlerDirectly(handler);
            }
        }

        private void NotifyHandlers(ConcurrentQueue<Action<DictionaryChangeArgs<TKey, TValue>>> handlers,
            DictionaryChangeArgs<TKey, TValue> args)
        {
            // 使用快照避免在遍历时修改
            var snapshot = handlers.ToArray();
            foreach (var handler in snapshot)
            {
                try
                {
                    handler?.Invoke(args);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error in dictionary change notification: {e}");
                }
            }
        }

        private void RemoveObserver(Action<DictionaryChangeArgs<TKey, TValue>> handler)
        {
            if (handler == null) return;

            if (Interlocked.CompareExchange(ref _isNotifying, 1, 1) == 1)
            {
                _pendingRemovals.Enqueue(handler);
            }
            else
            {
                RemoveHandlerDirectly(handler);
            }
        }

        private class DictionarySubscription : IDisposable
        {
            private HReactiveDictionary<TKey, TValue> _dictionary;
            private Action<DictionaryChangeArgs<TKey, TValue>> _handler;

            public DictionarySubscription(HReactiveDictionary<TKey, TValue> dictionary,
                Action<DictionaryChangeArgs<TKey, TValue>> handler)
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