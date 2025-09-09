using System.Collections.Concurrent;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using MemoryPack;
using Mirror;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    //客户端
    public abstract class SyncStateBase : NetworkAutoInjectComponent
    {
        protected abstract ISyncPropertyState CurrentState { get; set; }
        protected readonly ConcurrentQueue<INetworkCommand> CommandQueue = new ConcurrentQueue<INetworkCommand>();
        protected GameSyncManager GameSyncManager;
        protected JsonDataConfig JsonDataConfig;
        protected int LastConfirmedTick { get; private set; }
        protected abstract CommandType CommandType { get; }
        protected abstract void SetState<T>(T state) where T : ISyncPropertyState;
        
        [Inject]
        protected virtual void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            GameSyncManager = gameSyncManager;
            JsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
        }
        
        protected abstract void ProcessCommand(INetworkCommand networkCommand);
        
        // 添加预测命令
        public void AddPredictedCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (!header.CommandType.HasAnyState(CommandType)) 
                return;
            //command.SetHeader(netIdentity.connectionToClient.connectionId, CommandType, GameSyncManager.CurrentTick);
        
            CommandQueue.Enqueue(command);
            while (CommandQueue.Count > 0 && GameSyncManager.CurrentTick - command.GetHeader().Tick > JsonDataConfig.PlayerConfig.InputBufferTick)
            {
                if (CommandQueue.TryDequeue(out command))
                {
                    // 模拟命令效果
                    ProcessCommand(command);
                    var json = MemoryPackSerializer.Serialize(command);
                    // 发送命令
                    CmdSendCommand(json);
                }
            }
        }

        [Command]
        protected void CmdSendCommand(byte[] commandJson)
        {
            GameSyncManager.EnqueueCommand(commandJson);
        }
    }
}