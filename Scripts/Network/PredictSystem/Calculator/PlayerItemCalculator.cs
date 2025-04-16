using System;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Tool.ObjectPool;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerItemCalculator: IPlayerStateCalculator
    {
        // public Random Random { get; } = new Random();
        public static PlayerItemConstant Constant { get; private set; }
        public static void SetConstant(PlayerItemConstant constant)
        {
            Constant = constant;
        }
        public static int GetEquipmentConfigId(PlayerItemType itemType, int itemConfigId)
        {
            switch (itemType)
            {
                case PlayerItemType.Weapon:
                    return Constant.WeaponConfig.GetWeaponConfigByItemID(itemConfigId).weaponID;
                case PlayerItemType.Armor:
                    return Constant.ArmorConfig.GetArmorConfigByItemID(itemConfigId).armorID;
                default:
                    return 0;
            }
        }

        public static int GetItemConfigId(EquipmentPart part, int equipmentConfigId)
        {
            switch (part)
            {
                case EquipmentPart.Weapon:
                    return Constant.WeaponConfig.GetWeaponConfigData(equipmentConfigId).itemID;
                case EquipmentPart.Body:
                case EquipmentPart.Head:
                case EquipmentPart.Leg:
                case EquipmentPart.Feet:
                case EquipmentPart.Waist:
                    return Constant.ArmorConfig.GetArmorConfigData(equipmentConfigId).itemID;
                default:
                    return 0;
            }
        }

        public static ConditionCheckerHeader GetConditionCheckerHeader(PlayerItemType itemType, int itemConfigId)
        {
            var conditionConfigId = 0;
            switch (itemType)
            {
                case PlayerItemType.Weapon:
                    conditionConfigId = Constant.WeaponConfig.GetWeaponConfigByItemID(itemConfigId).battleEffectConditionId;
                    break;
                case PlayerItemType.Armor:
                    conditionConfigId = Constant.ArmorConfig.GetArmorConfigByItemID(itemConfigId).battleEffectConditionId;
                    break;
            }
            var config = Constant.ConditionConfig.GetConditionData(conditionConfigId);

            var header = ConditionCheckerHeader.Create(config.triggerType, config.interval, config.probability,
                config.conditionParam, config.targetType, config.targetCount);
            return header;
        }
        
        public static void CommandBuyItem(ItemsBuyCommand itemBuyCommand, ref PlayerItemState playerItemState)
        {
            foreach (var item in itemBuyCommand.Items)
            {
                CommandGetItem(ref playerItemState, item);
            }
        }
        
        public static void CommandSellItem(ItemsSellCommand itemSellCommand, ref PlayerItemState playerItemState, int connectionId)
        {
            foreach (var item in itemSellCommand.Slots)
            {
                if (PlayerItemState.RemoveItem(ref playerItemState, item.SlotIndex, item.Count, out var bagSlotItem))
                {
                    var config = Constant.ItemConfig.GetGameItemData(bagSlotItem.ConfigId);
                    if (config.itemType != PlayerItemType.Weapon && config.itemType != PlayerItemType.Armor)
                    {
                        continue;
                    }
                    var configId = GetEquipmentConfigId(config.itemType, bagSlotItem.ConfigId);
                    if (configId != 0 && Constant.IsServer)
                    {
                        Constant.GameSyncManager.EnqueueServerCommand(new EquipmentCommand
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
        
        public static void CommandDropItem(ItemDropCommand itemDropCommand, ref PlayerItemState playerItemState, int connectionId)
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
            if (!Constant.IsServer)
            {
                return;
            }
            var player = NetworkServer.connections[connectionId].identity.netId;
            var playerComponent = NetworkServer.spawned[player];

            var request = new PlayerToSceneRequest
            {
                Header = GameSyncManager.CreateInteractHeader(connectionId, InteractCategory.PlayerToScene,
                    playerComponent.transform.position),
                InteractionType = InteractionType.DropItem,
                ItemDatas = droppedItemDatas,
            };
            Constant.InteractSystem.EnqueueServerCommand(request);
        }
        
        public static void CommandLockItem(ItemLockCommand itemLockCommand, ref PlayerItemState playerItemState)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemLockCommand.SlotIndex, itemLockCommand.IsLocked ? ItemState.IsLocked : ItemState.IsInBag))
            {
                Debug.LogError($"Failed to lock item {itemLockCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemLockCommand.SlotIndex} locked");
        }
        public static void CommandEquipItem(ItemEquipCommand itemEquipCommand, ref PlayerItemState playerItemState, int connectionId)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemEquipCommand.SlotIndex, itemEquipCommand.IsEquip ? ItemState.IsEquipped : ItemState.IsInBag))
            {
                Debug.LogError($"Failed to equip item {itemEquipCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemEquipCommand.SlotIndex} equipped");
            if (!PlayerItemState.TryGetEquipItemBySlotIndex(playerItemState, itemEquipCommand.SlotIndex, out var bagItem)
                || bagItem.PlayerItemType == PlayerItemType.None || !Constant.IsServer)
            {
                return;
            }

            var configId = GetEquipmentConfigId(bagItem.PlayerItemType, bagItem.ConfigId);
            Constant.GameSyncManager.EnqueueServerCommand(new EquipmentCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                EquipmentConfigId = configId,
                EquipmentPart = bagItem.EquipmentPart,
                IsEquip = itemEquipCommand.IsEquip,
                ItemId = bagItem.ItemId
            });
        }
        public static void CommandGetItem(ref PlayerItemState playerItemState, ItemsCommandData itemsData, NetworkCommandHeader header = default)
        {
            var itemConfigData = Constant.ItemConfig.GetGameItemData(itemsData.ItemConfigId);
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
                    AddPlayerItems(itemsData, header, ref playerItemState);
                    break;
                case PlayerItemType.Collect:
                    if (header.ConnectionId == 0 || !Constant.IsServer)
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
                        Constant.GameSyncManager.EnqueueServerCommand(buffCommand);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        
        public static void AddPlayerItems(ItemsCommandData itemsData, NetworkCommandHeader header,
            ref PlayerItemState playerItemState)
        {
            var itemConfigData = Constant.ItemConfig.GetGameItemData(itemsData.ItemConfigId);
            var bagItem = ObjectPool<PlayerBagItem>.Get();
            try
            {
                bagItem = default;
                bagItem.ItemId = itemsData.ItemUniqueId[0];
                bagItem.ConfigId = itemsData.ItemConfigId;
                bagItem.PlayerItemType = itemConfigData.itemType;
                bagItem.State = ItemState.IsInBag;
                bagItem.MaxStack = itemConfigData.maxStack;
                bagItem.IndexSlot = -1;
                bagItem.EquipmentPart = itemConfigData.equipmentPart;
                PlayerItemState.TryAddAndEquipItem(ref playerItemState, bagItem);
            }

            catch (Exception e)
            {
                ObjectPool<PlayerBagItem>.Return(bagItem);
                Console.WriteLine(e);
                throw;
            }
        }

        public static void CommandUseItems(ItemsUseCommand itemsUseCommand, ref PlayerItemState playerItemState)
        {
            foreach (var itemData in itemsUseCommand.Slots)
            {
                if (!PlayerItemState.TryGetSlotItemBySlotIndex(playerItemState, itemData.SlotIndex, out var bagItem))
                {
                    Debug.LogError($"Failed to use item {itemData.SlotIndex}");
                    return;
                }

                if (!Constant.IsServer)
                {
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
                    var itemConfigData = Constant.ItemConfig.GetGameItemData(item.ItemConfigId);
                    if (itemConfigData.id == 0)
                    {
                        Debug.LogError($"Item config id {item.ItemId} not found");
                        return;
                    }

                    var buffCommand = new PropertyBuffCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                            CommandAuthority.Server, CommandExecuteType.Immediate),
                        CasterId = null,
                        TargetId = header.ConnectionId,
                    };
                    foreach (var buffExtra in itemConfigData.buffExtraData)
                    {
                        buffCommand.BuffExtraData = buffExtra;
                        Constant.GameSyncManager.EnqueueServerCommand(buffCommand); 
                    }
                }
            }
        }
    }
    

    public class PlayerItemComponent
    {
    }

    public struct PlayerItemConstant
    {
        public ItemConfig ItemConfig;
        public WeaponConfig WeaponConfig;
        public ArmorConfig ArmorConfig;
        public PropertyConfig PropertyConfig;
        public BattleEffectConditionConfig ConditionConfig;
        public GameSyncManager GameSyncManager;
        public InteractSystem InteractSystem;
        public bool IsServer;
    }
}