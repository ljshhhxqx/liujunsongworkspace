using System;
using System.Collections.Generic;
using System.Linq;
using Data;
using Game.Map;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using Mirror;
using Network.NetworkMes;
using Tool.GameEvent;
using UI.UIBase;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using PlayerInGameData = HotUpdate.Scripts.Network.Server.InGame.PlayerInGameData;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Network.Server
{
    public class NetworkManagerCustom : NetworkManager, IInjectableObject
    {
        private GameEventManager _gameEventManager;
        private List<NetworkStartPosition> _spawnPoints;
        private NetworkManagerHUD _networkManagerHUD;
        private UIManager _uiManager;
        private IObjectResolver _objectResolver;
        private readonly Dictionary<int, string> _playerAccountIdMap = new Dictionary<int, string>();
        private PlayerDataManager _playerDataManager;
        private PlayerInGameManager _playerInGameManager;
        private MapType _mapName;
        private GameConfigData _gameConfigData;

        [Inject]
        private void Init(GameEventManager gameEventManager, UIManager uIManager, IObjectResolver objectResolver,
            PlayerDataManager playerDataManager, IConfigProvider configProvider)
        {
            PropertyTypeReaderWriter.RegisterReaderWriter();
            _gameEventManager = gameEventManager;
            _spawnPoints = FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None).ToList();
            _networkManagerHUD = GetComponent<NetworkManagerHUD>();
            _networkManagerHUD.enabled = false;
            _playerInGameManager = PlayerInGameManager.Instance;
            _gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnSceneResourcesLoaded);
            _objectResolver = objectResolver;
            _playerDataManager = playerDataManager;
            _gameConfigData = configProvider.GetConfig<JsonDataConfig>().GameConfig;
        }
        
        // 服务器端有玩家连接时调用
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            Debug.Log($"玩家 【{conn.connectionId}】 已连接到服务器。");
            
        }
        
        // 服务器端有玩家断开时调用
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _playerInGameManager.RemovePlayer(conn.connectionId);
            Debug.Log($"玩家 【{conn.connectionId}】 已断开连接。");
        }
        
        // 客户端成功连接到服务器时调用
        public override void OnClientConnect()
        {
            base.OnClientConnect();
            Debug.Log($"{NetworkClient.connection.connectionId}成功连接到服务器！");
            UIPropertyBinder.LocalPlayerId = NetworkClient.connection.connectionId;
        }
        
        // 客户端断开连接时调用
        public override void OnClientDisconnect()
        {
            Debug.Log($"{NetworkClient.connection.connectionId}与服务器断开连接。");
            base.OnClientDisconnect();
            UIPropertyBinder.LocalPlayerId = -1;
        }

        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            var res = DataJsonManager.Instance.GetResourceData(_gameConfigData.playerPrefabName);
            var resInfo = ResourceManager.Instance.GetResource<GameObject>(res);
            if (resInfo)
            {
                //currentPlayer = resInfo.gameObject;
                var spawnPoint = _spawnPoints[Random.Range(0, _spawnPoints.Count)];
                var playerGo = Instantiate(resInfo.gameObject, spawnPoint.transform);
                playerGo.transform.parent = spawnPoint.transform;
                playerGo.transform.localPosition = Vector3.zero;
                playerGo.transform.localRotation = Quaternion.identity;
                playerGo.name = playerGo.name.Replace("(Clone)", conn.connectionId.ToString());
                playerGo.gameObject.SetActive(false);
                ObjectInjectProvider.Instance.InjectMapGameObject(_mapName, playerGo);
                playerGo.gameObject.SetActive(true);
                Debug.Log("Spawned player: " + playerGo.name);
                _spawnPoints.Remove(spawnPoint);
                NetworkServer.AddPlayerForConnection(conn, playerGo);
            }
        }

        private void OnSceneResourcesLoaded(GameSceneResourcesLoadedEvent sceneResourcesLoadedEvent)
        {
            if (Enum.TryParse<MapType>(sceneResourcesLoadedEvent.SceneName, out var mapType))
            {
                _networkManagerHUD.enabled = true;
                _mapName = mapType;
                Debug.Log("map resources loaded");
                return;
            }
            Debug.Log("map resources loaded fail");
            _networkManagerHUD.enabled = false;
        }
 
        public override void OnStartServer()
        {
            base.OnStartServer();
            // 监听服务器上的连接和断开事件
            NetworkServer.OnConnectedEvent += HandleServerConnected;
            NetworkServer.OnDisconnectedEvent += HandleServerDisconnected;

            NetworkServer.RegisterHandler<MirrorPlayerConnectMessage>(OnServerPlayerAccountIdMessage);
        }

        private void OnServerPlayerAccountIdMessage(NetworkConnectionToClient conn, MirrorPlayerConnectMessage message)
        { 
            if (_playerAccountIdMap.TryGetValue(conn.connectionId, out var uid))
            {
                Debug.LogError($"Player already connected: {uid}");
                return;
            }
            Debug.Log($"Received PlayerAccountId from client: {message.UID}");
            // 获取已添加的玩家对象
            if (conn.identity)
            {
                _playerAccountIdMap[conn.connectionId] = message.UID;
                _playerDataManager.UpdatePlayerConnectionId(message.UID, conn.connectionId);
                var playerInGameData = _playerDataManager.GetPlayer(conn.connectionId);
                _playerInGameManager.AddPlayer(conn.connectionId, new PlayerInGameData
                {
                    player = playerInGameData.player,
                    networkIdentity = conn.identity,
                });
                
                var playerCount = _playerDataManager.GetPlayers().Count;
                var gameInfo = new GameInfo
                {
                    SceneName = _mapName,
                    GameMode = (GameMode)_playerDataManager.CurrentRoomData.RoomCustomInfo.GameMode,
                    GameTime = _playerDataManager.CurrentRoomData.RoomCustomInfo.GameTime,
                    GameScore = _playerDataManager.CurrentRoomData.RoomCustomInfo.GameScore,
                    PlayerCount = playerCount
                };
                _gameEventManager.Publish(new PlayerConnectEvent(conn.connectionId, conn.identity, playerInGameData.player));
                _gameEventManager.Publish(new GameReadyEvent(gameInfo));
                // }
                // if (_playerAccountIdMap.Count == playerCount)
                // {
                //     var gameInfo = new GameInfo
                //     {
                //         SceneName = _mapName,
                //         GameMode = (GameMode)_playerDataManager.CurrentRoomData.RoomCustomInfo.GameMode,
                //         GameTime = _playerDataManager.CurrentRoomData.RoomCustomInfo.GameTime,
                //         GameScore = _playerDataManager.CurrentRoomData.RoomCustomInfo.GameScore,
                //         PlayerCount = playerCount
                //     };
                //     _gameEventManager.Publish(new GameReadyEvent(gameInfo));
                //     Debug.Log("player all ready");
                // }
                Debug.Log("Received PlayerAccountId from client: " + message.UID);
            }
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
            NetworkClient.RegisterHandler<MirrorPlayerConnectMessage>(OnPlayerConnectedMessage);
            // NetworkClient.RegisterHandler<PlayerDisconnectMessage>(OnPlayerDisconnectedMessage);
            NetworkClient.OnConnectedEvent += OnClientConnectToServer;
        }

        private void OnClientConnectToServer()
        {
            // 获取当前连接
            NetworkConnection conn = NetworkClient.connection;

            var msg = new MirrorPlayerConnectMessage("Creator1", conn.connectionId, "asdw");
            conn.Send(msg);
            // 发送 PlayerAccountId 给服务器
            // TODO: 取消注释
            //var msg = new PlayerConnectMessage(PlayFabData.PlayFabId.Value, conn.connectionId, PlayFabData.PlayerReadOnlyData.Value.Nickname);
            //conn.Send(msg);
        }

        private void OnPlayerConnectedMessage(MirrorPlayerConnectMessage message)
        {
            Debug.Log($"Received PlayerAccountId: {message.UID} - {message.Name} - {message.ConnectionID.ToString()}");
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