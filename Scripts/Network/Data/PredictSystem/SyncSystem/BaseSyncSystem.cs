using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem
{
    public abstract class BaseSyncSystem
    {
        protected readonly Dictionary<int, IPropertyState> PropertyStates = new Dictionary<int, IPropertyState>();
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

        private void OnPlayerDisconnected(int connectionId)
        {
            UnregisterState(connectionId);
        }

        private void OnPlayerConnected(int connectionId, NetworkIdentity identity)
        {
            RegisterState(connectionId, identity);
        }

        protected abstract void OnClientProcessStateUpdate(string stateJson);

        protected virtual void OnBroadcastStateUpdate(int connectionId)
        {
            if (PropertyStates.TryGetValue(connectionId, out var state))
            {
                SetState(connectionId, state);
            }
        }

        protected abstract void RegisterState(int connectionId, NetworkIdentity player); 
        
        protected virtual void UnregisterState(int connectionId)
        {
            PropertyStates.Remove(connectionId);
        }

        protected virtual void OnServerProcessCommand(INetworkCommand command, NetworkIdentity identity)
        {
            if (!ValidateCommand(command))
            {
                return;
            }
            ProcessCommand(command, identity);
        }
        
        protected virtual bool ValidateCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            return PropertyStates.ContainsKey(header.connectionId) && command.IsValid();
        }

        public abstract CommandType HandledCommandType { get; }
        public abstract IPropertyState ProcessCommand(INetworkCommand command, NetworkIdentity identity);

        public virtual T GetState<T>(int connectionId) where T : IPropertyState
        {
            if (PropertyStates.TryGetValue(connectionId, out var state))
            {
                return (T) state;
            }
            return default;
        }

        public virtual string GetAllState()
        {
            return JsonConvert.SerializeObject(PropertyStates);
            //return PropertyStates.Select(x => new PlayerState {connectionId = x.Key, State = x.Value} as IPlayerState).ToArray() as T[];
        }

        public abstract void SetState<T>(int connectionId, T state) where T : IPropertyState;
        public abstract bool HasStateChanged(IPropertyState oldState, IPropertyState newState);
    }

    public interface IServerSystem
    {
        void Initialize(GameSyncManager gameSyncManager);
        void ProcessServerCommand(INetworkCommand command, NetworkIdentity identity);
    }

    public interface IPropertySyncSystem : IServerSystem
    {
        void ModifyProperty(PropertyCommand command, NetworkIdentity identity);
    }
}