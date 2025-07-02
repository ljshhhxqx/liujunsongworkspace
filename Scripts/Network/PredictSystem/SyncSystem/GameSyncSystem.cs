using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using Mirror;
using Tool.GameEvent;
using Tool.Message;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using InteractHeader = HotUpdate.Scripts.Network.PredictSystem.Interact.InteractHeader;

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
        private float _maxCommandAge; 
        public static float TickSeconds => 1f / _tickRate;
        private float _tickTimer;
        private float _syncTimer;
        private JsonDataConfig _jsonDataConfig;
        private PlayerPropertySyncSystem _playerPropertySyncSystem;
        private bool _isProcessing; // 防止重入
        private CancellationTokenSource _cts;
        
        private InteractSystem _interactSystem;

        [SyncVar(hook = nameof(OnIsRandomUnionStartChanged))] 
        public bool isRandomUnionStart;
        [SyncVar(hook = nameof(OnGameStartChanged))] 
        public bool isGameStart;
        
        public static int CurrentTick { get; private set; }

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            CurrentTick = 0;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _cts = new CancellationTokenSource();
            _tickRate = _jsonDataConfig.GameConfig.tickRate;
            _serverInputRate = _jsonDataConfig.GameConfig.serverInputRate;
            _maxCommandAge = _jsonDataConfig.GameConfig.maxCommandAge;
            gameEventManager.Subscribe<PlayerConnectEvent>(OnPlayerConnect);
            gameEventManager.Subscribe<PlayerDisconnectEvent>(OnPlayerDisconnect);
            gameEventManager.Subscribe<AddBuffToAllPlayerEvent>(OnAddBuffToAllPlayer);
            gameEventManager.Subscribe<AddDeBuffToLowScorePlayerEvent>(OnAddDeBuffToLowScorePlayer);
            gameEventManager.Subscribe<AllPlayerGetSpeedEvent>(OnAllPlayerGetSpeed);
            var commandTypes = Enum.GetValues(typeof(CommandType));
            foreach (CommandType commandType in commandTypes)
            {
                var syncSystem = commandType.GetSyncSystem();
                if (syncSystem == null)
                {
                    Debug.Log($"No sync system found for {commandType}");
                    continue;
                }
                syncSystem.Initialize(this);
                ObjectInjectProvider.Instance.Inject(syncSystem);
                if (syncSystem is PlayerPropertySyncSystem playerPropertySyncSystem)
                {
                    _playerPropertySyncSystem = playerPropertySyncSystem;
                }
                _syncSystems.Add(commandType, syncSystem);
            }
            OnAllSystemInit?.Invoke();
            ProcessImmediateCommands(_cts.Token);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (!isServer)
            {
                _syncSystems.Clear();
                _clientCommands.Clear();
                _currentTickCommands.Clear();
            }
            Time.fixedDeltaTime = _serverInputRate;
            Observable.EveryFixedUpdate()
                .Where(_ => isServer && !_isProcessing)
                .Subscribe(_ =>
                {
                    _tickTimer = 0;
                    ProcessTick();
                    CurrentTick++;
                })
                .AddTo(this);
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

        private void OnIsRandomUnionStartChanged(bool oldValue, bool newValue)
        {
            if (newValue)
            {
                PlayerInGameManager.Instance.RandomUnion(out var noUnionPlayerId);
                if (noUnionPlayerId != 0)
                {
                    var command = new NoUnionPlayerAddMoreScoreAndGoldCommand
                    {
                        Header = CreateNetworkCommandHeader(noUnionPlayerId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate),
                    };
                    EnqueueServerCommand(command);
                }
            }
        }
        
        private void OnGameStartChanged(bool oldValue, bool newValue)
        {
            OnGameStart?.Invoke(newValue);
            if (isServer)
            {
                PlayerInGameManager.Instance.isGameStarted = newValue;
            }
        }

        private void OnPlayerDisconnect(PlayerDisconnectEvent disconnectEvent)
        {
            PlayerInGameManager.Instance.RemovePlayer(disconnectEvent.ConnectionId);
            OnPlayerDisconnected?.Invoke(disconnectEvent.ConnectionId);
            RpcPlayerDisconnect(disconnectEvent.ConnectionId);
        }

        private void OnPlayerConnect(PlayerConnectEvent connectEvent)
        {
            var networkIdentity = NetworkServer.connections[connectEvent.ConnectionId].identity;
            connectEvent = new PlayerConnectEvent(connectEvent.ConnectionId, networkIdentity, connectEvent.ReadOnlyData);
            PlayerInGameManager.Instance.AddPlayer(connectEvent.ConnectionId, new PlayerInGameData
            {
                player = connectEvent.ReadOnlyData,
                networkIdentity = networkIdentity
            });
            OnPlayerConnected?.Invoke(connectEvent.ConnectionId, connectEvent.Identity);
            RpcPlayerConnect(connectEvent);
        }
        
        [ClientRpc]
        private void RpcPlayerConnect(PlayerConnectEvent connectEvent)
        {
            OnPlayerConnected?.Invoke(connectEvent.ConnectionId, NetworkServer.connections[connectEvent.ConnectionId].identity);
        }
        
        [ClientRpc]
        private void RpcPlayerDisconnect(int connectionId)
        {
            PlayerInGameManager.Instance.RemovePlayer(connectionId);
            OnPlayerDisconnected?.Invoke(connectionId);
        }
        
        public event Action<int, NetworkIdentity> OnPlayerConnected;
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
        
        public PlayerComponentController GetPlayerConnection(int connectionId)
        {
            PlayerComponentController playerConnection = null;
            if (NetworkClient.connection.connectionId == connectionId)
            {
                playerConnection = NetworkClient.connection.identity.GetComponent<PlayerComponentController>();
            }
            else if (NetworkServer.connections.TryGetValue(connectionId, out var connection))
            {
                playerConnection = connection.identity.GetComponent<PlayerComponentController>();
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
                    var header = command.GetHeader();
                    var syncSystem = GetSyncSystem(header.CommandType);
                    if (syncSystem != null)
                    {
                        var allStates = syncSystem.GetAllState();
                        RpcProcessCurrentTickCommand(allStates);
                    }
                }
            }
        }

        /// <summary>
        /// 客户端发送命令(不能给服务器使用)
        /// </summary>
        /// <param name="commandJson"></param>
        [Server]
        public void EnqueueCommand(byte[] commandJson)
        {
            var command = NetworkCommandExtensions.Deserialize(commandJson);
            var header = command.GetHeader();
            var validCommand = command.ValidateCommand();
            if (!validCommand.IsValid)
            {
                Debug.LogError($"Invalid command: {header.CommandType} - {header.CommandId} - {validCommand}");
                return;
            }
            _clientCommands.Enqueue(command);
        }

        [Server]
        private void ProcessTick()
        {
            _isProcessing = true;

            try
            {
                _syncTimer += Time.fixedDeltaTime;
                // 将客户端待处理命令移到当前tick的命令队列
                MoveCommandsToCurrentTick();
                // 处理当前tick的所有命令
                ProcessCurrentTickCommands();
                if (_syncTimer >= 1f / _tickRate)
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

                var header = command.GetHeader();

                // 检查命令是否过期
                var commandAge = (CurrentTick - header.Tick) * Time.fixedDeltaTime;
                if (commandAge > _maxCommandAge)
                {
                    Debug.LogWarning($"Command discarded due to age: {commandAge}s");
                    continue;
                }

                // 如果命令属于未来的tick，停止处理
                if (header.Tick > CurrentTick)
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

                // 检查命令是否过期
                var commandAge = (CurrentTick - header.Tick) * Time.fixedDeltaTime;
                if (commandAge > _maxCommandAge)
                {
                    Debug.LogWarning($"Command discarded due to age: {commandAge}s");
                    continue;
                }

                // 如果命令属于未来的tick，停止处理
                if (header.Tick > CurrentTick)
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
                var header = command.GetHeader();
                OnServerProcessCurrentTickCommand?.Invoke(command);
                var syncSystem = GetSyncSystem(header.CommandType);
                if (syncSystem != null)
                {
                    var allStates = syncSystem.GetAllState();
                    RpcProcessCurrentTickCommand(allStates);
                }
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
            if (!validCommand.IsValid)
            {
                Debug.LogError($"Invalid command: {header.CommandType}");
                return;
            }
            _serverCommands.Enqueue(command);
        }
        
        private BaseSyncSystem GetSyncSystem(CommandType commandType)
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
        public event Action<byte[]> OnClientProcessStateUpdate;
        [ClientRpc]
        private void RpcProcessCurrentTickCommand(byte[] state)
        {
            OnClientProcessStateUpdate?.Invoke(state);
        }

        /// <summary>
        /// 广播状态更新
        /// </summary>
        public event Action OnBroadcastStateUpdate;
        private void BroadcastStateUpdates()
        {
            RpcUpdateState();
        }

        [ClientRpc]
        private void RpcUpdateState()
        {
            OnBroadcastStateUpdate?.Invoke(); 
        }

        public static InteractHeader CreateInteractHeader(int? connectionId, InteractCategory category, CompressedVector3 position = default, CommandAuthority authority = CommandAuthority.Server)
        {
            int? noSequence = null;
            var connectionIdValue = connectionId.GetValueOrDefault();
            var header = ObjectPool<InteractHeader>.Get();
            header.CommandId = HybridIdGenerator.GenerateCommandId(authority == CommandAuthority.Server, CommandType.Interact, ref noSequence);
            header.RequestConnectionId = connectionIdValue;
            header.Tick = CurrentTick;
            header.Category = category;
            header.Position = position;
            header.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            header.Authority = authority;
            return header;
        }

        public static NetworkCommandHeader CreateNetworkCommandHeader(int? connectionId, CommandType commandType, CommandAuthority authority = CommandAuthority.Server, CommandExecuteType commandExecuteType = CommandExecuteType.Predicate)
        {
            var tick = (int?)CurrentTick;
            var connectionIdValue = connectionId.GetValueOrDefault();
            var header = ObjectPool<NetworkCommandHeader>.Get();
            header.CommandId = HybridIdGenerator.GenerateCommandId(authority == CommandAuthority.Server, commandType, ref tick);
            header.ConnectionId = connectionIdValue;
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
        }
    }
}