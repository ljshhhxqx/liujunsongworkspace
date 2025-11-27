using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Data.State;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using Mirror;
using VContainer;
using INetworkCommand = AOTScripts.Data.INetworkCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public abstract class PredictableStateBase : NetworkAutoInjectHandlerBehaviour
    {
        protected abstract ISyncPropertyState CurrentState { get; set; }
        protected readonly ConcurrentQueue<INetworkCommand> CommandQueue = new ConcurrentQueue<INetworkCommand>();
        protected readonly Dictionary<uint, byte[]> CommandBuffer = new Dictionary<uint, byte[]>();
        protected GameSyncManager GameSyncManager;
        protected JsonDataConfig JsonDataConfig;
        protected PlayerComponentController PlayerComponentController;
        protected int LastConfirmedTick { get; private set; } = -1;
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
           // Debug.Log($"[PredictableStateBase] Initialized with input buffer tick {InputBufferTick}");
        }

        [Command]
        protected virtual void CmdSendCommand(byte[] commandJson)
        {
            GameSyncManager.EnqueueCommand(commandJson);
        }

        // 添加预测命令（不立即执行）
        public void AddPredictedCommand<T>(T command) where T : INetworkCommand
        {
            if (isServer && !isLocalPlayer)
            {
                return;
            }
            var header = command.GetHeader();
            if (header.CommandType != CommandType) return;
            
            CommandQueue.Enqueue(command);
            var buffer = NetworkCommandExtensions.SerializeCommand(command);
            if (CommandBuffer.ContainsKey(header.CommandId))
            {
                foreach (var key in CommandBuffer.Keys)
                {
                    if (key == header.CommandId)
                    {
                        //Debug.Log($"[PredictableStateBase] Command {header.CommandType} with id {header.CommandId} already exists in buffer.");
                    }
                }   
            }            
            //Debug.Log($"[PredictableStateBase] start Added predicted command {header.CommandType} with id {header.CommandId} to buffer at tick {header.Tick}");

            CommandBuffer.TryAdd(header.CommandId, buffer.Item1);
            //Debug.Log($"[PredictableStateBase] finish Added predicted command {header.CommandType} with id {header.CommandId} to buffer at tick {header.Tick}");
            //Debug.Log($"[PredictableStateBase] Added predicted command {header.CommandType} at tick {header.Tick}");
        }

        // 执行当前tick应执行的预测命令
        public virtual void ExecutePredictedCommands(int currentTick)
        {
            while (CommandQueue.Count > 0)
            {
                CommandQueue.TryPeek(out var command);
                var header = command.GetHeader();
                
                if (header.Tick > currentTick)
                {
                    break; // 未来tick的命令等待执行
                }

                CommandQueue.TryDequeue(out command);
                Simulate(command);
                SendCommandToServer(header.CommandId);
                // if (command is InputCommand inputCommand && (inputCommand.CommandAnimationState is AnimationState.Attack or AnimationState.Jump or AnimationState.SkillE or AnimationState.SkillQ or AnimationState.SprintJump))
                // {
                //     Debug.Log($"[PredictableStateBase] Executed predicted command {header.CommandId} {inputCommand.CommandAnimationState} at tick {header.Tick}");
                // }
            }
        }
        
        private void SendCommandToServer(uint commandId)
        {
            if (CommandBuffer.TryGetValue(commandId, out var json))
            {
                PlayerComponentController.CmdSendCommand(json);
                CommandBuffer.Remove(commandId);
                //return;
            }

            //Debug.LogError($"[PredictableStateBase] Command {commandId} not found in buffer.");
        }

        // 清理已确认的命令
        protected virtual void CleanupConfirmedCommands(int confirmedTick)
        {
            while (CommandQueue.Count > 0)
            {
                if (CommandQueue.TryPeek(out var command) && command.GetHeader().Tick <= confirmedTick)
                {
                    CommandQueue.TryDequeue(out _);
                    CommandBuffer.Remove(command.GetHeader().CommandId);
                }
            }
            LastConfirmedTick = confirmedTick;
        }

        public virtual void ApplyServerState<T>(T state) where T : ISyncPropertyState
        {
            CleanupConfirmedCommands(GameSyncManager.CurrentTick);
            
            if (isLocalPlayer)
            {
                //Debug.Log($"[PredictableStateBase] ApplyServerState {state.GetType().Name} at tick {GameSyncManager.CurrentTick}");
                 if (NeedsReconciliation(state))
                 {
                     //Debug.Log($"[PredictableStateBase] Reconciliation needed for {state.GetType().Name} at tick {GameSyncManager.CurrentTick}");
                     // 重置到服务器状态
                     InitCurrentState(state);
                     
                     var commands = GetUnconfirmedCommands();
                     // 仅重放未确认的命令
                     foreach (var command in commands)
                     {
                         Simulate(command);
                     }
                 }
            }
            else
            {
                InitCurrentState(state);
            }

            // if (NetworkIdentity.isClient) 
            // {
            //     
            // }
        }

        public abstract bool NeedsReconciliation<T>(T state) where T : ISyncPropertyState;
        public abstract void Simulate(INetworkCommand command);

        public virtual void InitCurrentState<T>(T state) where T : ISyncPropertyState
        {
            //Debug.Log($"[PredictableStateBase] InitCurrentState {state.GetType().Name} at tick {GameSyncManager.CurrentTick}");
            CurrentState = state;
        }

        // 新增：获取未确认的命令
        public IEnumerable<INetworkCommand> GetUnconfirmedCommands()
        {
            return CommandQueue.Where(c => c.GetHeader().Tick > LastConfirmedTick);
        }
    }
}