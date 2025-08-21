using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
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

        public static string GetSkillDescription(int skillConfigId, int connectionId,bool includeName = true)
        {
            if (skillConfigId == 0)
            {
                return string.Empty;
            }
            var skillConfigData = Constant.SkillConfig.GetSkillData(skillConfigId);
            if (skillConfigData.id == 0)
            {
                Debug.LogError($"skillConfigId {skillConfigId} not found");
                return string.Empty;
            }
            var propertySystem = Constant.GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            var buffProperty = propertySystem.GetPlayerProperty(connectionId);
            var skillDescription = skillConfigData.description;
            for (int i = 0; i < skillConfigData.extraEffects.Length; i++)
            {
                var extraEffect = skillConfigData.extraEffects[i];
                var value = extraEffect.baseValue;
                if (!buffProperty.TryGetValue(extraEffect.buffProperty, out var buffedProperty))
                {
                    //Debug.LogError($"{extraEffect.buffProperty} not found in buffProperty in skill {skillConfigId}");
                    continue;
                }
                switch (extraEffect.buffIncreaseType)
                {
                    case BuffIncreaseType.Current:
                        value += buffedProperty.CurrentValue * extraEffect.extraRatio;
                        break;
                    case BuffIncreaseType.Max:
                        value += buffedProperty.MaxValue * extraEffect.extraRatio;
                        break;
                }
                skillDescription = skillDescription.Replace("{value}", value.ToString("F0"));
            }
            return includeName ? skillConfigData.name + ":" + skillDescription : skillDescription;
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
                    if (constantBuff.increaseDataList == null || constantBuff.increaseDataList.Count == 0)
                    {
                        Debug.LogError($"Constant buff {buffExtraData.buffId} increase data list is null or empty");
                        return data;
                    }
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
                    if (randomBuff.increaseDataList == null || randomBuff.increaseDataList.Count == 0)
                    {
                        Debug.LogError($"randomBuff buff {buffExtraData.buffId}  increase data list is null or empty");
                        return data2;
                    }
                    data2.increaseValueRange = randomBuff.increaseDataList[0].increaseValueRange;
                    return data2;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static BattleEffectConditionConfigData GetBattleEffectConditionConfigData(int itemConfigId)
        {
            var itemConfigData = Constant.ItemConfig.GetGameItemData(itemConfigId);
            var equipId = GetEquipmentConfigId(itemConfigData.itemType, itemConfigId);
            return GetBattleEffectConditionConfigData(equipId, itemConfigData.equipmentPart);
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

            if (conditionConfigId == 0)
            {
                return default;
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
                    var skillId = GetEquipSkillId(config.itemType, bagSlotItem.ConfigId);
                    if (configId != 0 && Constant.IsServer)
                    {
                        for (int i = 0; i < removedItemIds.Length; i++)
                        {
                            GameItemManager.RemoveGameItemData(removedItemIds[i], Constant.GameSyncManager.netIdentity);
                        }

                        if (skillId != 0)
                        {
                            
                            var skillLoadCommand = new SkillLoadCommand
                            {
                                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Skill,
                                    CommandAuthority.Server, CommandExecuteType.Immediate),
                                SkillConfigId = skillId,
                                IsLoad = false,
                                KeyCode = SkillConfig.GetAnimationState(bagSlotItem.PlayerItemType),
                            };
                            Constant.GameSyncManager.EnqueueServerCommand(skillLoadCommand);
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
        }
        
        public static void CommandDropItem(ItemDropCommand itemDropCommand, ref PlayerItemState playerItemState, int connectionId)
        {
            var droppedItemDatas = new List<DroppedItemData>(itemDropCommand.Slots.Count);
            foreach (var kvp in itemDropCommand.Slots)
            {
                var item = kvp.Value;
                if (!PlayerItemState.RemoveItem(ref playerItemState, item.SlotIndex, item.Count, out var bagSlotItem, out var removedItemIds))
                {
                    Debug.LogError($"Failed to remove item {item.SlotIndex}");
                    return;
                }

                droppedItemDatas.Add(new DroppedItemData
                {
                    Count = bagSlotItem.Count,
                    ItemConfigId = bagSlotItem.ConfigId,
                    ItemIds = Constant.IsServer ? removedItemIds : Array.Empty<int>(),
                }); 
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
                ItemDatas = droppedItemDatas.ToArray(),
            };
            Constant.InteractSystem.EnqueueCommand(request);
        }
        
        public static void CommandLockItem(ItemLockCommand itemLockCommand, ref PlayerItemState playerItemState)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemLockCommand.SlotIndex, itemLockCommand.IsLocked ? ItemState.IsLocked : ItemState.IsInBag, out _))
            {
                Debug.LogError($"Failed to lock item {itemLockCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemLockCommand.SlotIndex} locked");
        }
        public static void CommandEquipItem(ItemEquipCommand itemEquipCommand, ref PlayerItemState playerItemState, int connectionId)
        {
            if (!PlayerItemState.UpdateItemState(ref playerItemState, itemEquipCommand.SlotIndex, itemEquipCommand.IsEquip ? ItemState.IsEquipped : ItemState.IsInBag, out var exchangedItem))
            {
                Debug.LogError($"Failed to equip item {itemEquipCommand.SlotIndex}");
                return;
            }
            Debug.Log($"Item {itemEquipCommand.SlotIndex} equipped");
            if (!PlayerItemState.TryGetSlotItemBySlotIndex(playerItemState, itemEquipCommand.SlotIndex, out var bagItem)
                || bagItem.PlayerItemType == PlayerItemType.None || !Constant.IsServer)
            {
                return;
            }

            var configId = GetEquipmentConfigId(bagItem.PlayerItemType, bagItem.ConfigId);
            var skillId = GetEquipSkillId(bagItem.PlayerItemType, bagItem.ConfigId);
            if (exchangedItem!= null)
            {
                var exchangedConfigId = GetEquipmentConfigId(exchangedItem.PlayerItemType, exchangedItem.ConfigId);
                Constant.GameSyncManager.EnqueueServerCommand(new EquipmentCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                    EquipmentConfigId = exchangedConfigId,
                    EquipmentPart = exchangedItem.EquipmentPart,
                    IsEquip = false,
                    ItemId = exchangedItem.ItemIds.First(),
                    EquipmentPassiveEffectData = JsonConvert.SerializeObject(exchangedItem.MainIncreaseDatas),
                    EquipmentMainEffectData = JsonConvert.SerializeObject(exchangedItem.PassiveAttributeIncreaseDatas),
                });
                
                var skillLoadCommand = new SkillLoadCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Skill,
                        CommandAuthority.Server, CommandExecuteType.Immediate),
                    SkillConfigId = exchangedItem.SkillId,
                    IsLoad = false,
                    KeyCode = SkillConfig.GetAnimationState(exchangedItem.PlayerItemType),
                };
                Constant.GameSyncManager.EnqueueServerCommand(skillLoadCommand);
            }
            Constant.GameSyncManager.EnqueueServerCommand(new EquipmentCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                EquipmentConfigId = configId,
                EquipmentPart = bagItem.EquipmentPart,
                IsEquip = itemEquipCommand.IsEquip,
                ItemId = bagItem.ItemIds.First(),
                EquipmentPassiveEffectData = JsonConvert.SerializeObject(bagItem.MainIncreaseDatas),
                EquipmentMainEffectData = JsonConvert.SerializeObject(bagItem.PassiveAttributeIncreaseDatas),
            });
            //var animationState = SkillConfig.GetAnimationState(bagItem.PlayerItemType);
            // if (bagItem.SkillId != 0 && animationState != AnimationState.None)
            // {
            //     Constant.GameSyncManager.EnqueueServerCommand(new SkillLoadCommand
            //     {
            //         Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Skill, CommandAuthority.Server, CommandExecuteType.Immediate),
            //         SkillConfigId = bagItem.SkillId,
            //         IsLoad = true,
            //         KeyCode = animationState  
            //     });
            //     Constant.GameSyncManager.EnqueueServerCommand(new SkillChangedCommand
            //     {
            //         Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Input, CommandAuthority.Server, CommandExecuteType.Immediate),
            //         SkillId = bagItem.SkillId,
            //         AnimationState = animationState
            //     });
            // }
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
                case PlayerItemType.Gold:
                case PlayerItemType.Score:
                    if (header.ConnectionId < 0 || !Constant.IsServer)
                    {
                        break;
                    }

                    var getScoreGoldCommand = new PropertyGetScoreGoldCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property),
                        Gold = itemsData.ItemType == PlayerItemType.Gold ? itemsData.Count : 0,
                        Score = itemsData.ItemType == PlayerItemType.Score ? itemsData.Count : 0,
                    };
                    Constant.GameSyncManager.EnqueueServerCommand(getScoreGoldCommand);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public static void CommandEnablePlayerSkill(ref PlayerItemState playerItemState, int skillId, int slotIndex, bool isEnable, int connectionId)
        {
            if (!Constant.IsServer)
                return;
            if (!PlayerItemState.TryGetSlotItemBySlotIndex(playerItemState, slotIndex, out var bagItem))
            {
                Debug.LogError($"Failed to enable skill {skillId}, no slotIndex {slotIndex}");
                return;
            }

            if (TryUnloadSkill(ref playerItemState, connectionId, slotIndex, out var unloadedItem))
            {
                Debug.Log($"Skill {skillId} unloaded from slot {slotIndex}");
                
            }

            if (bagItem.SkillId != skillId)
            {
                Debug.LogError($"Slot {slotIndex} is skill id is {bagItem.SkillId}, not {slotIndex}");
                return;
            }

            var skillConfig = Constant.SkillConfig.GetSkillData(skillId);
            bagItem.IsEnableSkill = isEnable;
            playerItemState.PlayerItemConfigIdSlotDictionary[slotIndex] = bagItem;
            var skillEnableCommand = new SkillLoadCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Skill, CommandAuthority.Server, CommandExecuteType.Immediate),
                SkillConfigId = skillId,
                IsLoad = isEnable,
                KeyCode = skillConfig.animationState
            };
            Constant.GameSyncManager.EnqueueServerCommand(skillEnableCommand);
        }

        public static bool TryUnloadSkill(ref PlayerItemState playerItemState, int connectionId, int slotIndex, out PlayerBagSlotItem unloadedItem)
        {
            unloadedItem = null;
            if (!PlayerItemState.TryGetSlotItemBySlotIndex(playerItemState, slotIndex, out var bagItem))
            {
                Debug.LogError($"Failed to unload skill, no slotIndex {slotIndex}");
                return false;
            }
            var loadedSkillItem = playerItemState.PlayerItemConfigIdSlotDictionary.FirstOrDefault(x => x.Value.IsEnableSkill && x.Value.PlayerItemType == bagItem.PlayerItemType);
            if (loadedSkillItem.Value!=null)
            {
                unloadedItem = loadedSkillItem.Value;
                unloadedItem.IsEnableSkill = false;
                var skillEnableCommand = new SkillLoadCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                    SkillConfigId = unloadedItem.SkillId,
                    IsLoad = false,
                    KeyCode = SkillConfig.GetAnimationState(unloadedItem.PlayerItemType)
                };
                Constant.GameSyncManager.EnqueueServerCommand(skillEnableCommand);
                playerItemState.PlayerItemConfigIdSlotDictionary[unloadedItem.IndexSlot] = unloadedItem;
                return true;
            }
            return false;
        }

        public static void AddPlayerItems(ItemsCommandData itemsData, NetworkCommandHeader header,
            ref PlayerItemState playerItemState)
        {
            if (!Constant.IsServer)
                return;
            var itemConfigData = Constant.ItemConfig.GetGameItemData(itemsData.ItemConfigId);
            var skillId = GetEquipSkillId(itemConfigData.itemType, itemsData.ItemConfigId);
            var bagItem = new PlayerBagItem();
            try
            {
                bagItem.ItemId = itemsData.ItemUniqueId[0];
                bagItem.ConfigId = itemsData.ItemConfigId;
                bagItem.PlayerItemType = itemConfigData.itemType;
                bagItem.State = ItemState.IsInBag;
                bagItem.MaxStack = itemConfigData.maxStack;
                bagItem.IndexSlot = -1;
                bagItem.EquipmentPart = itemConfigData.equipmentPart;
                bagItem.SkillId = skillId;
                PlayerItemState.TryAddAndEquipItem(ref playerItemState, ref bagItem, out var isEquipped, out var indexSlot);
                var gameItemData = new GameItemData
                {
                    ItemId = itemsData.ItemUniqueId[0],
                    ItemConfigId = itemsData.ItemConfigId,
                    ItemType = itemConfigData.itemType,
                    ItemState = ItemState.IsInBag,
                };
                GameItemManager.AddItemData(gameItemData, Constant.GameSyncManager.netIdentity);

                if (bagItem.PlayerItemType.ShowProperty())
                {
                    CheckAndAddBagAttributes(ref playerItemState, itemConfigData);
                }
                
                if (isEquipped)
                {
                    Debug.Log($"[AddPlayerItems] Item {bagItem.ConfigId} {itemConfigData.equipmentPart} equipped in slot {indexSlot}");
                    if (!playerItemState.PlayerItemConfigIdSlotDictionary.TryGetValue(indexSlot, out var equipSlotItem))
                    {
                        Debug.LogError($"Failed to find equip slot item {indexSlot}");
                        return;
                    }
                    var equipmentCommand = new EquipmentCommand();
                    equipmentCommand.Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate);
                    equipmentCommand.EquipmentPart = itemConfigData.equipmentPart;
                    equipmentCommand.IsEquip = true;
                    equipmentCommand.EquipmentConfigId = GetEquipmentConfigId(itemConfigData.itemType, itemsData.ItemConfigId);
                    equipmentCommand.ItemId = itemsData.ItemUniqueId[0];
                    equipmentCommand.EquipmentPassiveEffectData = JsonConvert.SerializeObject(equipSlotItem.PassiveAttributeIncreaseDatas);
                    equipmentCommand.EquipmentMainEffectData = JsonConvert.SerializeObject(equipSlotItem.MainIncreaseDatas);
                    Constant.GameSyncManager.EnqueueServerCommand(equipmentCommand);
                }
            }

            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        private static bool NeedCheck(PlayerBagSlotItem playerBagSlotItem, GameItemConfigData itemConfigData)
        {
            if (playerBagSlotItem.PlayerItemType == PlayerItemType.Consume)
            {
                if (itemConfigData.buffExtraData.All(b => b.buffType == BuffType.Random) && (playerBagSlotItem.RandomIncreaseDatas == null || playerBagSlotItem.RandomIncreaseDatas.Count == 0))
                {
                    return true;
                }

                if (itemConfigData.buffExtraData.All(b => b.buffType == BuffType.Constant) && (playerBagSlotItem.MainIncreaseDatas == null || playerBagSlotItem.MainIncreaseDatas.Count == 0))
                {
                    return true;
                }
            }

            if (playerBagSlotItem.PlayerItemType.IsEquipment())
            {
                if (playerBagSlotItem.MainIncreaseDatas == null || playerBagSlotItem.MainIncreaseDatas.Count == 0)
                {
                    return true;
                }
            }
            return false;
        }

        private static void CheckAndAddBagAttributes(ref PlayerItemState playerItemState, GameItemConfigData itemConfigData)
        {
            foreach (var kvp in playerItemState.PlayerItemConfigIdSlotDictionary)
            {
                var bagItem = playerItemState.PlayerItemConfigIdSlotDictionary[kvp.Key];
                if (!NeedCheck(bagItem, itemConfigData))
                {
                    continue;
                }
                var attributeData = GetAttributeIncreaseDatas(itemConfigData.buffExtraData);
                if (itemConfigData.itemType.IsEquipment() && (bagItem.MainIncreaseDatas == null || bagItem.MainIncreaseDatas.Count == 0))
                {
                    var mainIncreaseDatas = new MemoryList<AttributeIncreaseData>(attributeData.Length);
                    for (int i = 0; i < attributeData.Length; i++)
                    {
                        var attribute = attributeData[i];
                        if (attribute is AttributeIncreaseData attributeIncreaseData)
                        {
                            mainIncreaseDatas.Add(attributeIncreaseData);
                        }
                    }
                    bagItem.MainIncreaseDatas = mainIncreaseDatas;
                    var configId = GetEquipmentConfigId(bagItem.PlayerItemType, bagItem.ConfigId);
                    int battleEffectConfigId = 0;
                    switch (bagItem.PlayerItemType)
                    {
                        case PlayerItemType.Weapon:
                            battleEffectConfigId = Constant.WeaponConfig.GetWeaponConfigData(configId).battleEffectConditionId;
                            break;
                        case PlayerItemType.Armor:
                            battleEffectConfigId = Constant.ArmorConfig.GetArmorConfigData(configId).battleEffectConditionId;
                            break;
                    }
                    var conditionConfig = Constant.ConditionConfig.GetConditionData(battleEffectConfigId);
                    var buffIncreaseTypes = Enum.GetValues(typeof(BuffIncreaseType)).Cast<BuffIncreaseType>().ToArray();
                    var increaseType = buffIncreaseTypes.RandomSelect();
                    while (increaseType is BuffIncreaseType.None or BuffIncreaseType.Current or BuffIncreaseType.Max)
                    {
                        increaseType = buffIncreaseTypes.RandomSelect();
                    }
                    var equipmentBuff = Constant.RandomBuffConfig.GetEquipmentBuff(increaseType);
                    var passiveAttribute = Constant.RandomBuffConfig.GetBuff(equipmentBuff, conditionConfig.buffWeight);
                    var passiveAttributeIncreaseDatas = new MemoryList<AttributeIncreaseData>(passiveAttribute.increaseDataList.Count);
                    bagItem.PassiveAttributeIncreaseDatas = new MemoryList<AttributeIncreaseData>(passiveAttribute.increaseDataList.Count);
                    for (int i = 0; i < passiveAttribute.increaseDataList.Count; i++)
                    {
                        passiveAttributeIncreaseDatas.Add(new AttributeIncreaseData
                        {
                            header = new AttributeIncreaseDataHeader
                            {
                                buffIncreaseType = passiveAttribute.increaseDataList[i].increaseType,
                                propertyType = passiveAttribute.propertyType,
                                buffOperationType = BuffOperationType.Add
                            },
                            increaseValue = passiveAttribute.increaseDataList[i].increaseValue
                        });
                    }
                    bagItem.PassiveAttributeIncreaseDatas = passiveAttributeIncreaseDatas;
                }
                
                if (itemConfigData.itemType == PlayerItemType.Consume && (bagItem.RandomIncreaseDatas == null || bagItem.RandomIncreaseDatas.Count == 0))
                {
                    var memoryList = new MemoryList<RandomAttributeIncreaseData>(attributeData.Length);
                    for (int i = 0; i < attributeData.Length; i++)
                    {
                        var attribute = attributeData[i];
                        if (attribute is RandomAttributeIncreaseData randomAttributeIncreaseData)
                        {
                            memoryList.Add(randomAttributeIncreaseData);
                        }
                    }
                    bagItem.RandomIncreaseDatas = memoryList;
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

            if (!Constant.IsServer)
            {
                return;
            }
            if (itemsUseCommand.Slots.Count == 0)
            {
                return;
            }
            
            foreach (var kvp in itemsUseCommand.Slots)
            {
                var itemData = kvp.Value;
                if (!PlayerItemState.TryGetSlotItemBySlotIndex(playerItemState, itemData.SlotIndex, out var bagItem))
                {
                    Debug.LogError($"Failed to use item {itemData.SlotIndex}");
                    return;
                }

                var itemIds = bagItem.ItemIds;
                
                foreach (var itemId in itemIds)
                {
                    if (!PlayerItemState.RemoveItem(ref playerItemState, bagItem.IndexSlot, 1, out var bagSlotItem, out var removedItemIds))
                    {
                        Debug.LogWarning($"Failed to remove item {bagItem.IndexSlot} 1 from bag");
                        break;
                    }
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
        public SkillConfig SkillConfig;
    }
}