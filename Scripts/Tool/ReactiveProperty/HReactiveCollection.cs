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
        
        // 事件在热更新内部处理
        public event NotifyCollectionChangedEventHandler CollectionChanged;

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
            CollectionChanged += (sender, args) => handler(args);
            return new CollectionSubscription(this, handler);
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
            var oldHandlers = CollectionChanged;
            CollectionChanged = null;

            try
            {
                updateAction?.Invoke(this);
                OnCollectionChanged(NotifyCollectionChangedAction.Reset, null);
            }
            finally
            {
                CollectionChanged = oldHandlers;
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
            try
            {
                CollectionChanged?.Invoke(this, args); // 热更新内部事件调用
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in collection change notification: {e}");
            }
        }

        private class CollectionSubscription : IDisposable
        {
            private HReactiveCollection<T> _collection;
            private Action<NotifyCollectionChangedEventArgs> _handler;

            public CollectionSubscription(HReactiveCollection<T> collection, Action<NotifyCollectionChangedEventArgs> handler)
            {
                _collection = collection;
                _handler = handler;
            }

            public void Dispose()
            {
                if (_collection != null && _handler != null)
                {
                    // 需要从事件中移除，这里简化处理
                    // 实际实现需要维护handler列表
                    _collection = null;
                    _handler = null;
                }
            }
        }
    }
}