using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using MemoryPack;
using Mirror;
using VContainer;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public abstract class PredictableStateBase : NetworkAutoInjectComponent //NetworkAutoInjectComponent用于自动给NetworkBehaviour注入依赖
    {
        // 服务器权威状态
        protected abstract IPropertyState CurrentState { get; set; }
        protected NetworkIdentity NetworkIdentity;
        // 预测命令队列
        protected readonly Queue<INetworkCommand> CommandQueue = new Queue<INetworkCommand>();
        protected GameSyncManager GameSyncManager;
        protected JsonDataConfig JsonDataConfig;
        protected int LastConfirmedTick { get; private set; }
        protected abstract CommandType CommandType { get; }

        [Inject]
        protected virtual void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            GameSyncManager = gameSyncManager;
            JsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            NetworkIdentity = GetComponent<NetworkIdentity>();
        }

        //本地客户端用于模拟命令，立即执行
        public void AddCommandByOtherPredictableState(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType != HandledCommandType) 
                return;
            command.SetHeader(netIdentity.connectionToClient.connectionId, CommandType, GameSyncManager.CurrentTick);
            Simulate(command);
        }

        // 添加预测命令
        public void AddPredictedCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType != HandledCommandType) 
                return;
            command.SetHeader(netIdentity.connectionToClient.connectionId, CommandType, GameSyncManager.CurrentTick);
        
            CommandQueue.Enqueue(command);
            while (CommandQueue.Count > 0 && GameSyncManager.CurrentTick - command.GetHeader().Tick > JsonDataConfig.PlayerConfig.InputBufferTick)
            {
                CommandQueue.Dequeue();
            }
            // 模拟命令效果
            Simulate(command);
            var json = MemoryPackSerializer.Serialize(command);
            // 发送命令
            CmdSendCommand(json);
        }

        [Command]
        private void CmdSendCommand(byte[] commandJson)
        {
            GameSyncManager.EnqueueCommand(commandJson);
        }

        // 清理已确认的命令
        protected virtual void CleanupConfirmedCommands(int confirmedTick)
        {
            while (CommandQueue.Count > 0 && 
                   CommandQueue.Peek().GetHeader().Tick <= confirmedTick)
            {
                CommandQueue.Dequeue();
            }
            LastConfirmedTick = confirmedTick;
        }
        
        public abstract CommandType HandledCommandType { get; }

        public virtual void ApplyServerState<T>(T state) where T : IPropertyState
        {
            CleanupConfirmedCommands(GameSyncManager.CurrentTick);
            if (isLocalPlayer)
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

        public abstract bool NeedsReconciliation<T>(T state) where T : IPropertyState;
        public abstract void Simulate(INetworkCommand command);
    }
}