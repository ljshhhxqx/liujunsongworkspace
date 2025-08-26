using HotUpdate.Scripts.Network.PredictSystem.Calculator;
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

            for (int i = 0; i < equipmentState.EquipmentDatas.Count; i++)
            {
                var data = equipmentState.EquipmentDatas[i];
                if (data == null)
                    continue;
                equipmentState.EquipmentDatas[i] = data;
            }
        }

        protected override void ProcessCommand(INetworkCommand networkCommand)
        {
            if (CurrentState is not PlayerEquipmentState equipmentState)
            {
                return;
            }

            if (networkCommand is EquipmentCommand equipmentCommand)
            {
                PlayerEquipmentCalculator.CommandEquipment(equipmentCommand, ref equipmentState);
                CurrentState = equipmentState;
            }
        }

        public void ApplyState<T>(T state) where T : ISyncPropertyState
        {
            SetState(state);
        }
    }
}