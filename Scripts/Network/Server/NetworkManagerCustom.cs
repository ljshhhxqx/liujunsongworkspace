using System;
using Game.Map;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace Network.Server
{
    public class NetworkManagerCustom : NetworkManager, IInjectableObject
    {
        private GameEventManager _gameEventManager;
        private ResourceManager _resourceManager;
        private NetworkStartPosition[] _spawnPoints;
        private NetworkManagerHUD _networkManagerHUD;

        [Inject]
        private void Init(ResourceManager resourceManager, GameEventManager gameEventManager)
        {
            _resourceManager = resourceManager;
            _gameEventManager = gameEventManager;
            _spawnPoints = FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None);
            _networkManagerHUD = GetComponent<NetworkManagerHUD>();
            _networkManagerHUD.enabled = false;
            _gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnSceneResourcesLoaded);
            //this.playerManager = playerManager;
        }

        private void OnSceneResourcesLoaded(GameSceneResourcesLoadedEvent sceneResourcesLoadedEvent)
        {
            if (sceneResourcesLoadedEvent.SceneName == "MainGame")
            {
                _networkManagerHUD.enabled = true;
            }
        }

        [Server]
        public override void OnStartServer()
        {
            // 监听服务器上的连接和断开事件
            NetworkServer.OnConnectedEvent += HandleServerConnected;
            NetworkServer.OnDisconnectedEvent += HandleServerDisconnected;
        }

        [Server]
        public override void OnStopServer()
        {
            // 移除监听器
            NetworkServer.OnConnectedEvent -= HandleServerConnected;
            NetworkServer.OnDisconnectedEvent -= HandleServerDisconnected;
        }

        [Server]
        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var res = DataJsonManager.Instance.GetResourceData("Player");
            var resInfo = _resourceManager.GetResource<GameObject>(res);
            if (resInfo)
            {
                //currentPlayer = resInfo.gameObject;
                var spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Length)];
                var player = Instantiate(resInfo.gameObject, spawnPoint.transform);
                player.transform.localPosition = Vector3.zero;
                player.transform.localRotation = Quaternion.identity;
                NetworkServer.AddPlayerForConnection(conn, player);
            }
        }

        [Server]
        private void HandleServerConnected(NetworkConnection conn)
        {
            // 确保只有服务器执行注册玩家的逻辑
            //playerManager.RegisterPlayer(conn);
            Debug.Log($"Player connected: {conn.connectionId}");
        }

        [Server]
        private void HandleServerDisconnected(NetworkConnection conn)
        {
            // 确保只有服务器执行注销玩家的逻辑
            //playerManager.UnregisterPlayer(conn.connectionId);
            Debug.Log($"Player disconnected: {conn.connectionId}");
        }

        public override void OnDestroy()
        {
            _gameEventManager.Unsubscribe<GameSceneResourcesLoadedEvent>(OnSceneResourcesLoaded);
        }
    }
}