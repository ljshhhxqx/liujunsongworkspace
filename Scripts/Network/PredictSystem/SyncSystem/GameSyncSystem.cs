﻿using System;
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
        private PlayerPropertySyncSystem _playerPropertySyncSystem;
        private bool _isProcessing; // 防止重入
        private CancellationTokenSource _cts;
        
        private InteractSystem _interactSystem;

        [SyncVar(hook = nameof(OnIsRandomUnionStartChanged))] 
        public bool isRandomUnionStart;
        
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
            _cts = new CancellationTokenSource();
            _tickRate = _jsonDataConfig.GameConfig.tickRate;
            _maxCommandAge = _jsonDataConfig.GameConfig.maxCommandAge;
            gameEventManager.Subscribe<PlayerConnectEvent>(OnPlayerConnect);
            gameEventManager.Subscribe<PlayerDisconnectEvent>(OnPlayerDisconnect);
            gameEventManager.Subscribe<AddBuffToAllPlayerEvent>(OnAddBuffToAllPlayer);
            gameEventManager.Subscribe<AddDeBuffToLowScorePlayerEvent>(OnAddDeBuffToLowScorePlayer);
            gameEventManager.Subscribe<AllPlayerGetSpeedEvent>(OnAllPlayerGetSpeed);
            var commandTypes = Enum.GetValues(typeof(CommandType));
            foreach (CommandType commandType in commandTypes)
            {
                _syncSystems[commandType] = commandType.GetSyncSystem();
                _syncSystems[commandType].Initialize(this);
                ObjectInjectProvider.Instance.Inject(_syncSystems[commandType]);
                if (_syncSystems[commandType] is PlayerPropertySyncSystem playerPropertySyncSystem)
                {
                    _playerPropertySyncSystem = playerPropertySyncSystem;
                }
            }
            Observable.EveryUpdate()
                .Throttle(TimeSpan.FromSeconds(1 / _tickRate))
                .Where(_ => isServer && !_isProcessing)
                .Subscribe(_ =>
                {
                    _tickTimer = 0;
                    ProcessTick();
                    CurrentTick++;
                })
                .AddTo(this);
                
            ProcessImmediateCommands(_cts.Token);
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
                _playerInGameManager.RandomUnion(out var noUnionPlayerId);
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
                // 广播状态更新
                BroadcastStateUpdates();
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

        public static InteractHeader CreateInteractHeader(int? connectionId, InteractCategory category, CompressedVector3 position = default, CommandAuthority authority = CommandAuthority.Server)
        {
            int? noSequence = null;
            var connectionIdValue = connectionId.GetValueOrDefault();
            var header = ObjectPool<InteractHeader>.Get();
            header = default;
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