using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.Message;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using UnityEngine;
using VContainer;

namespace AOTScripts.Data.NetworkMes
{
    public class MirrorNetworkMessageHandler : NetworkBehaviour
    {
        private MessageCenter _messageCenter;
        private readonly Dictionary<Type, Delegate> _serverHandlers = new Dictionary<Type, Delegate>();
        private readonly Dictionary<Type, Delegate> _clientHandlers = new Dictionary<Type, Delegate>();
        private readonly ConcurrentDictionary<(string type, long id), DateTime> _lastMessageSent = new ConcurrentDictionary<(string type, long id), DateTime>();
        private bool _serverHandler;
        private bool _clientHandler;
        
        private readonly TimeSpan MESSAGE_EXPIRATION = TimeSpan.FromSeconds(2);
        
        [Inject]
        private void Init(MessageCenter messageCenter)
        {
            _messageCenter = messageCenter;
        }


        public void SendToServer<T>(T msg) where T : struct, NetworkMessage
        {
            if (_clientHandler)
            {
                NetworkClient.Send(msg);
            }
        }

        public void SendToAllClients<T>(T msg) where T : struct, NetworkMessage
        {
            if (_serverHandler)
            {
                NetworkServer.SendToAll(msg);
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _serverHandler = true;
            RegisterServerHandlers();
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _clientHandler = true;
            RegisterClientHandlers();
        }

        private void RegisterServerHandlers()
        {
            RegisterServerHandler<MirrorPickerPickUpCollectMessage>();
            RegisterServerHandler<MirrorPickerPickUpChestMessage>();
            RegisterServerHandler<MirrorPlayerInputMessage>();
            RegisterServerHandler<MirrorPlayerInputInfoMessage>();
            // 注册更多服务器消息处理程序...
        }

        private void RegisterClientHandlers()
        {
            RegisterClientHandler<MirrorCountdownMessage>();
            RegisterClientHandler<MirrorGameStartMessage>();
            RegisterClientHandler<MirrorGameWarmupMessage>();
            RegisterClientHandler<MirrorFrameUpdateMessage>();
            RegisterClientHandler<MirrorFrameAttackResultMessage>();
            RegisterClientHandler<MirrorPlayerConnectedMessage>();
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

        private void CleanupExpiredMessages(DateTime now)
        {
            var expiredMessages = _lastMessageSent
                .Where(m => now - m.Value > MESSAGE_EXPIRATION)
                .Select(m => m.Key)
                .ToList();

            foreach (var messageKey in expiredMessages)
            {
                _lastMessageSent.TryRemove(messageKey, out _);
            }
        }

        private IMessage ConvertToLocalMessage<T>(T networkMessage) where T : struct, NetworkMessage
        {
            // 这里实现网络消息到本地消息的转换逻辑
            if (networkMessage is MirrorGameStartMessage gameStartMessage)
            {
                return new GameStartMessage((MapType)gameStartMessage.mapType, (GameMode)gameStartMessage.gameMode, gameStartMessage.gameScore, gameStartMessage.gameTime, gameStartMessage.playerCount);
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
                return new PickerPickUpChestMessage(pickerPickUpChestMessage.PickerId, pickerPickUpChestMessage.ChestID);
            }
            
            if (networkMessage is MirrorPlayerInputMessage playerInputMessage)
            {
                return new PlayerInputMessage(playerInputMessage.playerInputInfo);
            }
            
            if (networkMessage is MirrorFrameUpdateMessage frameUpdateMessage)
            {
                return new PlayerFrameUpdateMessage(frameUpdateMessage.frame, frameUpdateMessage.playerInputs);
            }
            
            if (networkMessage is MirrorFrameAttackResultMessage frameAttackResultMessage)
            {
                return new PlayerDamageResultMessage(frameAttackResultMessage.frame, frameAttackResultMessage.damageResults);
            }

            if (networkMessage is MirrorPlayerInputInfoMessage playerInputInfoMessage)
            {
                return new PlayerInputInfoMessage(playerInputInfoMessage.connectionID, playerInputInfoMessage.input);
            }

            if (networkMessage is MirrorPlayerConnectedMessage playerConnectedMessage)
            {
                return new PlayerConnectedMessage(playerConnectedMessage.connectionID, playerConnectedMessage.spawnIndex, playerConnectedMessage.playerName);
            }
            
            // 添加更多消息类型的处理...

            Debug.LogWarning($"Unhandled network message type: {typeof(T)}");
            return null;
        }

        // 提供给其他脚本注册本地消息处理的方法
        public void RegisterLocalMessageHandler<T>(Action<T> callback) where T : IMessage
        {
            _messageCenter.Register(callback);
        }

        public void UnregisterLocalMessageHandler<T>(Action<T> callback) where T : IMessage
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