using System;
using System.Collections.Generic;
using System.Linq;

namespace Tool.Message
{
    public interface INetworkMessageCenter
    {
        void Register<T>(MessageType eventType, Action<T> callback) where T : Message;
        void Unregister<T>(MessageType eventType, Action<T> callback) where T : Message;
        void Post<T>(T message) where T : Message;
    }
    
    public class MessageCenter : INetworkMessageCenter
    {
        private readonly Dictionary<MessageType, Queue<Delegate>> listeners = new Dictionary<MessageType, Queue<Delegate>>();

        // 注册事件
        public void Register<T>(MessageType eventType, Action<T> callback) where T : Message
        {
            if (!listeners.ContainsKey(eventType))
            {
                listeners[eventType] = new Queue<Delegate>();
            }
            listeners[eventType].Enqueue(callback);
        }

        // 注销事件
        public void Unregister<T>(MessageType eventType, Action<T> callback) where T : Message
        {
            if (listeners.ContainsKey(eventType))
            {
                var newQueue = new Queue<Delegate>(listeners[eventType].Where(d => !d.Equals(callback)));
                if (newQueue.Count == 0)
                {
                    listeners.Remove(eventType);
                }
                else
                {
                    listeners[eventType] = newQueue;
                }
            }
        }

        // 发送消息
        public void Post<T>(T message) where T : Message
        {
            var eventType = message.Type;
            if (listeners.TryGetValue(eventType, out var queue))
            {
                int count = queue.Count;
                for (int i = 0; i < count; i++)
                {
                    var typedDelegate = queue.Dequeue(); // 取出队列的第一个元素
                    if (typedDelegate is Action<T> action)
                    {
                        action.Invoke(message);
                        queue.Enqueue(typedDelegate); // 处理完毕后，再将其放回队列末尾
                    }
                }
            }
        }
    }
}