using AOTScripts.Tool.ECS;
using Game.Map;
using Mirror;
using Tool.GameEvent;
using UI.UIBase;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Random = UnityEngine.Random;

namespace Network.Server
{
    public class NetworkManagerCustom : NetworkManager, IInjectableObject
    {
        private GameEventManager _gameEventManager;
        private NetworkStartPosition[] _spawnPoints;
        private NetworkManagerHUD _networkManagerHUD;
        private UIManager _uiManager;
        private IObjectResolver _objectResolver;

        [Inject]
        private void Init(GameEventManager gameEventManager, UIManager uIManager, IObjectResolver objectResolver)
        {
            _gameEventManager = gameEventManager;
            _spawnPoints = FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None);
            _networkManagerHUD = GetComponent<NetworkManagerHUD>();
            _networkManagerHUD.enabled = false;
            _gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnSceneResourcesLoaded);
            _objectResolver = objectResolver;
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
        public override async void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            // 生成物体
            var spawnedObjectsAddress = DataJsonManager.Instance.GetResourceData("SpawnedObjects");
            var spawnedObjects = await ResourceManager.Instance.LoadResourceAsync<GameObject>(spawnedObjectsAddress);
            var contorller = spawnedObjects.GetComponent<NetworkMonoController>();
            _objectResolver.Inject(contorller);
            _gameEventManager.Publish(new GameReadyEvent("MainGame"));
            // 服务器端添加玩家
            var res = DataJsonManager.Instance.GetResourceData("Player");
            var resInfo = ResourceManager.Instance.GetResource<GameObject>(res);
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