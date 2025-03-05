using System.Collections.Concurrent;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerItemSyncState : SyncStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType { get; }

        protected override void SetState<T>(T state)
        {
            CurrentState = state;
        }

        protected override void ProcessCommand(INetworkCommand networkCommand)
        {
            throw new System.NotImplementedException();
        }
    }
}