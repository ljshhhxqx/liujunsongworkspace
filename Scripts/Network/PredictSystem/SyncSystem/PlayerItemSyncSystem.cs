using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerItemSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerItemSyncState> _playerItemSyncStates = new Dictionary<int, PlayerItemSyncState>();
        private ItemConfig _itemConfig;
        private WeaponConfig _weaponConfig;
        private ArmorConfig _armorConfig;
        private InteractSystem _interactSystem;
        private PlayerInGameManager _playerInGameManager;

        [Inject]
        private void Init(IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            _weaponConfig = configProvider.GetConfig<WeaponConfig>();
            _armorConfig = configProvider.GetConfig<ArmorConfig>();
            _playerInGameManager = playerInGameManager;
            _interactSystem = Object.FindObjectOfType<InteractSystem>();
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
            ModifyPlayerItems(ref items);
            playerItemState.PlayerItems = items.ToDictionary(x => x.ItemId, x => x);
            PropertyStates.Add(connectionId, playerItemState);
            _playerItemSyncStates.Add(connectionId, playerPredictableState);
        }

        private void ModifyPlayerItems(ref List<PlayerBagItem> playerItems)
        {
            playerItems = new List<PlayerBagItem>(8);
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
                    CommandUseItems(itemUseCommand, ref playerItemState);
                    break;
                case ItemEquipCommand itemEquipCommand:
                    CommandEquipItem(itemEquipCommand, ref playerItemState, header.ConnectionId);
                    break;
                case ItemLockCommand itemLockCommand:
                    CommandLockItem(itemLockCommand, ref playerItemState);
                    break;
                case ItemDropCommand itemDropCommand:
                    CommandDropItem(itemDropCommand, ref playerItemState , header.ConnectionId);
                    break;
                case ItemsBuyCommand itemBuyCommand:
                    CommandBuyItem(itemBuyCommand, ref playerItemState);
                    break;
                case ItemsSellCommand itemSellCommand:
                    CommandSellItem(itemSellCommand, ref playerItemState, header.ConnectionId);
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

        private void CommandSellItem(ItemsSellCommand itemSellCommand, ref PlayerItemState playerItemState, int connectionId)
        {
            foreach (var item in itemSellCommand.Slots)
            {
                if (PlayerItemState.RemoveItem(ref playerItemState, item.SlotIndex, item.Count, out var bagSlotItem))
                {
                    var config = _itemConfig.GetGameItemData(bagSlotItem.ConfigId);
                    if (config.itemType != PlayerItemType.Weapon && config.itemType != PlayerItemType.Armor)
                    {
                        continue;
                    }
                    var configId = GetConfigId(config.itemType, bagSlotItem.ConfigId);
                    if (configId != 0)
                    {
                        GameSyncManager.EnqueueServerCommand(new EquipmentCommand
                        {
                            Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                            EquipmentConfigId = configId,
                            EquipmentPart = config.equipmentPart,
                            IsEquip = false,
                            ItemId = bagSlotItem.ItemIds.First(),
                        });
                    }
                }
            }
        }
        
        private void CommandLockItem(ItemLockCommand itemLockCommand, ref PlayerItemState playerItemState)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemLockCommand.SlotIndex, itemLockCommand.IsLocked ? ItemState.IsLocked : ItemState.IsInBag))
            {
                Debug.LogError($"Failed to lock item {itemLockCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemLockCommand.SlotIndex} locked");
        }
        
        private int GetConfigId(PlayerItemType itemType, int itemConfigId)
        {
            switch (itemType)
            {
                case PlayerItemType.Weapon:
                    return _weaponConfig.GetWeaponConfigByItemID(itemConfigId).weaponID;
                case PlayerItemType.Armor:
                    return _armorConfig.GetArmorConfigByItemID(itemConfigId).armorID;
                default:
                    return 0;
            }
        }

        private void CommandEquipItem(ItemEquipCommand itemEquipCommand, ref PlayerItemState playerItemState, int connectionId)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemEquipCommand.SlotIndex, itemEquipCommand.IsEquip ? ItemState.IsEquipped : ItemState.IsInBag))
            {
                Debug.LogError($"Failed to equip item {itemEquipCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemEquipCommand.SlotIndex} equipped");
            if (!PlayerItemState.TryGetEquipItemBySlotIndex(playerItemState, itemEquipCommand.SlotIndex, out var bagItem)
                || bagItem.PlayerItemType == PlayerItemType.None)
            {
                return;
            }

            var configId = GetConfigId(bagItem.PlayerItemType, bagItem.ConfigId);
            GameSyncManager.EnqueueServerCommand(new EquipmentCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                EquipmentConfigId = configId,
                EquipmentPart = bagItem.EquipmentPart,
                IsEquip = itemEquipCommand.IsEquip,
                ItemId = bagItem.ItemId
            });
        }

        private void CommandDropItem(ItemDropCommand itemDropCommand, ref PlayerItemState playerItemState, int connectionId)
        {
            var droppedItemDatas = new DroppedItemData[itemDropCommand.Slots.Length];
            for (int i = 0; i < itemDropCommand.Slots.Length; i++)
            {
                var item = itemDropCommand.Slots[i];
                if (!PlayerItemState.RemoveItem(ref playerItemState, item.SlotIndex, item.Count, out var bagSlotItem))
                {
                    Debug.LogError($"Failed to remove item {item.SlotIndex}");
                    return;
                }

                droppedItemDatas[i] = new DroppedItemData
                {
                    Count = bagSlotItem.Count,
                    ItemConfigId = bagSlotItem.ConfigId,
                };
            }
            var player = _playerInGameManager.GetPlayerNetId(connectionId);
            var playerComponent = NetworkServer.spawned[player];

            var request = new PlayerToSceneRequest
            {
                Header = GameSyncManager.CreateInteractHeader(connectionId, InteractCategory.PlayerToScene,
                    playerComponent.transform.position),
                InteractionType = InteractionType.DropItem,
                ItemDatas = droppedItemDatas,
            };
            _interactSystem.EnqueueServerCommand(request);
        }

        private void CommandUseItems(ItemsUseCommand itemsUseCommand, ref PlayerItemState playerItemState)
        {
            foreach (var itemData in itemsUseCommand.Slots)
            {
                if (!PlayerItemState.TryGetSlotItemBySlotIndex(playerItemState, itemData.SlotIndex, out var bagItem))
                {
                    Debug.LogError($"Failed to use item {itemData.SlotIndex}");
                    return;
                }

                var itemIds = bagItem.ItemIds;
                foreach (var itemId in itemIds)
                {
                    var item = GameItemManager.GetGameItemData(itemId);
                    if (item.ItemId != itemId)
                    {
                        Debug.LogError($"Item id {itemId} not found");
                        return;
                    }

                    if (item.ItemType != PlayerItemType.Consume)
                    {
                        Debug.LogError($"Item {itemId} is not a consume item");
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
                        Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate),
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
            bagItem.IndexSlot = -1;
            bagItem.EquipmentPart = itemConfigData.equipmentPart;
            if (!PlayerItemState.TryAddAndEquipItem(ref playerItemState, bagItem))
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