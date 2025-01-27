using System.Collections.Generic;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using Mirror;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState
{
    public abstract class PredictableStateBase : NetworkBehaviour
    {
        protected abstract IPropertyState ServerState { get; set; }
        protected NetworkIdentity NetworkIdentity;
        protected Queue<INetworkCommand> CommandQueue = new Queue<INetworkCommand>();
        
        public IPropertyState PropertyState => ServerState;
    
        protected virtual void Awake()
        {
            NetworkIdentity = GetComponent<NetworkIdentity>();
        }
        
        public virtual void AddPredictedCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.connectionId != NetworkIdentity.connectionToClient.connectionId || header.commandType != HandledCommandType) return;
            
            Simulate(command);
        }
        
        protected virtual void CleanupConfirmedCommands(int confirmedTick)
        {
            while (CommandQueue.Count > 0 && 
                   CommandQueue.Peek().GetHeader().tick <= confirmedTick)
            {
                CommandQueue.Dequeue();
            }
        }

        public abstract void SetServerState<T>(T state) where T : IPropertyState;
        public abstract CommandType HandledCommandType { get; }
        public abstract void ApplyServerState<T>(T state)where T : IPropertyState;
        public abstract bool NeedsReconciliation<T>(T state)where T : IPropertyState;

        public virtual void SetClientStateFromClient<T>(T state) where T : IPropertyState
        {
            ServerState = state;
        }

        public abstract void Simulate(INetworkCommand command);
    }
}