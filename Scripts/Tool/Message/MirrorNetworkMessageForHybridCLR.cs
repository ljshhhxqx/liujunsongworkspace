using System;
using AOTScripts.Tool;
using MemoryPack;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Tool.Message
{
    /// <summary>
    /// 热更新网络管理器
    /// 在热更新中实现，注册到 AOT 事件系统
    /// </summary>
    public class HotUpdateNetworkManager : SingletonAutoMono<HotUpdateNetworkManager>
    {
        
        /// <summary>
        /// 注册到 AOT 层的事件系统
        /// 这是关键：热更新代码注册到 AOT 事件
        /// </summary>
        private void RegisterToAOTEvents()
        {
            // 热更新注册到 AOT 事件 - 这是允许的
            NetworkEventBridge.OnCommandReceived += OnCommandReceivedFromAOT;
            NetworkEventBridge.OnClientRpcReceived += OnClientRpcReceivedFromAOT;
            NetworkEventBridge.OnTargetRpcReceived += OnTargetRpcReceivedFromAOT;
            
            Debug.Log("热更新网络管理器已注册到 AOT 事件系统");
        }
        
        private void OnDestroy()
        {
            // 取消注册
            NetworkEventBridge.OnCommandReceived -= OnCommandReceivedFromAOT;
            NetworkEventBridge.OnClientRpcReceived -= OnClientRpcReceivedFromAOT;
            NetworkEventBridge.OnTargetRpcReceived -= OnTargetRpcReceivedFromAOT;
        }
        
        // 这些方法会被 AOT 层通过委托调用
        private void OnCommandReceivedFromAOT(byte[] data, string dataType, NetworkConnection connection)
        {
            // 处理来自 AOT 的 Command 消息
            Debug.Log($"[热更新] 收到 Command: {dataType}");
            ProcessNetworkMessage(data, dataType, connection, MessageType.Command);
        }
        
        private void OnClientRpcReceivedFromAOT(byte[] data, string dataType)
        {
            // 处理来自 AOT 的 ClientRpc 消息
            Debug.Log($"[热更新] 收到 ClientRpc: {dataType}");
            ProcessNetworkMessage(data, dataType, null, MessageType.ClientRpc);
        }
        
        private void OnTargetRpcReceivedFromAOT(byte[] data, string dataType)
        {
            // 处理来自 AOT 的 TargetRpc 消息
            Debug.Log($"[热更新] 收到 TargetRpc: {dataType}");
            ProcessNetworkMessage(data, dataType, null, MessageType.TargetRpc);
        }
        
        private void ProcessNetworkMessage(byte[] data, string dataType, NetworkConnection connection, MessageType messageType)
        {
            // 热更新内部的消息处理逻辑
            // 使用 MemoryPack 反序列化并调用相应的处理器
            try
            {
                // 这里是热更新内部的处理，不涉及 AOT
                var message = DeserializeMessage(data, dataType);
                DispatchToHandlers(message, dataType, connection, messageType);
            }
            catch (Exception e)
            {
                Debug.LogError($"[热更新] 处理网络消息失败: {e}");
            }
        }
        
        private object DeserializeMessage(byte[] data, string dataType)
        {
            // 在热更新内部使用 MemoryPack 非泛型方法
            // 这里可以安全地使用热更新类型
            var type = Type.GetType(dataType);
            if (type != null)
            {
                return MemoryPackSerializer.Deserialize(type, data);
            }
            throw new ArgumentException($"未知的数据类型: {dataType}");
        }
        
        private void DispatchToHandlers(object message, string dataType, NetworkConnection connection, MessageType messageType)
        {
            // 热更新内部的消息分发逻辑
            // 这里调用在热更新中注册的处理器
        }
        
        private enum MessageType
        {
            Command,
            ClientRpc, 
            TargetRpc
        }
    }
    
    /// <summary>
    /// 热更新网络实体基类
    /// 继承自 AOT 的 HotUpdateNetworkBehaviour
    /// </summary>
    public abstract class HotUpdateNetworkEntity : HotUpdateNetworkBehaviour
    {
        // 提供类型安全的发送方法
        public void SendHotUpdateCommand<T>(T data) where T : struct
        {
            // 调用基类的 AOT 方法
            var bytes = MemoryPackSerializer.Serialize(typeof(T), data);
            SendCommandToServer(bytes, typeof(T).FullName);
        }
        
        public void SendHotUpdateRpc<T>(T data) where T : struct
        {
            var bytes = MemoryPackSerializer.Serialize(typeof(T), data);
            SendRpcToClients(bytes, typeof(T).FullName);
        }
        
        public void SendHotUpdateTargetRpc<T>(NetworkConnection target, T data) where T : struct
        {
            var bytes = MemoryPackSerializer.Serialize(typeof(T), data);
            SendTargetRpc(target, bytes, typeof(T).FullName);
        }
    }
}