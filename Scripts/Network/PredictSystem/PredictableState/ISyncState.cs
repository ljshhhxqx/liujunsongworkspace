using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.PredictSystem.State;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public abstract class SyncStateBase : NetworkAutoInjectComponent
    {
        protected abstract ISyncPropertyState CurrentState { get; set; }
    }
}