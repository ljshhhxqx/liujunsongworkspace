using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.ObjectPool;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using INetworkCommand = AOTScripts.Data.INetworkCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class GameSyncManager : NetworkBehaviour
    {
        private readonly ConcurrentQueue<INetworkCommand> _clientCommands = new ConcurrentQueue<INetworkCommand>();
        private readonly ConcurrentQueue<INetworkCommand> _serverCommands = new ConcurrentQueue<INetworkCommand>();
        private readonly ConcurrentQueue<INetworkCommand> _immediateCommands = new ConcurrentQueue<INetworkCommand>();
        private readonly Dictionary<int, Dictionary<CommandType, int>> _lastProcessedInputs = new Dictionary<int, Dictionary<CommandType, int>>();  // 记录每个玩家最后处理的输入序号
        private readonly ConcurrentQueue<INetworkCommand> _currentTickCommands = new ConcurrentQueue<INetworkCommand>();
        private readonly Dictionary<CommandType, BaseSyncSystem> _syncSystems = new Dictionary<CommandType, BaseSyncSystem>();
        private static float _tickRate; 
        private static float _serverInputRate;
        private static float _serverUpdateStateInterval;
        private float _maxCommandAge; 
        public static float TickSeconds => 1f / _tickRate;
        public static float ServerUpdateInterval => _serverUpdateStateInterval;
        private float _tickTimer;
        private float _syncTimer;
        private JsonDataConfig _jsonDataConfig;
        private PlayerPropertySyncSystem _playerPropertySyncSystem;
        private bool _isProcessing; // 防止重入
        private bool _serverHandler;
        private GameEventManager _gameEventManager;
        private CancellationTokenSource _cts;
        private PlayerInGameManager _playerInGameManager;
        private readonly Dictionary<int, PlayerComponentController> _playerComponentControllers = new Dictionary<int, PlayerComponentController>();
        private readonly Dictionary<uint, PlayerComponentController> _playerNetComponentControllers = new Dictionary<uint, PlayerComponentController>();
        private InteractSystem _interactSystem;

        //[SyncVar(hook = nameof(OnIsRandomUnionStartChanged))] 
        public bool isRandomUnionStart;
        [SyncVar] 
        public bool isGameStart;
        [SyncVar] 
        public bool isGameOver;
        
        public static int CurrentTick { get; private set; }
        
        [SyncVar(hook = nameof(OnCurrentTickChanged))]
        private int _currentTick;

        private PlayerComponentController _localPlayerNetComponentController;

        private void OnCurrentTickChanged(int oldValue, int newValue)
        {
            //Debug.Log($"CurrentTick changed from {oldValue} to {newValue}");
            CurrentTick = newValue;
        }

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, PlayerInGameManager playerInGameManager)
        {
            _currentTick = 0;
            _playerInGameManager = playerInGameManager;
            Debug.Log("GameSyncManager Init");
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _cts = new CancellationTokenSource();
            _tickRate = _jsonDataConfig.GameConfig.tickRate;
            _serverInputRate = _jsonDataConfig.GameConfig.serverInputRate;
            _maxCommandAge = _jsonDataConfig.GameConfig.maxCommandAge;
            _serverUpdateStateInterval = _jsonDataConfig.GameConfig.stateUpdateInterval;
            _gameEventManager = gameEventManager;
            var commandTypes = Enum.GetValues(typeof(CommandType));
            foreach (CommandType commandType in commandTypes)
            {
                var syncSystem = commandType.GetSyncSystem();
                if (syncSystem == null)// || syncSystem.GetType() == typeof(PlayerSkillSyncSystem) || syncSystem.GetType() == typeof(PlayerItemSyncSystem) || syncSystem.GetType() == typeof(ShopSyncSystem) || syncSystem.GetType() == typeof(PlayerEquipmentSystem))
                {
                    Debug.Log($"No sync system found for {commandType}");
                    continue;
                }
                ObjectInjectProvider.Instance.InjectMap((MapType)GameLoopDataModel.GameSceneName.Value, syncSystem);
                syncSystem.Initialize(this);
                if (syncSystem is PlayerPropertySyncSystem playerPropertySyncSystem)
                {
                    _playerPropertySyncSystem = playerPropertySyncSystem;
                }
                _syncSystems.Add(commandType, syncSystem);
            }
            OnAllSystemInit?.Invoke();
            ProcessImmediateCommands(_cts.Token);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            // _gameEventManager.Subscribe<PlayerConnectEvent>(OnPlayerConnect);
            // _gameEventManager.Subscribe<PlayerDisconnectEvent>(OnPlayerDisconnect);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log("GameSyncManager Start Server");
            _serverHandler = true;
            if (!isServer)
            {
                _syncSystems.Clear();
                _clientCommands.Clear();
                _currentTickCommands.Clear();
            }
            _gameEventManager.Subscribe<GameStartEvent>(OnGameStartEvent);
            _gameEventManager.Subscribe<PlayerConnectEvent>(OnPlayerConnect);
            _gameEventManager.Subscribe<PlayerDisconnectEvent>(OnPlayerDisconnect);
            _gameEventManager.Subscribe<AddBuffToAllPlayerEvent>(OnAddBuffToAllPlayer);
            _gameEventManager.Subscribe<AddDeBuffToLowScorePlayerEvent>(OnAddDeBuffToLowScorePlayer);
            _gameEventManager.Subscribe<AllPlayerGetSpeedEvent>(OnAllPlayerGetSpeed);
            ProcessTickSync(_cts.Token).Forget();
        }
        
        private async UniTask ProcessTickSync(CancellationToken cancellationToken)
        {
            while (cancellationToken.IsCancellationRequested == false)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(TickSeconds), ignoreTimeScale:true, cancellationToken: cancellationToken);
                if (isServer && !_isProcessing && NetworkServer.connections.Count > 0 && !isGameOver)
                {
                    _tickTimer = 0;
                    ProcessTick();
                    _currentTick++;
                }
            }
        }

        private void OnGameStartEvent(GameStartEvent gameStartEvent)
        {
            Debug.Log("GameSyncManager OnGameStartEvent");
            isGameStart = true;
            OnGameStart?.Invoke(true);
            _playerInGameManager.isGameStarted = true;
        }

        private void OnAllPlayerGetSpeed(AllPlayerGetSpeedEvent allPlayerGetSpeedEvent)
        {
            _playerPropertySyncSystem.AllPlayerGetSpeed();
        }

        private void OnAddBuffToAllPlayer(AddBuffToAllPlayerEvent addBuffToAllPlayerEvent)
        {
            _playerPropertySyncSystem.AddBuffToAllPlayer(addBuffToAllPlayerEvent.CurrentRound);
        }

        private void OnAddDeBuffToLowScorePlayer(AddDeBuffToLowScorePlayerEvent addDeBuffToLowScorePlayerEvent)
        {
            _playerPropertySyncSystem.AddBuffToLowScorePlayer(addDeBuffToLowScorePlayerEvent.CurrentRound);
        }

        private void OnPlayerDisconnect(PlayerDisconnectEvent disconnectEvent)
        {
            if(!_serverHandler)
                return;
            _playerInGameManager.RemovePlayer(disconnectEvent.ConnectionId);
            OnPlayerDisconnected?.Invoke(disconnectEvent.ConnectionId);
            RpcPlayerDisconnect(disconnectEvent.ConnectionId);
        }

        private void OnPlayerConnect(PlayerConnectEvent connectEvent)
        {
            if(!_serverHandler)
                return;
            var networkIdentity = NetworkServer.connections[connectEvent.ConnectionId].identity;
            connectEvent = new PlayerConnectEvent(connectEvent.ConnectionId, connectEvent.PlayerNetId);
            OnPlayerConnected?.Invoke(connectEvent.ConnectionId, connectEvent.PlayerNetId, networkIdentity);
            RpcPlayerConnect(connectEvent.ConnectionId, connectEvent.PlayerNetId);
        }
        
        [ClientRpc]
        private void RpcPlayerConnect(int connectionId, uint playerNetId)
        {
            if(_serverHandler)
                return;
            var player = GetPlayerConnection(playerNetId);
            OnPlayerConnected?.Invoke(connectionId, playerNetId, player.netIdentity);
        }
        
        [ClientRpc]
        private void RpcPlayerDisconnect(int connectionId)
        {
            if(_serverHandler)
                return;
            _playerInGameManager.RemovePlayer(connectionId);
            OnPlayerDisconnected?.Invoke(connectionId);
        }
        
        public event Action<int, uint, NetworkIdentity> OnPlayerConnected;
        public event Action<int> OnPlayerDisconnected;
        
        public T GetSyncSystem<T>(CommandType commandType) where T : BaseSyncSystem
        {
            if (_syncSystems.TryGetValue(commandType, out var system))
            {
                return (T)system;
            }
            
            Debug.LogError($"No sync system found for {commandType}");
            return null;
        }
        
        public PlayerComponentController GetLocalPlayerConnection()
        {
            if (!_localPlayerNetComponentController)
            {
                _localPlayerNetComponentController = GetPlayerConnection(_playerInGameManager.LocalPlayerNetId);
            }
            return _localPlayerNetComponentController;
        }

        public PlayerComponentController GetPlayerConnection(uint playerNetId)
        {
            if (!_playerNetComponentControllers.TryGetValue(playerNetId, out var playerConnection))
            {
                if (!_serverHandler)
                {

                    foreach (var identity in NetworkClient.spawned.Values)
                    {
                        if (playerNetId == identity.netId)
                        {
                            playerConnection = identity.GetComponent<PlayerComponentController>();
                            _playerNetComponentControllers.Add(playerNetId, playerConnection);
                            break;
                        }
                    }
                }
                else
                {
                    foreach (var connection in NetworkServer.connections.Values)
                    {
                        if (playerNetId == connection.identity.netId)
                        {
                            playerConnection = connection.identity.GetComponent<PlayerComponentController>();
                            _playerNetComponentControllers.Add(playerNetId, playerConnection);
                            break;
                        }
                    }
                }
            }
            return playerConnection;
        }

        public PlayerComponentController GetPlayerConnection(int connectionId)
        {
            if (!_playerComponentControllers.TryGetValue(connectionId, out var playerConnection))
            {
                if (!_serverHandler)
                {
                    if (_playerInGameManager.LocalPlayerId == connectionId)
                    {
                        playerConnection = NetworkClient.connection.identity.GetComponent<PlayerComponentController>();
                        _playerComponentControllers.Add(connectionId, playerConnection);
                    }
                    else
                    {
                        var playerNetId = _playerInGameManager.GetPlayerNetId(connectionId);
                        foreach (var identity in NetworkClient.spawned.Values)
                        {
                            if (playerNetId == identity.netId)
                            {
                                playerConnection = identity.GetComponent<PlayerComponentController>();
                                _playerComponentControllers.Add(connectionId, playerConnection);
                                break;
                            }
                        }
                    }
                }

                else if (_serverHandler && NetworkServer.connections != null && NetworkServer.connections.TryGetValue(connectionId, out var connection))
                {
                    playerConnection = connection.identity.GetComponent<PlayerComponentController>();
                    _playerComponentControllers.Add(connectionId, playerConnection);
                }
            }

            if (playerConnection)
            {
                return playerConnection; 
            }

            Debug.LogError($"No player connection found for {connectionId}");
            return null;
        }

        private async void ProcessImmediateCommands(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => !_immediateCommands.IsEmpty, 
                    cancellationToken: token);
                if (_immediateCommands.TryDequeue(out var command))
                {
                    //var header = command.GetHeader();
                    //var syncSystem = GetSyncSystem(header.CommandType);
                    OnServerProcessCurrentTickCommand?.Invoke(command);
                    // if (syncSystem != null)
                    // {
                    //     foreach (var playerConnection in syncSystem.PropertyStates.Keys)
                    //     {
                    //         var state = syncSystem.GetPlayerSerializedState(playerConnection);
                    //         RpcProcessCurrentTickCommand(playerConnection, state);
                    //         
                    //     }
                    // }
                }
            }
        }

        /// <summary>
        /// 客户端发送命令(不能给服务器使用)
        /// </summary>
        /// <param name="commandJson"></param>
        public void EnqueueCommand(byte[] commandJson)
        {
            var command = NetworkCommandExtensions.DeserializeCommand(commandJson);
            var header = command.GetHeader();
            var validCommand = command.ValidateCommand();
            // if (!validCommand.IsValid)
            // {
            //     foreach (var str in validCommand.Errors)
            //     {
            //         Debug.LogError($"Invalid command: CommandType-{command.GetType().Name} -> CommandId-{header.CommandId} -> Error-{str}");
            //     }
            //     ObjectPoolManager<CommandValidationResult>.Instance.Return(validCommand);
            //     return;
            // // }
            // if (command is InputCommand inputCommand && (inputCommand.CommandAnimationState is AnimationState.Attack or AnimationState.Jump or AnimationState.SkillE or AnimationState.SkillQ or AnimationState.SprintJump))
            // {
            //     Debug.Log($"[GameSyncManager] EnqueueCommand predicted command {header.CommandId} {inputCommand.CommandAnimationState} at tick {header.Tick}");
            // }
            //ObjectPoolManager<CommandValidationResult>.Instance.Return(validCommand);
            _clientCommands.Enqueue(command);
        }

        [Server]
        private void ProcessTick()
        {
            _isProcessing = true;

            //Debug.Log($"GameSyncManager ProcessTick in tick-{_currentTick}");
            try
            {
                _syncTimer += TickSeconds;
                // 将客户端待处理命令移到当前tick的命令队列
                MoveCommandsToCurrentTick();
                // 处理当前tick的所有命令
                ProcessCurrentTickCommands();
                if (_syncTimer >= _serverUpdateStateInterval)
                {
                    // 广播状态更新
                    BroadcastStateUpdates();
                    _syncTimer = 0;
                }
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void MoveCommandsToCurrentTick()
        {
            // 将待处理命令移到当前tick的命令队列
            while (_clientCommands.Count > 0)
            {
                if (!_clientCommands.TryDequeue(out var command))
                {
                    continue;
                }
                //Debug.Log($"MoveCommandsToCurrentTick Client: {command.GetHeader().CommandType}-{command.GetHeader().Tick}-{command.GetHeader().ConnectionId}");

                var header = command.GetHeader();

                // 检查命令是否过期
                // var commandAge = (CurrentTick - header.Tick) * Time.fixedDeltaTime;
                // if (commandAge > _maxCommandAge)
                // {
                //     Debug.LogWarning($"Command discarded due to age: {commandAge}s");
                //     continue;
                // }

                // 如果命令属于未来的tick，停止处理
                if (header.Tick > _currentTick)
                {
                    break;
                }

                _currentTickCommands.Enqueue(command);
            }
            while (_serverCommands.Count > 0)
            {
                if (!_serverCommands.TryDequeue(out var command))
                {
                    continue;
                }
                var header = command.GetHeader();
                //Debug.Log($"MoveCommandsToCurrentTick Server: {command.GetHeader().CommandType}-{command.GetHeader().Tick}-{command.GetHeader().ConnectionId}");

                // 检查命令是否过期
                // var commandAge = (CurrentTick - header.Tick) * Time.fixedDeltaTime;
                // if (commandAge > _maxCommandAge)
                // {
                //     Debug.LogWarning($"Command discarded due to age: {commandAge}s");
                //     continue;
                // }

                // 如果命令属于未来的tick，停止处理
                if (header.Tick > _currentTick)
                {
                    break;
                }

                _currentTickCommands.Enqueue(command);
            }
        }

        public event Action<INetworkCommand> OnServerProcessCurrentTickCommand;
        public event Action<bool> OnGameStart;
        public event Action OnAllSystemInit;
        private void ProcessCurrentTickCommands()
        {
            while (_currentTickCommands.Count > 0)
            {
                if (!_currentTickCommands.TryDequeue(out var command))
                {
                    continue;
                }
                //Debug.Log($"ProcessCurrentTickCommands: {command.GetHeader().CommandType}-{command.GetHeader().Tick}-{command.GetHeader().ConnectionId}");
                //var header = command.GetHeader();
                if (command is PropertyBuffCommand propertyBuffCommand)
                {
                    Debug.Log($"ProcessCurrentTickCommands: {propertyBuffCommand.GetHeader().CommandType}-{propertyBuffCommand.GetHeader().Tick}-{propertyBuffCommand.GetHeader().ConnectionId}");
                }
                OnServerProcessCurrentTickCommand?.Invoke(command);
                //var syncSystem = GetSyncSystem(header.CommandType);
                // if (syncSystem != null)
                // {
                //     foreach (var playerConnection in syncSystem.PropertyStates.Keys)
                //     {
                //         var state = syncSystem.GetPlayerSerializedState(playerConnection);
                //         RpcProcessCurrentTickCommand(playerConnection, state);
                //     }
                // }
            }
        }

        /// <summary>
        /// 处理服务器端命令(安全地使用，客户端无法使用)
        /// </summary>
        /// <param name="command"></param>
        [Server]
        public void EnqueueServerCommand<T>(T command) where T : INetworkCommand
        {
            var header = command.GetHeader();
            var validCommand = command.ValidateCommand();
            //ObjectPoolManager<CommandValidationResult>.Instance.Return(validCommand);
            // if (!validCommand.IsValid)
            // {
            //     var sb = new StringBuilder();
            //     sb.AppendLine($"Invalid command: {command.GetType().Name}");
            //     for (int i = 0; i < validCommand.Errors.Count; i++)
            //     {
            //         sb.AppendLine(validCommand.Errors[i]);
            //     }
            //     Debug.LogError(sb.ToString());
            //     return;
            // }
            _serverCommands.Enqueue(command);
        }
        
        public BaseSyncSystem GetSyncSystem(CommandType commandType)
        {
            if (_syncSystems.TryGetValue(commandType, out var system))
            {
                return system;
            }
            
            Debug.LogError($"No sync system found for {commandType}");
            return null;
        }

        /// <summary>
        /// 强制更新每个BaseSyncSystem的客户端状态
        /// </summary>
        public event Action<int, byte[], CommandType> OnClientProcessStateUpdate;
        [ClientRpc]
        private void RpcProcessCurrentTickCommand(int connectionId, byte[] state, CommandType commandType)
        {
            // if (isServer)
            //     return;
            OnClientProcessStateUpdate?.Invoke(connectionId, state, commandType);
        }

        /// <summary>
        /// 广播状态更新
        /// </summary>
        public event Action OnBroadcastStateUpdate;
        private void BroadcastStateUpdates()
        {
            foreach (var commandType in _syncSystems.Keys)
            {
                var system = _syncSystems.GetValueOrDefault(commandType);
                foreach (var playerConnection in system.PropertyStates.Keys)
                {
                    var state = system.GetPlayerSerializedState(playerConnection);
                    RpcProcessCurrentTickCommand(playerConnection, state, commandType);
                }
            }
            RpcUpdateState();
        }

        [ClientRpc]
        private void RpcUpdateState()
        {
            OnBroadcastStateUpdate?.Invoke(); 
        }

        public static NetworkCommandHeader CreateNetworkCommandHeader(int connectionId, CommandType commandType, CommandAuthority authority = CommandAuthority.Server, CommandExecuteType commandExecuteType = CommandExecuteType.Predicate, NetworkCommandType networkCommandType = NetworkCommandType.None)
        {
            var tick = (int?)CurrentTick;
            var header = new NetworkCommandHeader();
            header.Clear();
            header.CommandId = HybridIdGenerator.GenerateCommandId(authority == CommandAuthority.Server, commandType, networkCommandType, ref tick);
            header.ConnectionId = connectionId;
            header.CommandType = commandType;
            header.Tick = tick.GetValueOrDefault();
            header.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            header.Authority = authority;
            header.ExecuteType = commandExecuteType;
            return header;
        }

        private void OnDestroy()
        {
            foreach (var syncSystem in _syncSystems)
            {
                syncSystem.Value.Clear();
            }
            _syncSystems.Clear();
            _clientCommands.Clear();
            _currentTickCommands.Clear();
            _serverCommands.Clear();
            Debug.Log("GameSyncSystem Destroy");
            _playerNetComponentControllers.Clear();
            _playerComponentControllers.Clear();
            _gameEventManager.Unsubscribe<GameStartEvent>(OnGameStartEvent);
            _gameEventManager.Unsubscribe<PlayerConnectEvent>(OnPlayerConnect);
            _gameEventManager.Unsubscribe<PlayerDisconnectEvent>(OnPlayerDisconnect);
            _gameEventManager.Unsubscribe<AddBuffToAllPlayerEvent>(OnAddBuffToAllPlayer);
            _gameEventManager.Unsubscribe<AddDeBuffToLowScorePlayerEvent>(OnAddDeBuffToLowScorePlayer);
            _gameEventManager.Unsubscribe<AllPlayerGetSpeedEvent>(OnAllPlayerGetSpeed);
        }
    }
    
    public static class GameSyncSystemExtensions
    {
        
        public static BaseSyncSystem GetSyncSystem(this CommandType syncNetworkData)
        {
            switch (syncNetworkData)
            {
                case CommandType.Property:
                    return new PlayerPropertySyncSystem();
                case CommandType.Input:
                    return new PlayerInputSyncSystem();
                case CommandType.Item:
                    return new PlayerItemSyncSystem();
                case CommandType.Equipment:
                    return new PlayerEquipmentSystem();
                case CommandType.Shop:
                    return new ShopSyncSystem();
                case CommandType.Skill:
                    return new PlayerSkillSyncSystem();
                case CommandType.Interact:
                    //Debug.LogWarning("Not implemented yet");
                    return null;
                // case CommandType.UI:
                //     return new PlayerCombatSyncSystem();
            }   
            return null;
        }
    }
}