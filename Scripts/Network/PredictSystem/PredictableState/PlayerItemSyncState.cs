﻿using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using MemoryPack;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerItemSyncState : PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Item;
        private ReactiveDictionary<int,BagItemData> _bagItems;
        private ItemConfig _itemConfig;
        private BindingKey _bindKey;
        private Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>();

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

        private void OnUseItem(int slotIndex, int count)
        {
            if(!isLocalPlayer)
                return;
            var useItemCommand = new ItemsUseCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = new SlotIndexData[]
                {
                    new SlotIndexData
                    {
                        SlotIndex = slotIndex,
                        Count = count
                    }
                }
            };
            GameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(useItemCommand));
        }

        private void OnEquipItem(int slotIndex, bool isEquip, PlayerItemType playerItemType)
        {
            if(!isLocalPlayer)
                return;
            var equipItemCommand = new ItemEquipCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                SlotIndex = slotIndex,
                PlayerItemType = playerItemType,
                IsEquip = isEquip
            };
            GameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(equipItemCommand));
        }

        private void OnLockItem(int slotIndex, bool isLock)
        {
            if(!isLocalPlayer)
                return;
            var lockItemCommand = new ItemLockCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                SlotIndex = slotIndex,
                IsLocked = isLock
            };
            GameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(lockItemCommand));
        }

        private void OnDropItem(int slotIndex, int count)
        {
            if(!isLocalPlayer)
                return;
            var dropItemCommand = new ItemDropCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = new SlotIndexData[]
                {
                    new SlotIndexData
                    {
                        SlotIndex = slotIndex,
                        Count = count
                    }
                }
            };
            GameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(dropItemCommand));
        }

        private void OnExchangeItem(int fromSlotIndex, int toSlotIndex)
        {
            if(!isLocalPlayer)
                return;
            var exchangeItemCommand = new ItemExchangeCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                FromSlotIndex = fromSlotIndex,
                ToSlotIndex = toSlotIndex
            };
            GameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(exchangeItemCommand));
        }

        private void OnSellItem(int slotIndex, int count)
        {
            if(!isLocalPlayer)
                return;
            var sellItemCommand = new ItemsSellCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = new SlotIndexData[]
                {
                    new SlotIndexData
                    {
                        SlotIndex = slotIndex,
                        Count = count
                    }
                }
            };
            GameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(sellItemCommand));
        }

        private void OnPlayerItemUpdate(PlayerItemState playerItemState)
        {
            if (!isLocalPlayer)
                return;
            _bagItems ??= UIPropertyBinder.GetReactiveDictionary<BagItemData>(_bindKey);
            foreach (var item in playerItemState.PlayerItemConfigIdSlotDictionary.Keys)
            {
                var bagItemData = playerItemState.PlayerItemConfigIdSlotDictionary[item];
                var itemConfig = _itemConfig.GetGameItemData(bagItemData.ConfigId);
                
                var bagItem = new BagItemData
                {
                    ItemName = itemConfig.name,
                    Index = bagItemData.IndexSlot,
                    Stack = bagItemData.Count,
                    Icon = UISpriteContainer.GetSprite(itemConfig.iconName),
                    Description = itemConfig.propertyDesc,
                    PlayerItemType = itemConfig.itemType,
                    IsEquip = bagItemData.State == ItemState.IsEquipped,
                    IsLock = bagItemData.State == ItemState.IsLocked,
                    MaxStack = itemConfig.maxStack,
                    OnUseItem = OnUseItem,
                    OnDropItem = OnDropItem,
                    OnLockItem = OnLockItem,
                    OnEquipItem = OnEquipItem,
                    OnExchangeItem = OnExchangeItem,
                    OnSellItem = OnSellItem,
                };
                if (!_bagItems.TryAdd(item, bagItem))
                {
                    _bagItems[item] = bagItem;
                }
            }
        }
    }
}