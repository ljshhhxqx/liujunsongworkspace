using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Network.Client.Player;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Player
{
    public class PlayerManager : NetworkBehaviour
    {
        private readonly Dictionary<uint, PlayerControlClient> _players = new Dictionary<uint, PlayerControlClient>();
        public PlayerControlClient LocalPlayer { get; private set; }
        
        [Inject]
        private void Init()
        {
            
        }
        
        public void GetPlayerContorl(PlayerControlClient player)
        {
            
        }

        // public override void OnStartServer()
        // {
        //     NetworkServer.OnConnectedEvent += OnServerConnect;
        //     NetworkServer.OnDisconnectedEvent += OnServerDisconnect;
        // }
        //
        // public override void OnStopServer()
        // {
        //     base.OnStopServer();
        //     NetworkServer.OnConnectedEvent -= OnServerConnect;
        //     NetworkServer.OnDisconnectedEvent -= OnServerDisconnect;
        // }
        //
        //
        // public override void OnStartClient()
        // {
        //     base.OnStartClient();
        //     // 客户端监听玩家Spawn/Despawn事件
        //     ClientScene.
        // }
        //
        // public override void OnStopClient()
        // {
        //     base.OnStopClient();
        //     NetworkClient.onSpawn -= OnClientSpawn;
        //     NetworkClient.onUnspawn -= OnClientUnspawn;
        // }
    }
}
