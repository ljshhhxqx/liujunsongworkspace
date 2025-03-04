using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.InteractSystem;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class GameSyncManager : NetworkBehaviour
    {
        private readonly ConcurrentQueue<INetworkCommand> _clientCommands = new ConcurrentQueue<INetworkCommand>();
        private readonly ConcurrentQueue<INetworkCommand> _serverCommands = new ConcurrentQueue<INetworkCommand>();
        private readonly Dictionary<int, PlayerComponentController> _playerConnections = new Dictionary<int, PlayerComponentController>();
        private readonly Dictionary<int, Dictionary<CommandType, int>> _lastProcessedInputs = new Dictionary<int, Dictionary<CommandType, int>>();  // 记录每个玩家最后处理的输入序号
        private readonly ConcurrentQueue<INetworkCommand> _currentTickCommands = new ConcurrentQueue<INetworkCommand>();
        private readonly Dictionary<CommandType, BaseSyncSystem> _syncSystems = new Dictionary<CommandType, BaseSyncSystem>();
        private float _tickRate; 
        private float _maxCommandAge; 
        public float TickRate => _tickRate;
        private float _tickTimer;
        private PlayerInGameManager _playerInGameManager;
        private JsonDataConfig _jsonDataConfig;
        private bool _isProcessing; // 防止重入
        
        
        private InteractSystem.InteractSystem _interactSystem;
        
        public static int CurrentTick { get; private set; }

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, PlayerInGameManager playerInGameManager)
        {
            CurrentTick = 0;
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _playerInGameManager = playerInGameManager;
            if (!isServer)
            {
                _syncSystems.Clear();
                _clientCommands.Clear();
                _currentTickCommands.Clear();
            }
            _tickRate = _jsonDataConfig.GameConfig.tickRate;
            _maxCommandAge = _jsonDataConfig.GameConfig.maxCommandAge;
            gameEventManager.Subscribe<PlayerConnectEvent>(OnPlayerConnect);
            gameEventManager.Subscribe<PlayerDisconnectEvent>(OnPlayerDisconnect);
            var commandTypes = Enum.GetValues(typeof(CommandType));
            foreach (CommandType commandType in commandTypes)
            {
                _syncSystems[commandType] = commandType.GetSyncSystem();
                _syncSystems[commandType].Initialize(this);
                ObjectInjectProvider.Instance.Inject(_syncSystems[commandType]);
            }
        }

        private void OnPlayerDisconnect(PlayerDisconnectEvent disconnectEvent)
        {
            _playerConnections.Remove(disconnectEvent.ConnectionId);
            _playerInGameManager.RemovePlayer(disconnectEvent.ConnectionId);
            OnPlayerDisconnected?.Invoke(disconnectEvent.ConnectionId);
            RpcPlayerDisconnect(disconnectEvent.ConnectionId);
        }

        private void OnPlayerConnect(PlayerConnectEvent connectEvent)
        {
            _playerConnections.Add(connectEvent.ConnectionId, connectEvent.Identity.gameObject.GetComponent<PlayerComponentController>());
            _playerInGameManager.AddPlayer(connectEvent.ConnectionId, new PlayerInGameData
            {
                player = connectEvent.ReadOnlyData,
                networkIdentity = connectEvent.Identity
            });
            OnPlayerConnected?.Invoke(connectEvent.ConnectionId, connectEvent.Identity);
            RpcPlayerConnect(connectEvent);
        }
        
        [ClientRpc]
        private void RpcPlayerConnect(PlayerConnectEvent connectEvent)
        {
            _playerConnections.Add(connectEvent.ConnectionId, connectEvent.Identity.gameObject.GetComponent<PlayerComponentController>());
            OnPlayerConnected?.Invoke(connectEvent.ConnectionId, connectEvent.Identity);
        }
        
        [ClientRpc]
        private void RpcPlayerDisconnect(int connectionId)
        {
            _playerConnections.Remove(connectionId);
            _playerInGameManager.RemovePlayer(connectionId);
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
            if (_playerConnections.TryGetValue(connectionId, out var playerConnection))
            {
                return playerConnection;
            }

            Debug.LogError($"No player connection found for {connectionId}");
            return null;
        }

        private void Update()
        {
            if (!isServer || _isProcessing) return;

            _tickTimer += Time.deltaTime;
        
            // 检查是否需要处理新的tick
            if (_tickTimer >= _tickRate)
            {
                _tickTimer = 0;
                ProcessTick();
                CurrentTick++;
            }
        }

        /// <summary>
        /// 客户端发送命令(不能给服务器使用)
        /// </summary>
        /// <param name="commandJson"></param>
        [Server]
        public void EnqueueCommand(byte[] commandJson)
        {
            var command = MemoryPackSerializer.Deserialize<INetworkCommand>(commandJson);
            var header = command.GetHeader();
            var validCommand = command.ValidateCommand();
            if (!validCommand.IsValid)
            {
                Debug.LogError($"Invalid command: {header.CommandType}");
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
                // 将客户端待处理命令移到当前tick的命令队列
                MoveCommandsToCurrentTick();
                // 处理当前tick的所有命令
                ProcessCurrentTickCommands();
                // 处理其他系统的命令
                ProcessOtherSystemCommands();
                // 广播状态更新
                BroadcastStateUpdates();
            }
            finally
            {
                _isProcessing = false;
            }
        }

        private void ProcessOtherSystemCommands()
        {
            _interactSystem.ProcessCommands();
        }

        private void MoveCommandsToCurrentTick()
        {
            // 将待处理命令移到当前tick的命令队列
            while (_clientCommands.Count > 0)
            {
                if (!_clientCommands.TryPeek(out var command))
                {
                    continue;
                }

                var header = command.GetHeader();

                // 检查命令是否过期
                var commandAge = (CurrentTick - header.Tick) * _tickRate;
                if (commandAge > _maxCommandAge)
                {
                    if (_clientCommands.TryDequeue(out command))
                    {
                        Debug.LogWarning($"Command discarded due to age: {commandAge}s");
                        continue;
                    }
                }

                // 如果命令属于未来的tick，停止处理
                if (header.Tick > CurrentTick)
                {
                    break;
                }

                if (_clientCommands.TryDequeue(out command))
                {
                    _currentTickCommands.Enqueue(command);
                }
            }
            while (_serverCommands.Count > 0)
            {
                if (!_serverCommands.TryPeek(out var command))
                {
                    continue;
                }
                var header = command.GetHeader();

                // 检查命令是否过期
                var commandAge = (CurrentTick - header.Tick) * _tickRate;
                
                if (_serverCommands.TryDequeue(out command))
                {
                    Debug.LogWarning($"Command discarded due to age: {commandAge}s");
                    continue;
                }

                // 如果命令属于未来的tick，停止处理
                if (header.Tick > CurrentTick)
                {
                    break;
                }

                if (_serverCommands.TryDequeue(out command))
                {
                    _currentTickCommands.Enqueue(command);
                }
            }
        }

        public event Action<INetworkCommand> OnServerProcessCurrentTickCommand;
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

        public static InteractHeader CreateInteractHeader(int? connectionId, InteractCategory category, CompressedVector3 position, CommandAuthority authority = CommandAuthority.Server)
        {
            var tick = CurrentTick;
            var connectionIdValue = connectionId.GetValueOrDefault();
            var header = ObjectPool<InteractHeader>.Get();
            header.CommandId = HybridCommandId.Generate(authority == CommandAuthority.Server, ref tick);
            header.RequestConnectionId = connectionIdValue;
            header.Tick = tick;
            header.Category = category;
            header.Position = position;
            header.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            header.Authority = authority;
            return header;
        }

        public static NetworkCommandHeader CreateNetworkCommandHeader(int? connectionId, CommandType commandType, CommandAuthority authority = CommandAuthority.Server)
        {
            var tick = CurrentTick;
            var connectionIdValue = connectionId.GetValueOrDefault();
            var header = ObjectPool<NetworkCommandHeader>.Get();
            header.CommandId = HybridCommandId.Generate(authority == CommandAuthority.Server, ref tick);
            header.ConnectionId = connectionIdValue;
            header.CommandType = commandType;
            header.Tick = tick;
            header.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            header.Authority = authority;
            return header;
        }
    }
}