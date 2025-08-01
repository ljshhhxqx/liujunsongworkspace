﻿using System;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.Tool.Static;
using Mirror;
using Newtonsoft.Json;
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
        
        public static int GetEquipSkillId(PlayerItemType itemType, int itemConfigId)
        {
            switch (itemType)
            {
                case PlayerItemType.Weapon:
                    return Constant.WeaponConfig.GetWeaponConfigByItemID(itemConfigId).skillID;
                case PlayerItemType.Armor:
                    return Constant.ArmorConfig.GetArmorConfigByItemID(itemConfigId).skillID;
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

        public static IAttributeIncreaseData[] GetAttributeIncreaseDatas(BuffExtraData[] buffExtraData)
        {
            var attributeIncreaseDatas = new IAttributeIncreaseData[buffExtraData.Length];
            for (int i = 0; i < buffExtraData.Length; i++)
            {
                attributeIncreaseDatas[i] = GetAttributeIncreaseData(buffExtraData[i]);
            }
            return attributeIncreaseDatas;
        }

        public static IAttributeIncreaseData GetAttributeIncreaseData(BuffExtraData buffExtraData)
        {
            switch (buffExtraData.buffType)
            {
                case BuffType.Constant:
                    var constantBuff = Constant.ConstantBuffConfig.GetBuff(buffExtraData);
                    var header = new AttributeIncreaseDataHeader();
                    header.buffIncreaseType = constantBuff.mainIncreaseType;
                    header.propertyType = constantBuff.propertyType;
                    header.buffOperationType = BuffOperationType.Add;
                    var data = new AttributeIncreaseData();
                    data.header = header;
                    data.increaseValue = constantBuff.increaseDataList[0].increaseValue;
                    return data;
                case BuffType.Random:
                    var randomBuff = Constant.RandomBuffConfig.GetRandomBuffData(buffExtraData.buffId);
                    var header2 = new AttributeIncreaseDataHeader();
                    header2.buffIncreaseType = randomBuff.mainIncreaseType;
                    header2.propertyType = randomBuff.propertyType;
                    header2.buffOperationType = BuffOperationType.Add;
                    var data2 = new RandomAttributeIncreaseData();
                    data2.header = header2;
                    data2.increaseValueRange = randomBuff.increaseDataList[0].increaseValueRange;
                    return data2;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static BattleEffectConditionConfigData GetBattleEffectConditionConfigData(int equipConfigId,
            EquipmentPart part)
        {
            var battleEffectId = part switch
            {
                EquipmentPart.Weapon => Constant.WeaponConfig.GetWeaponConfigData(equipConfigId).battleEffectConditionId,
                EquipmentPart.Body or EquipmentPart.Head or EquipmentPart.Leg or EquipmentPart.Feet
                    or EquipmentPart.Waist => Constant.ArmorConfig.GetArmorConfigData(equipConfigId).battleEffectConditionId,
                _ => 0
            };
            if (battleEffectId == 0)
            {
                //Debug.LogError($"{nameof(BattleEffectConditionConfigData)} not found");
                return default;
            }
            return Constant.ConditionConfig.GetConditionData(battleEffectId);
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
                if (PlayerItemState.RemoveItem(ref playerItemState, item.SlotIndex, item.Count, out var bagSlotItem, out var removedItemIds))
                {
                    var config = Constant.ItemConfig.GetGameItemData(bagSlotItem.ConfigId);
                    if (config.itemType != PlayerItemType.Weapon && config.itemType != PlayerItemType.Armor)
                    {
                        continue;
                    }
                    var configId = GetEquipmentConfigId(config.itemType, bagSlotItem.ConfigId);
                    if (configId != 0 && Constant.IsServer)
                    {
                        for (int i = 0; i < removedItemIds.Length; i++)
                        {
                            GameItemManager.RemoveGameItemData(removedItemIds[i], Constant.GameSyncManager.netIdentity);
                        }
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
            var droppedItemDatas = new DroppedItemData[itemDropCommand.Slots.Count];
            for (int i = 0; i < itemDropCommand.Slots.Count; i++)
            {
                var item = itemDropCommand.Slots[i];
                if (!PlayerItemState.RemoveItem(ref playerItemState, item.SlotIndex, item.Count, out var bagSlotItem, out var removedItemIds))
                {
                    Debug.LogError($"Failed to remove item {item.SlotIndex}");
                    return;
                }

                droppedItemDatas[i] = new DroppedItemData
                {
                    Count = bagSlotItem.Count,
                    ItemConfigId = bagSlotItem.ConfigId,
                    ItemIds = Constant.IsServer ? removedItemIds : Array.Empty<int>(),
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
                Header = InteractSystem.CreateInteractHeader(connectionId, InteractCategory.PlayerToScene,
                    playerComponent.transform.position),
                InteractionType = InteractionType.DropItem,
                ItemDatas = droppedItemDatas,
            };
            Constant.InteractSystem.EnqueueCommand(request);
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
            if (!PlayerItemState.TryGetPlayerEquipItemByEquipPart(playerItemState, bagItem.EquipmentPart, out var equipSlotItem))
            {
                return;
            }
            Constant.GameSyncManager.EnqueueServerCommand(new EquipmentCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                EquipmentConfigId = configId,
                EquipmentPart = bagItem.EquipmentPart,
                IsEquip = itemEquipCommand.IsEquip,
                ItemId = bagItem.ItemId,
                EquipmentPassiveEffectData = JsonConvert.SerializeObject(equipSlotItem.MainIncreaseDatas),
                EquipmentMainEffectData = JsonConvert.SerializeObject(equipSlotItem.PassiveIncreaseDatas),
            });
            var animationState = SkillConfig.GetAnimationState(bagItem.PlayerItemType);
            Constant.GameSyncManager.EnqueueServerCommand(new SkillLoadCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Skill, CommandAuthority.Server, CommandExecuteType.Immediate),
                SkillConfigId = equipSlotItem.SkillId,
                IsLoad = true,
                KeyCode = animationState  
            });
            Constant.GameSyncManager.EnqueueServerCommand(new SkillChangedCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Input, CommandAuthority.Server, CommandExecuteType.Immediate),
                SkillId = equipSlotItem.SkillId,
                AnimationState = animationState
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
                    if (header.ConnectionId < 0 || !Constant.IsServer)
                    {
                        break;
                    }
                    var buffCommand = new PropertyBuffCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property),
                        CasterId = null,
                        TargetId = header.ConnectionId,
                        BuffSourceType = BuffSourceType.Collect,
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
                PlayerItemState.TryAddAndEquipItem(ref playerItemState, bagItem, out var isEquipped);
                if (!Constant.IsServer)
                    return;
                var gameItemData = new GameItemData
                {
                    ItemId = itemsData.ItemUniqueId[0],
                    ItemConfigId = itemsData.ItemConfigId,
                    ItemType = itemConfigData.itemType,
                    ItemState = ItemState.IsInBag,
                };
                GameItemManager.AddItemData(gameItemData, Constant.GameSyncManager.netIdentity);
                if (isEquipped)
                {
                    var equipmentCommand = new EquipmentCommand();
                    equipmentCommand.Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate);
                    equipmentCommand.EquipmentPart = itemConfigData.equipmentPart;
                    equipmentCommand.IsEquip = true;
                    equipmentCommand.EquipmentConfigId = GetEquipmentConfigId(itemConfigData.itemType, itemsData.ItemConfigId);
                    equipmentCommand.ItemId = itemsData.ItemUniqueId[0];
                    Constant.GameSyncManager.EnqueueServerCommand(equipmentCommand);
                }

                if (bagItem.PlayerItemType.ShowProperty())
                {
                    CheckAndAddBagAttributes(ref playerItemState, itemConfigData);
                }
            }

            catch (Exception e)
            {
                ObjectPool<PlayerBagItem>.Return(bagItem);
                Console.WriteLine(e);
                throw;
            }
        }

        private static void CheckAndAddBagAttributes(ref PlayerItemState playerItemState, GameItemConfigData itemConfigData)
        {
            foreach (var key in playerItemState.PlayerItemConfigIdSlotDictionary.Keys)
            {
                var bagItem = playerItemState.PlayerItemConfigIdSlotDictionary[key];
                var attributeData = GetAttributeIncreaseDatas(itemConfigData.buffExtraData);
                var mainAttributeData = new AttributeIncreaseData[attributeData.Length];
                var passiveAttributeData = new RandomAttributeIncreaseData[attributeData.Length];
                if (bagItem.MainIncreaseDatas == null || bagItem.MainIncreaseDatas.Count == 0)
                {
                    bagItem.MainIncreaseDatas = new MemoryList<AttributeIncreaseData>(mainAttributeData);
                }
                
                if (itemConfigData.itemType == PlayerItemType.Consume && bagItem.RandomIncreaseDatas == null || bagItem.RandomIncreaseDatas.Count == 0)
                {
                    bagItem.RandomIncreaseDatas = new MemoryList<RandomAttributeIncreaseData>(passiveAttributeData);
                }

                if (itemConfigData.itemType.IsEquipment() && bagItem.PassiveAttributeIncreaseDatas == null || bagItem.PassiveAttributeIncreaseDatas.Count == 0)
                {
                    var configId = GetEquipmentConfigId(bagItem.PlayerItemType, bagItem.ConfigId);
                    int battleEffectConfigId;
                    switch (bagItem.PlayerItemType)
                    {
                        case PlayerItemType.Weapon:
                            battleEffectConfigId = Constant.WeaponConfig.GetWeaponConfigData(configId).battleEffectConditionId;
                            break;
                        case PlayerItemType.Armor:
                            battleEffectConfigId = Constant.ArmorConfig.GetArmorConfigData(configId).battleEffectConditionId;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    var conditionConfig = Constant.ConditionConfig.GetConditionData(battleEffectConfigId);
                    var buffIncreaseTypes = Enum.GetValues(typeof(BuffIncreaseType)).Cast<BuffIncreaseType>();
                    var equipmentBuff = Constant.RandomBuffConfig.GetEquipmentBuff(buffIncreaseTypes.RandomSelect());
                    var attribute = Constant.RandomBuffConfig.GetBuff(equipmentBuff, conditionConfig.buffWeight);
                    bagItem.PassiveAttributeIncreaseDatas = new MemoryList<AttributeIncreaseData>(attribute.increaseDataList.Count);
                    for (int i = 0; i < attribute.increaseDataList.Count; i++)
                    {
                        bagItem.PassiveAttributeIncreaseDatas[i] = new AttributeIncreaseData
                        {
                            header = new AttributeIncreaseDataHeader
                            {
                                buffIncreaseType = attribute.increaseDataList[i].increaseType,
                                propertyType = attribute.propertyType,
                                buffOperationType = BuffOperationType.Add
                            },
                            increaseValue = attribute.increaseDataList[i].increaseValue
                        };
                    }
                }
            }
        }

        public static void CommandExchangeItem(ItemExchangeCommand itemExchangeCommand, ref PlayerItemState playerItemState)
        {
            try
            {
                PlayerItemState.SwapItems(ref playerItemState, itemExchangeCommand.FromSlotIndex, itemExchangeCommand.ToSlotIndex);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        public static void CommandUseItems(ItemsUseCommand itemsUseCommand, ref PlayerItemState playerItemState)
        {
            foreach (var index in itemsUseCommand.Slots.Keys)
            {
                var itemData = itemsUseCommand.Slots[index];
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
                        BuffSourceType = BuffSourceType.Consume
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

    public class PlayerItemConstant
    {
        public ItemConfig ItemConfig;
        public WeaponConfig WeaponConfig;
        public ArmorConfig ArmorConfig;
        public PropertyConfig PropertyConfig;
        public BattleEffectConditionConfig ConditionConfig;
        public GameSyncManager GameSyncManager;
        public InteractSystem InteractSystem;
        public ConstantBuffConfig ConstantBuffConfig;
        public RandomBuffConfig RandomBuffConfig;
        public bool IsServer;
        public bool IsClient;
        public bool IsLocalPlayer;
    }
}