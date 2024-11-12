using System;
using System.Collections.Generic;

namespace Tool.GameEvent
{
    public class GameEventManager
    {
        
        private Dictionary<Type, Action<GameEvent>> eventListeners = new Dictionary<Type, Action<GameEvent>>();
        private Dictionary<Delegate, Action<GameEvent>> listenerMapping = new Dictionary<Delegate, Action<GameEvent>>();
        
        public GameEventManager()
        {
            //Debug.Log("GameEventManager created");
            GameEventExtensions.RegisterGameEventWriteRead();
        }

        public void Subscribe<T>(Action<T> listener) where T : GameEvent
        {
            var eventType = typeof(T);
            Action<GameEvent> internalListener = e => listener((T)e);

            if (!eventListeners.ContainsKey(eventType))
            {
                eventListeners[eventType] = internalListener;
            }
            else
            {
                eventListeners[eventType] += internalListener;
            }

            listenerMapping[listener] = internalListener;

            //Debug.Log($"Subscribed to event {eventType.Name}");
            //Debug.Log($"Listener count for event {eventType.Name}: {eventListeners[eventType].GetInvocationList().Length}");
        }

        public void Unsubscribe<T>(Action<T> listener) where T : GameEvent
        {
            Type eventType = typeof(T);
            if (listenerMapping.TryGetValue(listener, out Action<GameEvent> internalListener))
            {
                if (eventListeners.ContainsKey(eventType))
                {
                    eventListeners[eventType] -= internalListener;

                    if (eventListeners[eventType] == null)
                    {
                        eventListeners.Remove(eventType);
                    }
                }

                listenerMapping.Remove(listener);
               // Debug.Log($"Unsubscribed from event {eventType.Name}");
            }
            else
            {
                //Debug.LogWarning($"Listener not found for event {eventType.Name}");
            }
        }

        public void Publish<T>(T gameEvent) where T : GameEvent
        {
            Type eventType = typeof(T);
            if (eventListeners.ContainsKey(eventType))
            {
                //Debug.Log($"Publishing event {eventType.Name}");
                eventListeners[eventType].Invoke(gameEvent);
            }
        }
    }

}