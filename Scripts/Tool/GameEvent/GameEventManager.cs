using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.GameEvent
{
    public class GameEventManager
    {
        private readonly Dictionary<Type, Action<IGameEvent>> _eventListeners = new Dictionary<Type, Action<IGameEvent>>();
        private readonly Dictionary<Delegate, Action<IGameEvent>> _listenerMapping = new Dictionary<Delegate, Action<IGameEvent>>();
        
        public GameEventManager()
        {
            GameEventExtensions.RegisterGameEventWriteRead();
        }

        public void Subscribe<T>(Action<T> listener) where T : IGameEvent
        {
            var eventType = typeof(T);
            Action<IGameEvent> internalListener = e => listener((T)e);

            if (!_eventListeners.ContainsKey(eventType))
            {
                _eventListeners[eventType] = internalListener;
            }
            else
            {
                _eventListeners[eventType] += internalListener;
            }

            _listenerMapping[listener] = internalListener;
        }

        public void Unsubscribe<T>(Action<T> listener) where T : IGameEvent
        {
            Type eventType = typeof(T);
            if (_listenerMapping.TryGetValue(listener, out Action<IGameEvent> internalListener))
            {
                if (_eventListeners.ContainsKey(eventType))
                {
                    _eventListeners[eventType] -= internalListener;

                    if (_eventListeners[eventType] == null)
                    {
                        _eventListeners.Remove(eventType);
                    }
                }

                _listenerMapping.Remove(listener);
                Debug.Log($"Unsubscribed from event {eventType.Name}");
            }
            else
            {
                Debug.LogWarning($"Listener not found for event {eventType.Name}");
            }
        }

        public void Publish<T>(T gameEvent) where T : IGameEvent
        {
            Type eventType = typeof(T);
            if (_eventListeners.TryGetValue(eventType, out var listener))
            {
                Debug.Log($"Publishing event {eventType.Name}");
                listener.Invoke(gameEvent);
            }
        }
    }

}