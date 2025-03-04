using System.Collections.Generic;
using HotUpdate.Scripts.Network.Data.PredictSystem;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using Mirror;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public abstract class BaseSyncSystem
    {
        protected readonly Dictionary<int, IPredictablePropertyState> PropertyStates = new Dictionary<int, IPredictablePropertyState>();
        //存储若干个字典，字典的key为客户端的connectionId，value为客户端具体重写的IPredictableState
        protected GameSyncManager GameSyncManager { get; private set; }

        public virtual void Initialize(GameSyncManager gameSyncManager)
        {
            GameSyncManager = gameSyncManager;
            GameSyncManager.OnServerProcessCurrentTickCommand += OnServerProcessCommand;
            GameSyncManager.OnBroadcastStateUpdate += OnBroadcastStateUpdate;
            GameSyncManager.OnClientProcessStateUpdate += OnClientProcessStateUpdate;
            GameSyncManager.OnPlayerConnected += OnPlayerConnected;
            GameSyncManager.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        public PlayerComponentController GetPlayerComponentController(int connectionId)
        {
            return GameSyncManager.GetPlayerConnection(connectionId);
        }

        private void OnPlayerDisconnected(int connectionId)
        {
            UnregisterState(connectionId);
        }

        private void OnPlayerConnected(int connectionId, NetworkIdentity identity)
        {
            RegisterState(connectionId, identity);
            //todo: 获取PlayerComponentController，注册
        }

        /// <summary>
        /// 把服务器PropertyStates转存到客户端内
        /// </summary>
        /// <param name="state"></param>
        protected abstract void OnClientProcessStateUpdate(byte[] state);

        /// <summary>
        /// For Client(更新每个客户端PredictableStateBase的CurrentState)
        /// </summary>
        protected virtual void OnBroadcastStateUpdate()
        {
            foreach (var kvp in PropertyStates)
            {
                SetState(kvp.Key, kvp.Value);
            }
        }

        protected abstract void RegisterState(int connectionId, NetworkIdentity player); 
        
        protected virtual void UnregisterState(int connectionId)
        {
            PropertyStates.Remove(connectionId);
        }

        protected virtual void OnServerProcessCommand(INetworkCommand command)
        {
            if (!ValidateCommand(command))
            {
                return;
            }
            ProcessCommand(command);
        }
        
        protected virtual bool ValidateCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            return PropertyStates.ContainsKey(header.ConnectionId) && command.IsValid();
        }

        public abstract CommandType HandledCommandType { get; }
        public abstract IPredictablePropertyState ProcessCommand(INetworkCommand command);

        public virtual T GetState<T>(int connectionId) where T : IPredictablePropertyState
        {
            if (PropertyStates.TryGetValue(connectionId, out var state))
            {
                return (T) state;
            }
            return default;
        }

        public virtual byte[] GetAllState()
        {
            return MemoryPackSerializer.Serialize(PropertyStates);
        }

        public abstract void SetState<T>(int connectionId, T state) where T : IPredictablePropertyState;
        public abstract bool HasStateChanged(IPredictablePropertyState oldState, IPredictablePropertyState newState);
        public abstract void Clear();
    }
}