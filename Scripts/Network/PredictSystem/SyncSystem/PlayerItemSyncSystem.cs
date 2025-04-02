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
                case ItemGetCommand itemGetCommand:
                    for (var i = 0; i < itemGetCommand.Item.Count; i++)
                    {
                        CommandGetItem(itemGetCommand.Item, header, ref playerItemState);
                    }
                    break;
                case ItemsGetCommand itemsGetCommand:
                    for (var i = 0; i < itemsGetCommand.Items.Length; i++)
                    {
                        CommandGetItem(itemsGetCommand.Items[i], header, ref playerItemState);
                    }
                    break;
                case ItemUseCommand itemUseCommand:
                    CommandUseItem(itemUseCommand);
                    break;
                case ItemEquipCommand itemEquipCommand:
                    break;
                case ItemLockCommand itemLockCommand:
                    break;
                case ItemDropCommand itemDropCommand:
                    break;
            }
            PropertyStates[header.ConnectionId] = playerItemState;
            return playerItemState;
        }
        
        private void CommandLockItem(ItemLockCommand itemLockCommand, ref PlayerItemState playerItemState)
        {
            
        }

        private void CommandEquipItem(ItemEquipCommand itemEquipCommand, ref PlayerItemState playerItemState)
        {
            
        }

        private void CommandDropItem(ItemDropCommand itemDropCommand, ref PlayerItemState playerItemState)
        {
            
        }

        private void CommandUseItem(ItemUseCommand itemUseCommand)
        {
            var item = GameItemManager.GetGameItemData(itemUseCommand.ItemId);
            if (item.ItemId != itemUseCommand.ItemId)
            {
                Debug.LogError($"Item id {itemUseCommand.ItemId} not found");
                return;
            }
            var header = itemUseCommand.Header;
            var itemConfigData = _itemConfig.GetGameItemData(item.ItemId);
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

        private void CommandGetItem(ItemCommandData itemData, NetworkCommandHeader header,
            ref PlayerItemState playerItemState)
        {
            var itemConfigData = _itemConfig.GetGameItemData(itemData.ItemConfigId);
            if (itemConfigData.id == 0)
            {
                Debug.LogError($"Item config id {itemData.ItemConfigId} not found");
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
                    ModifyPlayerItems(itemData, header, ref playerItemState);
                    // var playerItems = playerItemState.PlayerItems;
                    // if (playerItems.ContainsKey(itemData.ItemUniqueId))
                    // {
                    //     var itemState = playerItems[itemData.ItemUniqueId];
                    //     itemState.ItemCount += itemData.Count;
                    //     playerItems[itemData.ItemUniqueId] = itemState;
                    // }
                    // else
                    // {
                    //     var playerItem = new PlayerBagItem
                    //     {
                    //         ItemId = itemData.ItemUniqueId,
                    //         ConfigId = itemData.ItemConfigId,
                    //         PlayerItemType = itemConfigData.itemType,
                    //         State = ItemState.IsInBag,
                    //         IndexSlot = 1,
                    //     };
                    //     playerItems.Add(itemData.ItemUniqueId, playerItem);
                    // }
                    //playerItemState.PlayerItems = playerItems;
                    break;
                case PlayerItemType.Collect:
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

        private void ModifyPlayerItems(ItemCommandData itemData, NetworkCommandHeader header,
            ref PlayerItemState playerItemState)
        {
            
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