using System;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using UniRx;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerItemSyncState : PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Item;
        private ReactiveDictionary<int,BagItemData> _bagItems;
        private BindingKey _bindKey;

        [Inject]
        private void Init()
        {
            _bindKey = new BindingKey(UIPropertyDefine.BagItem);
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            return state is not null && state is PlayerItemState;
        }

        public void RegisterState(PlayerItemState state)
        {
            OnPlayerItemUpdate(state);
        }

        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType.HasAnyState(CommandType) || CurrentState is not PlayerItemState playerItemState)
            {
                return;
            }
            switch (command)
            {
                case ItemsGetCommand itemsGetCommand:
                    for (var i = 0; i < itemsGetCommand.Items.Length; i++)
                    {
                        PlayerItemCalculator.CommandGetItem(ref playerItemState, itemsGetCommand.Items[i], header);
                    }
                    break;
                case ItemsUseCommand itemUseCommand:
                    PlayerItemCalculator.CommandUseItems(itemUseCommand, ref playerItemState);
                    break;
                case ItemEquipCommand itemEquipCommand:
                    PlayerItemCalculator.CommandEquipItem(itemEquipCommand, ref playerItemState, header.ConnectionId);
                    break;
                case ItemLockCommand itemLockCommand:
                    PlayerItemCalculator.CommandLockItem(itemLockCommand, ref playerItemState);
                    break;
                case ItemDropCommand itemDropCommand:
                    PlayerItemCalculator.CommandDropItem(itemDropCommand, ref playerItemState , header.ConnectionId);
                    break;
                case ItemsBuyCommand itemBuyCommand:
                    PlayerItemCalculator.CommandBuyItem(itemBuyCommand, ref playerItemState);
                    break;
                case ItemsSellCommand itemSellCommand:
                    PlayerItemCalculator.CommandSellItem(itemSellCommand, ref playerItemState, header.ConnectionId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void ApplyServerState<T>(T state)
        {
            if (state is not PlayerItemState playerItemState)
            {
                return;
            }
            base.ApplyServerState(playerItemState);
            CurrentState = playerItemState;
            OnPlayerItemUpdate(playerItemState);
        }

        private void OnPlayerItemUpdate(PlayerItemState playerItemState)
        {
            _bagItems ??= UIPropertyBinder.GetReactiveDictionary<BagItemData>(_bindKey);
        }
    }
}