using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerItemSyncState : PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Item;
        
        public override bool NeedsReconciliation<T>(T state)
        {
            return state is not null && state is PlayerItemState;
        }

        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType != CommandType.Item)
            {
                return;
            }
            switch (command)
            {
                case ItemUseCommand itemUseCommand:
                    break;
                case ItemEquipCommand itemEquipCommand:
                    break;
                case ItemLockCommand itemLockCommand:
                    break;
                case ItemDropCommand itemDropCommand:
                    break;
            }
        }
    }
}