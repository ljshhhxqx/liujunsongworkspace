using System;
using System.Collections.Generic;
using System.Linq;

namespace Tool.Message
{
    public interface INetworkMessageCenter
    {
        void Register<T>(Action<T> callback) where T : Message;
        void Unregister<T>(Action<T> callback) where T : Message;
        void Post<T>(T message) where T : Message;
    }
    
    public class MessageCenter : INetworkMessageCenter
    {
        private readonly Dictionary<Type, Queue<Delegate>> listeners = new Dictionary<Type, Queue<Delegate>>();

        // 注册事件
        public void Register<T>(Action<T> callback) where T : Message
        {
            var t = typeof(T);
            if (!listeners.ContainsKey(t))
            {
                listeners[t] = new Queue<Delegate>();
            }
            listeners[t].Enqueue(callback);
        }

        // 注销事件
        public void Unregister<T>(Action<T> callback) where T : Message
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
        public void Post<T>(T message) where T : Message
        {
            if (listeners.TryGetValue(message.GetType(), out var queue))
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