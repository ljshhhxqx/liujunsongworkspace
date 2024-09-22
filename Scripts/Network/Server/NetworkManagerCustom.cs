using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using Game.Map;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using Network.Data;
using Network.NetworkMes;
using Tool.GameEvent;
using UI.UIBase;
using UnityEngine;
using VContainer;

namespace Network.Server
{
    public class NetworkManagerCustom : NetworkManager, IInjectableObject
    {
        private GameEventManager _gameEventManager;
        private List<NetworkStartPosition> _spawnPoints;
        private NetworkManagerHUD _networkManagerHUD;
        private UIManager _uiManager;
        private IObjectResolver _objectResolver;
        private readonly Dictionary<int, string> _playerAccountIdMap = new Dictionary<int, string>();
        private PlayerInGameManager _playerInGameManager;
        private NetworkManagerRpcCaller _networkManagerRpcCaller;

        [Inject]
        private void Init(GameEventManager gameEventManager, UIManager uIManager, IObjectResolver objectResolver, PlayerInGameManager playerInGameManager)
        {
            _gameEventManager = gameEventManager;
            _spawnPoints = FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None).ToList();
            _networkManagerHUD = GetComponent<NetworkManagerHUD>();
            _networkManagerRpcCaller = GetComponent<NetworkManagerRpcCaller>();
            _networkManagerHUD.enabled = false;
            _gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnSceneResourcesLoaded);
            _objectResolver = objectResolver;
            _playerInGameManager = playerInGameManager;
            _objectResolver.Inject(_networkManagerRpcCaller);
            //this.playerManager = playerManager;
        }

        private void OnSceneResourcesLoaded(GameSceneResourcesLoadedEvent sceneResourcesLoadedEvent)
        {
            if (sceneResourcesLoadedEvent.SceneName == "MainGame")
            {
                _networkManagerHUD.enabled = true;
            }
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            // 监听服务器上的连接和断开事件
            NetworkServer.OnConnectedEvent += HandleServerConnected;
            NetworkServer.OnDisconnectedEvent += HandleServerDisconnected;
            NetworkServer.RegisterHandler<PlayerConnectMessage>(OnServerPlayerAccountIdMessage);
        }

        private void OnServerPlayerAccountIdMessage(NetworkConnectionToClient conn, PlayerConnectMessage message)
        { 
            // 获取已添加的玩家对象
            if (conn.identity != null)
            {
                var player = conn.identity.gameObject;
                var playerData = player.GetComponent<PlayerPropertyComponent>();
                if (playerData != null)
                {
                    playerData.PlayerId = message.UID;
                    _playerAccountIdMap[conn.connectionId] = message.UID;
                    _playerInGameManager.InitPlayerProperty(playerData);
                }
                
                // 服务器端添加玩家
                var res = DataJsonManager.Instance.GetResourceData("Player");
                var resInfo = ResourceManager.Instance.GetResource<GameObject>(res);
                if (resInfo)
                {
                    //currentPlayer = resInfo.gameObject;
                    var spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Count)];
                    var playerGo = Instantiate(resInfo.gameObject, spawnPoint.transform);
                    playerGo.transform.localPosition = Vector3.zero;
                    playerGo.transform.localRotation = Quaternion.identity;
                    NetworkServer.AddPlayerForConnection(conn, player);
                    _spawnPoints.Remove(spawnPoint);
                }
            }

            if (_playerAccountIdMap.Count == _playerInGameManager.GetPlayers().Count)
            {
                _networkManagerRpcCaller.SendGameReadyMessageRpc();
            }
            Debug.Log("Received PlayerAccountId from client: " + message.UID);
        }

        public override void OnStopServer()
        {
            base.OnStopServer();
            // 移除监听器
            NetworkServer.OnConnectedEvent -= HandleServerConnected;
            NetworkServer.OnDisconnectedEvent -= HandleServerDisconnected;
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            NetworkClient.RegisterHandler<PlayerConnectMessage>(OnPlayerConnectedMessage);
            // NetworkClient.RegisterHandler<PlayerDisconnectMessage>(OnPlayerDisconnectedMessage);
            NetworkClient.OnConnectedEvent += OnClientConnectToServer;
        }

        private void OnClientConnectToServer()
        {
            // 获取当前连接
            NetworkConnection conn = NetworkClient.connection;

            // 发送 PlayerAccountId 给服务器
            var msg = new PlayerConnectMessage(PlayFabData.PlayFabId.Value, conn.connectionId, PlayFabData.PlayerReadOnlyData.Value.Nickname);
            conn.Send(msg);
        }

        private void OnPlayerConnectedMessage(PlayerConnectMessage message)
        {
            Debug.Log($"Received PlayerAccountId: {message.UID} - {message.Name} - {message.ConnectionID.ToString()}");
        }

        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _playerAccountIdMap.Remove(conn.connectionId);
            _playerInGameManager.RemovePlayer(conn.connectionId);
            Debug.Log($"Player disconnected: {conn.connectionId}");
        }

        private void HandleServerConnected(NetworkConnection conn)
        {
            // 确保只有服务器执行注册玩家的逻辑
            //playerManager.RegisterPlayer(conn);
            Debug.Log($"Player connected: {conn.connectionId}");
            
        }

        private void HandleServerDisconnected(NetworkConnection conn)
        {
            // 确保只有服务器执行注销玩家的逻辑
            //_playerInGameManager.RemovePlayer(conn.connectionId);
            Debug.Log($"Player disconnected: {conn.connectionId}");
        }

        public override void OnDestroy()
        {
            _gameEventManager.Unsubscribe<GameSceneResourcesLoadedEvent>(OnSceneResourcesLoaded);
        }
    }
}