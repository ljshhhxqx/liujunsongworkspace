using System.Collections.Concurrent;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public abstract class PredictableStateBase : NetworkAutoInjectComponent //NetworkAutoInjectComponent用于自动给NetworkBehaviour注入依赖
    {
        // 服务器权威状态
        protected abstract ISyncPropertyState CurrentState { get; set; }
        // 预测命令队列
        protected readonly ConcurrentQueue<INetworkCommand> CommandQueue = new ConcurrentQueue<INetworkCommand>();
        protected GameSyncManager GameSyncManager;
        protected JsonDataConfig JsonDataConfig;
        protected PlayerComponentController PlayerComponentController;
        protected int LastConfirmedTick { get; private set; }
        protected abstract CommandType CommandType { get; }
        protected int InputBufferTick;
        protected NetworkIdentity NetworkIdentity;

        [Inject]
        protected virtual void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            GameSyncManager = gameSyncManager;
            JsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            InputBufferTick = JsonDataConfig.PlayerConfig.InputBufferTick;
            PlayerComponentController = GetComponent<PlayerComponentController>();
            NetworkIdentity = GetComponent<NetworkIdentity>();
        }

        // 添加预测命令
        public void AddPredictedCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (!header.CommandType.HasAnyState(CommandType) && header.ExecuteType != CommandExecuteType.Predicate) 
                return;
        
            CommandQueue.Enqueue(command);
            Debug.Log($"[PredictableStatBase]Add predicted command {header.CommandType} at tick {header.Tick}");
            while (CommandQueue.Count > 0 && GameSyncManager.CurrentTick - header.Tick > 
                   InputBufferTick)
            {
                if (CommandQueue.TryDequeue(out command))
                {
                    // 模拟命令效果
                    Simulate(command);
                    var json = MemoryPackSerializer.Serialize(command);
                    // 发送命令
                    CmdSendCommand(json);
                }
            }
        }

        [Command]
        private void CmdSendCommand(byte[] commandJson)
        {
            GameSyncManager.EnqueueCommand(commandJson);
        }

        // 清理已确认的命令
        protected virtual void CleanupConfirmedCommands(int confirmedTick)
        {
            while (CommandQueue.Count > 0)
            {
                if (CommandQueue.TryPeek(out var command) && command.GetHeader().Tick == confirmedTick)
                {
                    CommandQueue.TryDequeue(out command);
                }
            }
            LastConfirmedTick = confirmedTick;
        }

        public virtual void ApplyServerState<T>(T state) where T : ISyncPropertyState
        {
            CleanupConfirmedCommands(GameSyncManager.CurrentTick);
            if (NetworkIdentity.isLocalPlayer)
            {
                if (NeedsReconciliation(state))
                {
                    CurrentState = state;
            
                    // 重新应用未确认的命令
                    foreach (var command in CommandQueue)
                    {
                        Simulate(command);
                    }
                }
                return;
            }
            CurrentState = state;
        }

        public abstract bool NeedsReconciliation<T>(T state) where T : ISyncPropertyState;
        public abstract void Simulate(INetworkCommand command);
    }
}