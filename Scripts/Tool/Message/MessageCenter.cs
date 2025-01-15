using System;
using System.Collections.Generic;
using System.Linq;
using Tool.Message;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.Message
{
    public interface INetworkMessageCenter
    {
        void Register<T>(Action<T> callback) where T : IMessage;
        void Unregister<T>(Action<T> callback) where T : IMessage;
        void Post<T>(T message) where T : IMessage;
    }
    
    public class MessageCenter : INetworkMessageCenter
    {
        private readonly Dictionary<Type, Queue<Delegate>> listeners = new Dictionary<Type, Queue<Delegate>>();

        // 注册事件
        public void Register<T>(Action<T> callback) where T : IMessage
        {
            var t = typeof(T);
            if (!listeners.ContainsKey(t))
            {
                listeners[t] = new Queue<Delegate>();
            }
            listeners[t].Enqueue(callback);
        }

        // 注销事件
        public void Unregister<T>(Action<T> callback) where T : IMessage
        {
            var t = typeof(T);
            if (listeners.ContainsKey(t))
            {
                var newQueue = new Queue<Delegate>(listeners[t].Where(d => !d.Equals(callback)));
                if (newQueue.Count == 0)
                {
                    listeners.Remove(t);
                }
                else
                {
                    listeners[t] = newQueue;
                }
            }
        }

        // 发送消息
        public void Post<T>(T message) where T : IMessage
        {
            if (listeners.TryGetValue(message.GetType(), out var queue))
            {
                var count = queue.Count;
                for (var i = 0; i < count; i++)
                {
                    var typedDelegate = queue.Dequeue(); // 取出队列的第一个元素
                    var name = typedDelegate.Method.Name;
                    try
                    {
                        typedDelegate.DynamicInvoke(message); // 调用委托
                        //Debug.Log($"Message posted and handler invoked for type: {name}");
                        queue.Enqueue(typedDelegate);
                    }
                    catch (Exception ex)
                    {
                        Debug.LogError($"Error invoking handler for message type {name}: {ex}");
                    }
                }
            }
            else
            {
                Debug.LogError($"No handler registered for message type {message.GetType().Name}");
            }
        }
    }
}