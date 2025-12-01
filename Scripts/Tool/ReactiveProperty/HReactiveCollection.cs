using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.ReactiveProperty
{
    public class HReactiveCollection<T> : IList<T>, INotifyCollectionChanged
    {
        private readonly List<T> _items = new List<T>();
        
        // 使用链表存储监听器以避免并发修改问题
        private LinkedList<NotifyCollectionChangedEventHandler> _collectionChangedHandlers = new LinkedList<NotifyCollectionChangedEventHandler>();
        private bool _isNotifying = false;
        private readonly LinkedList<NotifyCollectionChangedEventHandler> _pendingAdditions = new LinkedList<NotifyCollectionChangedEventHandler>();
        private readonly LinkedList<NotifyCollectionChangedEventHandler> _pendingRemovals = new LinkedList<NotifyCollectionChangedEventHandler>();

        public event NotifyCollectionChangedEventHandler CollectionChanged
        {
            add
            {
                if (value == null) return;
                
                if (_isNotifying)
                {
                    lock (_pendingAdditions)
                    {
                        _pendingAdditions.AddLast(value);
                    }
                }
                else
                {
                    _collectionChangedHandlers.AddLast(value);
                }
            }
            remove
            {
                if (value == null) return;
                
                if (_isNotifying)
                {
                    lock (_pendingRemovals)
                    {
                        _pendingRemovals.AddLast(value);
                    }
                }
                else
                {
                    _collectionChangedHandlers.Remove(value);
                }
            }
        }

        public T this[int index]
        {
            get => _items[index];
            set
            {
                var oldItem = _items[index];
                if (!EqualityComparer<T>.Default.Equals(oldItem, value))
                {
                    _items[index] = value;
                    OnCollectionChanged(NotifyCollectionChangedAction.Replace, value, oldItem, index);
                }
            }
        }

        public int Count => _items.Count;
        public bool IsReadOnly => false;

        public void Add(T item)
        {
            _items.Add(item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, _items.Count - 1);
        }

        public void AddRange(IEnumerable<T> collection)
        {
            if (collection == null) return;

            int startIndex = _items.Count;
            _items.AddRange(collection);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, collection, startIndex);
        }

        public void Insert(int index, T item)
        {
            _items.Insert(index, item);
            OnCollectionChanged(NotifyCollectionChangedAction.Add, item, index);
        }

        public bool Remove(T item)
        {
            int index = _items.IndexOf(item);
            if (index >= 0)
            {
                RemoveAt(index);
                return true;
            }
            return false;
        }

        public void RemoveAt(int index)
        {
            var oldItem = _items[index];
            _items.RemoveAt(index);
            OnCollectionChanged(NotifyCollectionChangedAction.Remove, oldItem, index);
        }

        public void Clear()
        {
            var oldItems = _items.ToArray();
            _items.Clear();
            OnCollectionChanged(NotifyCollectionChangedAction.Reset, oldItems);
        }

        public bool Contains(T item) => _items.Contains(item);
        public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
        public int IndexOf(T item) => _items.IndexOf(item);
        public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

        /// <summary>
        /// 观察集合变化（类型安全的热更新内部方法）
        /// </summary>
        public IDisposable Observe(Action<NotifyCollectionChangedEventArgs> handler)
        {
            if (handler == null) return null;
            
            NotifyCollectionChangedEventHandler eventHandler = (sender, args) => handler(args);
            
            if (_isNotifying)
            {
                lock (_pendingAdditions)
                {
                    _pendingAdditions.AddLast(eventHandler);
                }
            }
            else
            {
                _collectionChangedHandlers.AddLast(eventHandler);
            }
            
            return new CollectionSubscription(this, eventHandler);
        }

        /// <summary>
        /// 观察添加操作
        /// </summary>
        public IDisposable ObserveAdd(Action<T, int> onAdded)
        {
            return Observe(args =>
            {
                if (args.Action == NotifyCollectionChangedAction.Add && args.NewItems != null)
                {
                    for (int i = 0; i < args.NewItems.Count; i++)
                    {
                        if (args.NewItems[i] is T item)
                        {
                            onAdded?.Invoke(item, args.NewStartingIndex + i);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 观察移除操作
        /// </summary>
        public IDisposable ObserveRemove(Action<T, int> onRemoved)
        {
            return Observe(args =>
            {
                if (args.Action == NotifyCollectionChangedAction.Remove && args.OldItems != null)
                {
                    for (int i = 0; i < args.OldItems.Count; i++)
                    {
                        if (args.OldItems[i] is T item)
                        {
                            onRemoved?.Invoke(item, args.OldStartingIndex + i);
                        }
                    }
                }
            });
        }

        /// <summary>
        /// 批量操作，减少通知次数
        /// </summary>
        public void BatchUpdate(Action<HReactiveCollection<T>> updateAction)
        {
            if (updateAction == null) return;

            var oldHandlers = _collectionChangedHandlers;
            _collectionChangedHandlers = new LinkedList<NotifyCollectionChangedEventHandler>();

            try
            {
                updateAction(this);
                OnCollectionChanged(NotifyCollectionChangedAction.Reset, null);
            }
            finally
            {
                _collectionChangedHandlers = oldHandlers;
            }
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedAction action, object item = null, int index = -1)
        {
            var args = new NotifyCollectionChangedEventArgs(action, item, index);
            OnCollectionChanged(args);
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedAction action, object newItem, object oldItem, int index)
        {
            var args = new NotifyCollectionChangedEventArgs(action, newItem, oldItem, index);
            OnCollectionChanged(args);
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedAction action, IEnumerable items, int startingIndex)
        {
            var args = new NotifyCollectionChangedEventArgs(action, items, startingIndex);
            OnCollectionChanged(args);
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedAction action, IList oldItems)
        {
            var args = new NotifyCollectionChangedEventArgs(action, oldItems);
            OnCollectionChanged(args);
        }

        protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs args)
        {
            if (_collectionChangedHandlers.Count == 0 && _pendingAdditions.Count == 0)
                return;

            _isNotifying = true;

            try
            {
                var currentNode = _collectionChangedHandlers.First;
                while (currentNode != null)
                {
                    var nextNode = currentNode.Next;
                    try
                    {
                        currentNode.Value?.Invoke(this, args);
                    }
                    catch (Exception e)
                    {
                        Debug.LogError($"Error in collection change notification: {e}");
                    }
                    currentNode = nextNode;
                }

                ProcessPendingOperations();
            }
            finally
            {
                _isNotifying = false;
            }
        }

        private void ProcessPendingOperations()
        {
            // 处理待添加的监听器
            lock (_pendingAdditions)
            {
                var currentNode = _pendingAdditions.First;
                while (currentNode != null)
                {
                    var nextNode = currentNode.Next;
                    _collectionChangedHandlers.AddLast(currentNode);
                    _pendingAdditions.Remove(currentNode);
                    currentNode = nextNode;
                }
            }

            // 处理待移除的监听器
            lock (_pendingRemovals)
            {
                var currentNode = _pendingRemovals.First;
                while (currentNode != null)
                {
                    var nextNode = currentNode.Next;
                    _collectionChangedHandlers.Remove(currentNode);
                    _pendingRemovals.Remove(currentNode);
                    currentNode = nextNode;
                }
            }
        }

        private void RemoveObserver(NotifyCollectionChangedEventHandler handler)
        {
            if (handler == null) return;
            
            if (_isNotifying)
            {
                lock (_pendingRemovals)
                {
                    _pendingRemovals.AddLast(handler);
                }
            }
            else
            {
                _collectionChangedHandlers.Remove(handler);
            }
        }

        private class CollectionSubscription : IDisposable
        {
            private HReactiveCollection<T> _collection;
            private NotifyCollectionChangedEventHandler _handler;

            public CollectionSubscription(HReactiveCollection<T> collection, NotifyCollectionChangedEventHandler handler)
            {
                _collection = collection;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_collection != null && _handler != null)
                {
                    _collection.RemoveObserver(_handler);
                    _collection = null;
                    _handler = null;
                }
            }
        }
    }
}