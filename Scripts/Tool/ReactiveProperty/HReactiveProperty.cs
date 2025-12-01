using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.ReactiveProperty
{
    public class HReactiveProperty<T>
    {
        private T _value;
        private LinkedList<Action<T>> _listeners = new LinkedList<Action<T>>();
        private bool _isNotifying = false;
        private readonly LinkedList<Action<T>> _pendingAdditions = new LinkedList<Action<T>>();
        private readonly LinkedList<Action<T>> _pendingRemovals = new LinkedList<Action<T>>();

        public HReactiveProperty(T initialValue = default(T))
        {
            _value = initialValue;
        }

        public T Value
        {
            get => _value;
            set
            {
                if (EqualityComparer<T>.Default.Equals(_value, value))
                    return;

                _value = value;
                NotifyValueChanged();
            }
        }

        public IDisposable Subscribe(Action<T> listener, bool notifyImmediately = true)
        {
            if (listener == null)
            {
                Debug.LogWarning("Listener is null.");
                return null;
            }

            // 创建链表节点，用于后续的高效移除
            var node = new LinkedListNode<Action<T>>(listener);
            if (_isNotifying)
            {
                lock (_pendingAdditions)
                {
                    _pendingAdditions.AddLast(node);
                }
            }
            else
            {
                _listeners.AddLast(node);
            }

            if (notifyImmediately)
            {
                SafeInvoke(listener, _value);
            }

            return new Subscription(this, node);
        }

        public void SetValueAndNotify(T newValue)
        {
            _value = newValue;
            NotifyValueChanged();
        }

        public void SetValueWithoutNotify(T newValue)
        {
            _value = newValue;
        }

        public void ForceNotify()
        {
            NotifyValueChanged();
        }

        public void Dispose()
        {
            _listeners.Clear();
            lock (_pendingAdditions)
            {
                _pendingAdditions.Clear();
                _pendingRemovals.Clear();
            }
            _value = default(T);
        }

        private void Unsubscribe(LinkedListNode<Action<T>> node)
        {
            if (_isNotifying)
            {
                lock (_pendingRemovals)
                {
                    _pendingRemovals.AddLast(node);
                }
            }
            else
            {
                _listeners.Remove(node);
            }
        }

        private void NotifyValueChanged()
        {
            if (_listeners.Count == 0 && _pendingAdditions.Count == 0)
                return;

            _isNotifying = true;

            try
            {
                // 所有委托调用都在热更新内部
                var currentNode = _listeners.First;
                while (currentNode != null)
                {
                    var nextNode = currentNode.Next; // 先保存下一个节点，因为回调中可能会移除当前节点
                    SafeInvoke(currentNode.Value, _value);
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
                    _listeners.AddLast(currentNode);
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
                    _listeners.Remove(currentNode);
                    _pendingRemovals.Remove(currentNode);
                    currentNode = nextNode;
                }
            }
        }

        private void SafeInvoke(Action<T> action, T value)
        {
            try
            {
                action(value); // 纯热更新内部调用
            }
            catch (Exception e)
            {
                Debug.LogError($"Error in safe reactive property: {e}");
            }
        }

        public static implicit operator T(HReactiveProperty<T> property)
        {
            return property.Value;
        }

        private class Subscription : IDisposable
        {
            private HReactiveProperty<T> _property;
            private LinkedListNode<Action<T>> _node;

            public Subscription(HReactiveProperty<T> property, LinkedListNode<Action<T>> node)
            {
                _property = property;
                _node = node;
            }

            public void Dispose()
            {
                if (_property != null && _node != null)
                {
                    _property.Unsubscribe(_node);
                    _property = null;
                    _node = null;
                }
            }
        }
    }
}