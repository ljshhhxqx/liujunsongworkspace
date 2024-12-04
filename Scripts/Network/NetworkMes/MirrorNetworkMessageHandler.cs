using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Network.NetworkMes;
using Tool.Message;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.NetworkMes
{
    public class MirrorNetworkMessageHandler : NetworkBehaviour
    {
        private MessageCenter _messageCenter;
        private readonly Dictionary<Type, Delegate> _serverHandlers = new Dictionary<Type, Delegate>();
        private readonly Dictionary<Type, Delegate> _clientHandlers = new Dictionary<Type, Delegate>();
        
        [Inject]
        private void Init(MessageCenter messageCenter)
        {
            _messageCenter = messageCenter;
        }

        public void SendToServer<T>(T msg) where T : struct, NetworkMessage
        {
            if (isClient)
            {
                NetworkClient.Send(msg);
            }
        }

        public void SendToAllClients<T>(T msg) where T : struct, NetworkMessage
        {
            if (isServer)
            {
                NetworkServer.SendToAll(msg);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            RegisterServerHandlers();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            RegisterClientHandlers();
        }

        private void RegisterServerHandlers()
        {
            RegisterServerHandler<MirrorPickerPickUpCollectMessage>();
            RegisterServerHandler<MirrorPickerPickUpChestMessage>();
        }

        private void RegisterClientHandlers()
        {
            RegisterClientHandler<MirrorCountdownMessage>();
            RegisterClientHandler<MirrorGameStartMessage>();
            RegisterClientHandler<MirrorGameWarmupMessage>();
            // 注册更多客户端消息处理程序...
        }

        private void RegisterServerHandler<T>() where T : struct, NetworkMessage
        {
            Action<NetworkConnectionToClient, T> handler = OnServerMessageReceived;
            _serverHandlers[typeof(T)] = handler;
            NetworkServer.RegisterHandler(handler, false);
//            Debug.Log($"Registered server handler for {typeof(T)}");
        }

        private void RegisterClientHandler<T>() where T : struct, NetworkMessage
        {
            Action<T> handler = OnClientMessageReceived;
            _clientHandlers[typeof(T)] = handler;
            NetworkClient.RegisterHandler(handler, false);
            //Debug.Log($"Registered client handler for {typeof(T)}");
        }

        private void OnServerMessageReceived<T>(NetworkConnectionToClient conn, T msg) where T : struct, NetworkMessage
        {
            //Debug.Log($"Server received {typeof(T).Name}");
            ProcessMessage(msg);
        }

        private void OnClientMessageReceived<T>(T msg) where T : struct, NetworkMessage
        {
            //Debug.Log($"Client received {typeof(T).Name}");
            ProcessMessage(msg);
        }

        private void ProcessMessage<T>(T networkMessage) where T : struct, NetworkMessage
        {
            var localMessage = ConvertToLocalMessage(networkMessage);
            if (localMessage != null)
            {
                _messageCenter.Post(localMessage);
            }
        }

        private Message ConvertToLocalMessage<T>(T networkMessage) where T : struct, NetworkMessage
        {
            // 这里实现网络消息到本地消息的转换逻辑
            if (networkMessage is MirrorGameStartMessage gameStartMessage)
            {
                return new GameStartMessage(gameStartMessage.GameInfo);
            }
            if (networkMessage is MirrorCountdownMessage countdownMessage)
            {
                return new CountdownMessage(countdownMessage.RemainingTime);
            }

            if (networkMessage is MirrorGameWarmupMessage gameWarmupMessage)
            {
                return new GameWarmupMessage(gameWarmupMessage.TimeLeft);
            }
            
            if (networkMessage is MirrorPickerPickUpCollectMessage pickerPickUpMessage)
            {
                return new PickerPickUpMessage(pickerPickUpMessage.PickerID, pickerPickUpMessage.ItemID);
            }

            if (networkMessage is MirrorPickerPickUpChestMessage pickerPickUpChestMessage)
            {
                return new PickerPickUpChestMessage(pickerPickUpChestMessage.PickerID,
                    pickerPickUpChestMessage.ChestID);
            }
            // 添加更多消息类型的处理...

            Debug.LogWarning($"Unhandled network message type: {typeof(T)}");
            return null;
        }

        // 提供给其他脚本注册本地消息处理的方法
        public void RegisterLocalMessageHandler<T>(Action<T> callback) where T : Message
        {
            _messageCenter.Register(callback);
        }

        public void UnregisterLocalMessageHandler<T>(Action<T> callback) where T : Message
        {
            _messageCenter.Unregister(callback);
        }

        // [Client]
        // public void HandleMessage(NetworkMessage netMsg)
        // {
        //     
        // }
        //
        // [Server]
        // public void HandleMessage(NetworkMessage netMsg)
        // {
        // }
    }
}