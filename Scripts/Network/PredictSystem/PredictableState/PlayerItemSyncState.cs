using HotUpdate.Scripts.Network.PredictSystem.State;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerItemSyncState : SyncStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
    }
}