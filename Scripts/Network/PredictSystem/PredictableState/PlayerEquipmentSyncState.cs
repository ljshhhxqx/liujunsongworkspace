using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerEquipmentSyncState : SyncStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Equipment;
        
        protected override void SetState<T>(T state)
        {
            if (state is not PlayerEquipmentState equipmentState)
                return;
            CurrentState = equipmentState;
        }

        protected override void ProcessCommand(INetworkCommand networkCommand)
        {
            
        }

        public void ApplyState<T>(T state) where T : ISyncPropertyState
        {
            SetState(state);
        }
    }
}