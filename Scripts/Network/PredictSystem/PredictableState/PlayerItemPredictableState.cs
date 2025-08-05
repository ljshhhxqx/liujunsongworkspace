using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool.Static;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using MemoryPack;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerItemPredictableState : PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Item;
        private ItemConfig _itemConfig;
        private BindingKey _bindKey;

        [Inject]
        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            _bindKey = new BindingKey(UIPropertyDefine.BagItem);
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            return state is not null && state is PlayerItemState;
        }

        public void RegisterState(PlayerItemState state)
        {
            //OnPlayerItemUpdate(state);
        }

        private PlayerItemState GetPlayerItemState()
        {
            if (CurrentState is not PlayerItemState playerItemState)
            {
                return default;
            }
            return playerItemState;
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
                    for (var i = 0; i < itemsGetCommand.Items.Count; i++)
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
                case ItemExchangeCommand itemExchangeCommand:
                    PlayerItemCalculator.CommandExchangeItem(itemExchangeCommand, ref playerItemState);
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

        private void OnUseItem(int slotIndex, int count)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var useItem = new SlotIndexData
            {
                SlotIndex = slotIndex,
                Count = count
            };
            var dic = new MemoryDictionary<int, SlotIndexData>(1);
            dic.Add(slotIndex, useItem);
            var useItemCommand = new ItemsUseCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = dic
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(useItemCommand).Item1);
        }

        private void OnEquipItem(int slotIndex, bool isEquip)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var state = GetPlayerItemState();
            var playerItemType = state.PlayerItemConfigIdSlotDictionary[slotIndex].PlayerItemType;
            var equipItemCommand = new ItemEquipCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                SlotIndex = slotIndex,
                PlayerItemType = playerItemType,
                IsEquip = isEquip
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(equipItemCommand).Item1);
        }

        private void OnLockItem(int slotIndex, bool isLock)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var lockItemCommand = new ItemLockCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                SlotIndex = slotIndex,
                IsLocked = isLock
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(lockItemCommand).Item1);
        }

        private void OnDropItem(int slotIndex, int count)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var dropItem = new SlotIndexData
            {
                SlotIndex = slotIndex,
                Count = count
            };
            var dic = new MemoryDictionary<int, SlotIndexData>(1);
            dic.Add(slotIndex, dropItem);
            var dropItemCommand = new ItemDropCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = dic
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(dropItemCommand).Item1);
        }

        private void OnExchangeItem(int fromSlotIndex, int toSlotIndex)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var exchangeItemCommand = new ItemExchangeCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                FromSlotIndex = fromSlotIndex,
                ToSlotIndex = toSlotIndex
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(exchangeItemCommand).Item1);
        }

        private void OnSellItem(int slotIndex, int count)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var sellItem = new SlotIndexData
            {
                SlotIndex = slotIndex,
                Count = count
            };
            var list = new MemoryList<SlotIndexData>(1);//<int, SlotIndexData>();
            list.Add(sellItem);
            var sellItemCommand = new ItemsSellCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = list
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(sellItemCommand).Item1);
        }

        private void OnPlayerItemUpdate(PlayerItemState playerItemState)
        {
            if (!NetworkIdentity.isLocalPlayer)
                return;
            //Debug.Log("OnPlayerItemUpdate");
            CurrentState = playerItemState;
            var bagItems = UIPropertyBinder.GetReactiveDictionary<BagItemData>(_bindKey);
            foreach (var kvp in playerItemState.PlayerItemConfigIdSlotDictionary)
            {
                var playerBagSlotItem = kvp.Value;
                var itemConfig = _itemConfig.GetGameItemData(playerBagSlotItem.ConfigId);
                var mainProperty = GameStaticExtensions.GetBuffEffectDesc(playerBagSlotItem.MainIncreaseDatas.Items);
                var randomBuffEffectDesc = GameStaticExtensions.GetRandomBuffEffectDesc(playerBagSlotItem.RandomIncreaseDatas.Items);
                var passiveProperty =
                    GameStaticExtensions.GetBuffEffectDesc(playerBagSlotItem.PassiveAttributeIncreaseDatas.Items);
                var bagItem = new BagItemData
                {
                    ItemName = itemConfig.name,
                    Index = playerBagSlotItem.IndexSlot,
                    Stack = playerBagSlotItem.Count,
                    Icon = UISpriteContainer.GetSprite(itemConfig.iconName),
                    QualityIcon = UISpriteContainer.GetQualitySprite(itemConfig.quality),
                    Description = itemConfig.desc,
                    PropertyDescription = mainProperty ?? "",
                    RandomDescription = randomBuffEffectDesc ?? "",
                    PassiveDescription = passiveProperty,
                    PlayerItemType = itemConfig.itemType,
                    IsEquip = playerBagSlotItem.State == ItemState.IsEquipped,
                    IsLock = playerBagSlotItem.State == ItemState.IsLocked,
                    MaxStack = itemConfig.maxStack,
                    OnUseItem = OnUseItem,
                    OnDropItem = OnDropItem,
                    OnLockItem = OnLockItem,
                    OnEquipItem = OnEquipItem,
                    OnExchangeItem = OnExchangeItem,
                    OnSellItem = OnSellItem,
                };
                Debug.Log(bagItem.ToString());
                bagItems.TryAdd(kvp.Key, bagItem);
            }
        }
    }
}