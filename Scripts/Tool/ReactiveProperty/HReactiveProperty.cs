using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.ReactiveProperty
{
    public class HReactiveProperty<T>
    {
        private T _value;
        private readonly List<Action<T>> _listeners = new List<Action<T>>();
        private bool _isNotifying = false;
        private readonly List<Action<T>> _pendingAdditions = new List<Action<T>>();
        private readonly List<Action<T>> _pendingRemovals = new List<Action<T>>();

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
                throw new ArgumentNullException(nameof(listener));

            // 确保订阅操作在热更新内部完成
            if (_isNotifying)
            {
                _pendingAdditions.Add(listener);
            }
            else
            {
                _listeners.Add(listener);
            }

            if (notifyImmediately)
            {
                SafeInvoke(listener, _value);
            }

            return new Subscription(this, listener);
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
            _pendingAdditions.Clear();
            _pendingRemovals.Clear();
            _value = default(T);
        }

        private void Unsubscribe(Action<T> listener)
        {
            if (_isNotifying)
            {
                _pendingRemovals.Add(listener);
            }
            else
            {
                _listeners.Remove(listener);
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
                foreach (var listener in _listeners)
                {
                    SafeInvoke(listener, _value);
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
            foreach (var listener in _pendingAdditions)
            {
                if (!_listeners.Contains(listener))
                {
                    _listeners.Add(listener);
                }
            }
            _pendingAdditions.Clear();

            foreach (var listener in _pendingRemovals)
            {
                _listeners.Remove(listener);
            }
            _pendingRemovals.Clear();
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
            private Action<T> _listener;

            public Subscription(HReactiveProperty<T> property, Action<T> listener)
            {
                _property = property;
                _listener = listener;
            }

            public void Dispose()
            {
                if (_property != null && _listener != null)
                {
                    _property.Unsubscribe(_listener);
                    _property = null;
                    _listener = null;
                }
            }
        }
    }
}