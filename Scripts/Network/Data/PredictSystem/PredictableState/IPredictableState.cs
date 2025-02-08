using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Inject;
using Mirror;
using Newtonsoft.Json;
using VContainer;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState
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
    
        // 添加预测命令
        public void AddPredictedCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.commandType != HandledCommandType) 
                return;
            command.SetHeader(netIdentity.connectionToClient.connectionId, CommandType, GameSyncManager.CurrentTick, netIdentity);
        
            CommandQueue.Enqueue(command);
            while (CommandQueue.Count > 0 && GameSyncManager.CurrentTick - command.GetHeader().tick > JsonDataConfig.PlayerConfig.InputBufferTick)
            {
                CommandQueue.Dequeue();
            }
            // 模拟命令效果
            Simulate(command);
            var json = JsonConvert.SerializeObject(command, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All,
                Formatting = Formatting.Indented
            });
            // 发送命令
            CmdSendCommand(json);
        }

        [Command]
        private void CmdSendCommand(string commandJson)
        {
            GameSyncManager.EnqueueCommand(commandJson, true);
        }

        // 清理已确认的命令
        protected virtual void CleanupConfirmedCommands(int confirmedTick)
        {
            while (CommandQueue.Count > 0 && 
                   CommandQueue.Peek().GetHeader().tick <= confirmedTick)
            {
                CommandQueue.Dequeue();
            }
        }
        public abstract CommandType HandledCommandType { get; }

        public virtual void ApplyServerState<T>(T state) where T : IPropertyState
        {
            var serverTick = GameSyncManager.CurrentTick;
            CleanupConfirmedCommands(serverTick);
            LastConfirmedTick = serverTick;
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
            }
            CurrentState = state;
        }

        public abstract bool NeedsReconciliation<T>(T state) where T : IPropertyState;
        public abstract void Simulate(INetworkCommand command);
    }
}