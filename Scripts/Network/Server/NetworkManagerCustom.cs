using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Data.NetworkMes;
using Game.Map;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.Server.PlayFab;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.UI.UIBase;
using kcp2k;
using Mirror;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;
using GameInfo = HotUpdate.Scripts.Tool.GameEvent.GameInfo;
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
        private KcpTransport _transport;
        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private PlayFabRoomManager _playFabRoomManager;
        
        private readonly Dictionary<int, NetworkConnectionToClient> _connectionToClients = new Dictionary<int, NetworkConnectionToClient>();

        [Inject]
        private void Init(GameEventManager gameEventManager, UIManager uIManager, IObjectResolver objectResolver,
            PlayerDataManager playerDataManager, IConfigProvider configProvider, PlayFabRoomManager playFabRoomManager)
        {
            _transport = GetComponent<KcpTransport>();
            _playFabRoomManager = playFabRoomManager;
            PropertyTypeReaderWriter.RegisterReaderWriter();
            _gameEventManager = gameEventManager;
            _spawnPoints = FindObjectsByType<NetworkStartPosition>(FindObjectsSortMode.None).ToList();
            _networkManagerHUD = GetComponent<NetworkManagerHUD>();
            _networkManagerHUD.enabled = false;
            _playerInGameManager = PlayerInGameManager.Instance;
            _gameEventManager.Subscribe<GameSceneResourcesLoadedEvent>(OnSceneResourcesLoaded);
            _objectResolver = objectResolver;
            _playerDataManager = playerDataManager;
            _uiManager = uIManager;
            _mirrorNetworkMessageHandler = FindObjectOfType<MirrorNetworkMessageHandler>();
            _gameConfigData = configProvider.GetConfig<JsonDataConfig>().GameConfig;
            Debug.Log($"[NetworkManagerCustom]: init success");
        }

        // 服务器端有玩家连接时调用
        public override void OnServerConnect(NetworkConnectionToClient conn)
        {
            base.OnServerConnect(conn);
            _connectionToClients.Add(conn.connectionId, conn);
            Debug.Log($"玩家 【{conn.connectionId}】 已连接到服务器。");

        }
        
        // 服务器端有玩家断开时调用
        public override void OnServerDisconnect(NetworkConnectionToClient conn)
        {
            base.OnServerDisconnect(conn);
            _connectionToClients.Remove(conn.connectionId);
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

        private GameObject SpawnPlayer(int connectionId, NetworkStartPosition spawnPoint)
        {
            var res = DataJsonManager.Instance.GetResourceData(_gameConfigData.playerPrefabName);
            var resInfo = ResourceManager.Instance.GetResource<GameObject>(res);
            var playerGo = Instantiate(resInfo.gameObject, spawnPoint.transform.position, Quaternion.identity, spawnPoint.transform);
            playerGo.name = playerGo.name.Replace("(Clone)", connectionId.ToString());
            Debug.Log("Spawned player: " + playerGo.name);
            return playerGo;
        }

        private void SpawnPlayer(NetworkConnectionToClient conn)
        {
            var spawnIndex = Random.Range(0, _spawnPoints.Count);
            Debug.Log($"[NetworkManagerCustom]: spawnIndex");
            var res = DataJsonManager.Instance.GetResourceData(_gameConfigData.playerPrefabName);
            Debug.Log($"[NetworkManagerCustom]: res");
            var spawnPoint = _spawnPoints[spawnIndex];
            Debug.Log($"[NetworkManagerCustom]: spawnPoint");
            var playerGo = SpawnPlayer(conn.connectionId, spawnPoint);
            Debug.Log($"[NetworkManagerCustom]: playerGo");
            //currentPlayer = resInfo.gameObject;
            // playerGo.gameObject.SetActive(false);
            // ObjectInjectProvider.Instance.InjectMapGameObject(_mapName, playerGo);
            // playerGo.gameObject.SetActive(true);
            var identity = playerGo.GetComponent<NetworkIdentity>();
            Debug.Log($"[NetworkManagerCustom]: identity - {identity} {res.Name} {conn.connectionId} {playerGo.name} {playerGo.name} {playerGo.transform.position} {identity.netId}");
            _mirrorNetworkMessageHandler.SendToAllClients(new MirrorPlayerConnectMessage(res.Name, 
                conn.connectionId, playerGo.name, playerGo.transform.position, identity.netId));

            Debug.Log($"[NetworkManagerCustom]: _mirrorNetworkMessageHandler SendToAllClients");
            _spawnPoints.Remove(spawnPoint);            
            Debug.Log($"[NetworkManagerCustom]: _spawnPoints.Remove(spawnPoint)"); 
            // 详细检查
            // Debug.Log($"Connection: {conn}");
            // Debug.Log($"Player GameObject: {playerGo}");
            // Debug.Log($"NetworkIdentity: {playerGo.GetComponent<NetworkIdentity>()}");
            //
            // // 检查所有 NetworkBehaviour 组件
            // NetworkBehaviour[] behaviours = playerGo.GetComponents<NetworkBehaviour>();
            // foreach (var behaviour in behaviours)
            // {
            //     Debug.Log($"NetworkBehaviour: {behaviour} - Type: {behaviour.GetType()}");
            //     if (behaviour == null)
            //     {
            //         Debug.LogError("Found null NetworkBehaviour!");
            //     }
            // }
    
            try
            {
                NetworkServer.AddPlayerForConnection(conn, playerGo);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"AddPlayerForConnection failed: {e}");
                Destroy(playerGo);
            }
            
        }


        public override void OnServerAddPlayer(NetworkConnectionToClient conn)
        {
            Debug.Log("OnServerAddPlayer");
            
            var hasHost = PlayFabData.PlayerList.Any(player => (PlayerGameDuty)Enum.Parse(typeof(PlayerGameDuty), player.playerDuty) == PlayerGameDuty.Host);
            if (_connectionToClients.Count == PlayFabData.PlayerList.Count - (hasHost ? 0 : 1))
            {
                _uiManager.CloseAllPanel();
                Debug.Log("关闭主界面");
                foreach (var connection in _connectionToClients.Values)
                {
                    SpawnPlayer(connection);
                }
                _uiManager.CloseUI(UIType.Loading);
            }
        }

        private void OnSceneResourcesLoaded(GameSceneResourcesLoadedEvent sceneResourcesLoadedEvent)
        {
            if (Enum.TryParse<MapType>(sceneResourcesLoadedEvent.SceneName, out var mapType))
            {
                //_networkManagerHUD.enabled = true;
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
            PlayFabData.ConnectionAddress
                .Subscribe(address =>
            {
                if (!string.IsNullOrEmpty(address))
                {
                    networkAddress = address.Trim();
                }
            }).AddTo(this);
            PlayFabData.ConnectionPort.Subscribe(port =>
            {
                if (port > 0)
                {
                    _transport.port = (ushort)port;
                }
            }).AddTo(this);

            NetworkServer.RegisterHandler<MirrorPlayerConnectMessage>(OnServerPlayerAccountIdMessage);
        }
        //public override void On

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
                    playerNetId = conn.identity.netId,
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
                _gameEventManager.Publish(new PlayerConnectEvent(conn.connectionId, conn.identity.netId));//, message.UID, message.Name, gameInfo));
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
            //
            var msg = new MirrorPlayerConnectMessage("Creator1", conn.connectionId, "asdw", new AotCompressedVector3(), 0);
            conn.Send(msg);
            // 发送 PlayerAccountId 给服务器
            // TODO: 取消注释
            //var msg = new PlayerConnectMessage(PlayFabData.PlayFabId.Value, conn.connectionId, PlayFabData.PlayerReadOnlyData.Value.Nickname);
            //conn.Send(msg);
        }

        private void OnPlayerConnectedMessage(MirrorPlayerConnectMessage message)
        {
            // Debug.Log($"Received PlayerAccountId: {message.UID} - {message.Name} - {message.ConnectionID.ToString()} - {message.position.ToString()}");
            //
            // var spawnPoint = message.position.ToVector3();
            // var playerGo = NetworkClient.spawned.FirstOrDefault(x => x.Value.netId == message.playerUid);
            // if (!playerGo.Value)
            // {
            //     Debug.LogError("Player not found: " + message.Name);
            //     return;
            // }
            // playerGo.Value.transform.position = spawnPoint;
            // playerGo.Value.transform.rotation = Quaternion.identity;
            // Debug.Log("Spawned player: " + playerGo.Value.name);
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