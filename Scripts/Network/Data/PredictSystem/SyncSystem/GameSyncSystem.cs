using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem
{
    public class GameSyncManager : NetworkBehaviour
    {
        private readonly Queue<INetworkCommand> _pendingCommands = new Queue<INetworkCommand>();
        private readonly List<int> _playerConnectionIds = new List<int>();
        private readonly Queue<INetworkCommand> _currentTickCommands = new Queue<INetworkCommand>();
        private readonly Dictionary<CommandType, BaseSyncSystem> _syncSystems = new Dictionary<CommandType, BaseSyncSystem>();
        [Header("Sync Settings")]
        [SerializeField] private float tickRate = 1/30f; // 服务器每秒发送30个tick
        [SerializeField] private float maxCommandAge = 1f; // 最大命令存活时间
        private NetworkIdentity _networkIdentity;
        public float TickRate => tickRate;
        private float _tickTimer;
        private IConfigProvider _configProvider;
        private bool _isProcessing; // 防止重入
        
        public int CurrentTick { get; private set; }

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            _configProvider = configProvider;
            _networkIdentity = GetComponent<NetworkIdentity>();
            if (!isServer)
            {
                _syncSystems.Clear();
                _pendingCommands.Clear();
                _currentTickCommands.Clear();
            }
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
            _playerConnectionIds.Remove(disconnectEvent.ConnectionId);
            OnPlayerDisconnected?.Invoke(disconnectEvent.ConnectionId);
        }

        private void OnPlayerConnect(PlayerConnectEvent connectEvent)
        {
            _playerConnectionIds.Add(connectEvent.ConnectionId);
            OnPlayerConnected?.Invoke(connectEvent.ConnectionId, _networkIdentity);
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

        private void Update()
        {
            if (!isServer || _isProcessing) return;

            _tickTimer += Time.deltaTime;
        
            // 检查是否需要处理新的tick
            if (_tickTimer >= tickRate)
            {
                _tickTimer = 0;
                ProcessTick();
                CurrentTick++;
            }
        }

        [Server]
        public void EnqueueCommand<T>(T command) where T : INetworkCommand
        {
            var header = command.GetHeader();
            if (header.isClientCommand || !command.IsValid())
            {
                Debug.LogError($"Invalid command: {header.commandType}");
                return;
            }
            _pendingCommands.Enqueue(command);
        }

        [Server]
        private void ProcessTick()
        {
            _isProcessing = true;

            try
            {
                // 将待处理命令移到当前tick的命令队列
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
            while (_pendingCommands.Count > 0)
            {
                var command = _pendingCommands.Peek();
                var header = command.GetHeader();

                // 检查命令是否过期
                var commandAge = (CurrentTick - header.tick) * tickRate;
                if (commandAge > maxCommandAge)
                {
                    _pendingCommands.Dequeue(); // 丢弃过期命令
                    Debug.LogWarning($"Command discarded due to age: {commandAge}s");
                    continue;
                }

                // 如果命令属于未来的tick，停止处理
                if (header.tick > CurrentTick)
                {
                    break;
                }

                _currentTickCommands.Enqueue(_pendingCommands.Dequeue());
            }
        }

        public event Action<INetworkCommand, NetworkIdentity> OnServerProcessCurrentTickCommand;
        private void ProcessCurrentTickCommands()
        {
            while (_currentTickCommands.Count > 0)
            {
                var command = _currentTickCommands.Dequeue();
                var header = command.GetHeader();
                OnServerProcessCurrentTickCommand?.Invoke(command, _networkIdentity);
                var syncSystem = GetSyncSystem(header.commandType);
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
        public void EnqueueServerCommand<T>(T command) where T : INetworkCommand
        {
            var header = command.GetHeader();
            if (!isServer || header.isClientCommand)
            {
                Debug.LogError($"Invalid command: {header.commandType}");
                return;
            }
            _currentTickCommands.Enqueue(command);
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

        public event Action<string> OnClientProcessStateUpdate;
        [ClientRpc]
        private void RpcProcessCurrentTickCommand(string state)
        {
            OnClientProcessStateUpdate?.Invoke(state);
        }

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

        #region Debug

        // private void OnGUI()
        // {
        //     if (!isServer) return;
        //
        //     GUILayout.BeginArea(new Rect(10, 10, 200, 200));
        //     GUILayout.Label($"Current Tick: {_currentTick}");
        //     GUILayout.Label($"Pending Commands: {_pendingCommands.Count}");
        //     GUILayout.Label($"Current Tick Commands: {_currentTickCommands.Count}");
        //     GUILayout.Label($"Tick Timer: {_tickTimer:F3}");
        //     GUILayout.EndArea();
        // }

        #endregion
    }
}