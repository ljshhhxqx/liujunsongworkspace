using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerItemSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerItemSyncState> _playerItemSyncStates = new Dictionary<int, PlayerItemSyncState>();
        private ItemConfig _itemConfig;

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _itemConfig = configProvider.GetConfig<ItemConfig>();
        }

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerItemState>>(state);
            foreach (var playerState in playerStates)
            {
                if (!PropertyStates.ContainsKey(playerState.Key))
                {
                    continue;
                }
                PropertyStates[playerState.Key] = playerState.Value;
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerItemSyncState>();
            var playerItemState = new PlayerItemState();
            var items = ObjectPool<List<PlayerBagItem>>.Get();
            ModifyPlayerItems(items);
            playerItemState.PlayerItems = items.ToDictionary(x => x.ItemId, x => x);
            PropertyStates.Add(connectionId, playerItemState);
            _playerItemSyncStates.Add(connectionId, playerPredictableState);
        }

        private void ModifyPlayerItems(List<PlayerBagItem> playerItems)
        {
            
        }

        public override CommandType HandledCommandType => CommandType.Item;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            var itemState = PropertyStates[header.ConnectionId];
            if (itemState is not PlayerItemState playerItemState)
            {
                Debug.LogError("PlayerItemState not found");
                return null;
            }
            switch (command)
            {
                case ItemsGetCommand itemsGetCommand:
                    for (var i = 0; i < itemsGetCommand.Items.Length; i++)
                    {
                        CommandGetItem(ref playerItemState, itemsGetCommand.Items[i], header);
                    }
                    break;
                case ItemsUseCommand itemUseCommand:
                    CommandUseItems(itemUseCommand);
                    break;
                case ItemEquipCommand itemEquipCommand:
                    CommandEquipItem(itemEquipCommand, ref playerItemState);
                    break;
                case ItemLockCommand itemLockCommand:
                    CommandLockItem(itemLockCommand, ref playerItemState);
                    break;
                case ItemDropCommand itemDropCommand:
                    CommandDropItem(itemDropCommand, ref playerItemState);
                    break;
                case ItemsBuyCommand itemBuyCommand:
                    CommandBuyItem(itemBuyCommand, ref playerItemState);
                    break;
                case ItemsSellCommand itemSellCommand:
                    CommandSellItem(itemSellCommand, ref playerItemState);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            PropertyStates[header.ConnectionId] = playerItemState;
            return playerItemState;
        }
        
        private void CommandBuyItem(ItemsBuyCommand itemBuyCommand, ref PlayerItemState playerItemState)
        {
            foreach (var item in itemBuyCommand.Items)
            {
                CommandGetItem(ref playerItemState, item);
            }
        }

        private void CommandSellItem(ItemsSellCommand itemSellCommand, ref PlayerItemState playerItemState)
        {
            
        }
        
        private void CommandLockItem(ItemLockCommand itemLockCommand, ref PlayerItemState playerItemState)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemLockCommand.SlotIndex, ItemState.IsLocked))
            {
                Debug.LogError($"Failed to lock item {itemLockCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemLockCommand.SlotIndex} locked");
        }

        private void CommandEquipItem(ItemEquipCommand itemEquipCommand, ref PlayerItemState playerItemState)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemEquipCommand.SlotIndex, ItemState.IsEquipped))
            {
                Debug.LogError($"Failed to equip item {itemEquipCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemEquipCommand.SlotIndex} equipped");
            GameSyncManager.EnqueueServerCommand(new EquipmentCommand
            {
                
            });
        }

        private void CommandDropItem(ItemDropCommand itemDropCommand, ref PlayerItemState playerItemState)
        {
            
        }

        private void CommandUseItems(ItemsUseCommand itemsUseCommand)
        {
            foreach (var itemData in itemsUseCommand.Items)
            {
                foreach (var itemId in itemData.ItemUniqueId)
                {
                    var item = GameItemManager.GetGameItemData(itemId);
                    if (item.ItemId != itemId)
                    {
                        Debug.LogError($"Item id {itemId} not found");
                        return;
                    }
                    var header = itemsUseCommand.Header;
                    var itemConfigData = _itemConfig.GetGameItemData(item.ItemConfigId);
                    if (itemConfigData.id == 0)
                    {
                        Debug.LogError($"Item config id {item.ItemId} not found");
                        return;
                    }
            
                    var buffCommand = new PropertyBuffCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property),
                        CasterId = null,
                        TargetId = header.ConnectionId,
                    };
                    foreach (var buffExtra in itemConfigData.buffExtraData)
                    {
                        buffCommand.BuffExtraData = buffExtra;
                        GameSyncManager.EnqueueServerCommand(buffCommand);
                    }
                }
            }
        }

        private void CommandGetItem(ref PlayerItemState playerItemState, ItemsCommandData itemsData, NetworkCommandHeader header = default)
        {
            var itemConfigData = _itemConfig.GetGameItemData(itemsData.ItemConfigId);
            if (itemConfigData.id == 0)
            {
                Debug.LogError($"Item config id {itemsData.ItemConfigId} not found");
                return;
            }

            switch (itemConfigData.itemType)
            {
                case PlayerItemType.Weapon:
                case PlayerItemType.Armor:
                    break;
                case PlayerItemType.Consume:
                case PlayerItemType.Item:
                    //todo: 操作玩家背包
                    AddPlayerItems(itemsData, header, ref playerItemState);
                    break;
                case PlayerItemType.Collect:
                    if (header.ConnectionId == 0)
                    {
                        break;
                    }
                    var buffCommand = new PropertyBuffCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property),
                        CasterId = null,
                        TargetId = header.ConnectionId,
                    };
                    foreach (var buffExtra in itemConfigData.buffExtraData)
                    {
                        buffCommand.BuffExtraData = buffExtra;
                        GameSyncManager.EnqueueServerCommand(buffCommand);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void AddPlayerItems(ItemsCommandData itemsData, NetworkCommandHeader header,
            ref PlayerItemState playerItemState)
        {
            var itemConfigData = _itemConfig.GetGameItemData(itemsData.ItemConfigId);

            var bagItem = ObjectPool<PlayerBagItem>.Get();
            bagItem.ItemId = itemsData.ItemUniqueId[0];
            bagItem.ConfigId = itemsData.ItemConfigId;
            bagItem.PlayerItemType = itemConfigData.itemType;
            bagItem.State = ItemState.IsInBag;
            bagItem.MaxStack = itemConfigData.maxStack;
            if (!PlayerItemState.AddItem(ref playerItemState, bagItem))
            {
                Debug.LogError($"Failed to add item {bagItem.ItemId}");
                ObjectPool<PlayerBagItem>.Return(bagItem);
            }
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _playerItemSyncStates[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _playerItemSyncStates.Clear();
        }

        public static int CreateItemId(int configId)
        {
            return HybridIdGenerator.GenerateItemId(configId, SyncSystem.GameSyncManager.CurrentTick);
        }
        
        public static int CreateChestId(int configId)
        {
            return HybridIdGenerator.GenerateChestId(configId, SyncSystem.GameSyncManager.CurrentTick);
        }
    }
}