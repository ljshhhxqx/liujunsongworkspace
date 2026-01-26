using System.Collections.Concurrent;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Data.State;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using Mirror;
using UnityEngine;
using INetworkCommand = AOTScripts.Data.INetworkCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public abstract class BaseSyncSystem
    {
        public Dictionary<int, ISyncPropertyState> PropertyStates { get; } = new Dictionary<int, ISyncPropertyState>();
        //存储若干个字典，字典的key为客户端的connectionId，value为客户端具体重写的IPredictableState
        protected GameSyncManager GameSyncManager { get; private set; }
        protected abstract CommandType CommandType { get; }

        public virtual void Initialize(GameSyncManager gameSyncManager)
        {
            GameSyncManager = gameSyncManager;
            GameSyncManager.OnServerProcessCurrentTickCommand += OnServerProcessCommand;
            GameSyncManager.OnBroadcastStateUpdate += OnBroadcastStateUpdate;
            GameSyncManager.OnClientProcessStateUpdate += OnClientProcessStateUpdate;
            GameSyncManager.OnPlayerConnected += OnPlayerConnected;
            GameSyncManager.OnPlayerDisconnected += OnPlayerDisconnected;
            GameSyncManager.OnGameStart += OnGameStart;
            GameSyncManager.OnAllSystemInit += OnAllSystemInit;
        }

        private void OnServerProcessCommands(ConcurrentQueue<INetworkCommand> commands)
        {
            
        }

        protected virtual void OnAllSystemInit()
        {
        }

        protected virtual void OnGameStart(bool isGameStarted)
        {
            
        }

        public PlayerComponentController GetPlayerComponentController(int connectionId)
        {
            return GameSyncManager.GetPlayerConnection(connectionId);
        }

        private void OnPlayerDisconnected(int connectionId)
        {
            UnregisterState(connectionId);
        }

        private void OnPlayerConnected(int connectionId, uint playerNetId, NetworkIdentity identity)
        {
            RegisterState(connectionId, playerNetId, identity);
            //todo: 获取PlayerComponentController，注册
        }

        /// <summary>
        /// 把服务器PropertyStates转存到客户端内
        /// </summary>
        /// <param name="state"></param>
        /// <param name="connectionId"></param>
        /// <param name="commandType"></param>
        protected abstract void OnClientProcessStateUpdate(int connectionId, byte[] state, CommandType commandType);

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

        protected abstract void RegisterState(int connectionId, uint playerNetId, NetworkIdentity player); 
        
        protected virtual void UnregisterState(int connectionId)
        {
            PropertyStates.Remove(connectionId);
        }

        protected virtual void OnServerProcessCommand(INetworkCommand command)
        {
            if (command.GetHeader().CommandType != CommandType)
            {
                return;
            }
            if (!ValidateCommand(command))
            {
                Debug.LogError($"{command.GetType().Name} not valid command type {command.GetHeader().CommandType} for {CommandType} or Command is not Valid");
                return;
            }
            ProcessCommand(command);
        }
        
        protected virtual bool ValidateCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (!PropertyStates.ContainsKey(header.ConnectionId))
            {
                Debug.LogError($"{command.GetType().Name} not valid command playerId {header.ConnectionId}");
                return false;
            }

            if (!command.IsValid())
            {
                Debug.LogError($"{command.GetType().Name} not valid command {command.GetHeader().CommandType}");
                return false;
            }

            return true;
        }

        public abstract CommandType HandledCommandType { get; }
        public abstract ISyncPropertyState ProcessCommand(INetworkCommand command);

        public virtual T GetState<T>(int connectionId) where T : ISyncPropertyState
        {
            if (PropertyStates.TryGetValue(connectionId, out var state))
            {
                return (T) state;
            }
            return default;
        }

        public abstract byte[] GetPlayerSerializedState(int connectionId);

        public abstract void SetState<T>(int connectionId, T state) where T : ISyncPropertyState;
        public abstract bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState);

        public virtual void Clear()
        {
            PropertyStates.Clear();
            GameSyncManager.OnServerProcessCurrentTickCommand -= OnServerProcessCommand;
            GameSyncManager.OnBroadcastStateUpdate -= OnBroadcastStateUpdate;
            GameSyncManager.OnClientProcessStateUpdate -= OnClientProcessStateUpdate;
            GameSyncManager.OnPlayerConnected -= OnPlayerConnected;
            GameSyncManager.OnPlayerDisconnected -= OnPlayerDisconnected;
            GameSyncManager.OnGameStart -= OnGameStart;
            GameSyncManager.OnAllSystemInit -= OnAllSystemInit;
        }
    }
}