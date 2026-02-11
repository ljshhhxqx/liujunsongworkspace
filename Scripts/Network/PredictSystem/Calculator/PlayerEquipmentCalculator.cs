using System;
using System.Collections.Generic;
using AOTScripts.Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using Newtonsoft.Json;
using UnityEngine;
using ISyncPropertyState = AOTScripts.Data.ISyncPropertyState;
using PlayerEquipmentState = AOTScripts.Data.PlayerEquipmentState;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerEquipmentCalculator : IPlayerStateCalculator
    {
        public static PlayerEquipmentConstant Constant { get; private set; }
        
        public static void SetConstant(PlayerEquipmentConstant constant)
        {
            Constant = constant;
        }
        
        public static IConditionChecker GetConditionChecker(PlayerItemType itemType, int itemConfigId)
        {
            var header = PlayerItemCalculator.GetConditionCheckerHeader(itemType, itemConfigId);
            if (header.CheckParams == null)
            {
                Debug.LogWarning($"Can't find condition params for item {itemConfigId}");
                return null;
            }
            var conditionChecker = ConditionCheckerHeader.CreateChecker(header);
            return conditionChecker;
        }
        
        public static void CommandEquipment(EquipmentCommand equipmentCommand, ref PlayerEquipmentState playerEquipmentState)
        {
            var step = 0;
            try
            {
                step = 1;
                if (!Constant.IsServer)
                    return;
                step = 2;
                var header = equipmentCommand.Header;
                step = 3;
                var configId = PlayerItemCalculator.GetItemConfigId(equipmentCommand.EquipmentPart, equipmentCommand.EquipmentConfigId);
                step = 4;
                var itemConfig = Constant.ItemConfig.GetGameItemData(configId); 
                step = 5;
                var itemId = equipmentCommand.ItemId;
                step = 6;
                var equipConfigId = equipmentCommand.EquipmentConfigId;
                step = 7;
                if (itemId == 0 || !GameItemManager.HasGameItemData(itemId))
                {
                    Debug.LogWarning($"Can't find item data {itemId}"); 
                    return;
                }
                step = 8;

                var propertyEquipmentChangedCommand = new PropertyEquipmentChangedCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                        CommandAuthority.Client, CommandExecuteType.Immediate),
                    EquipConfigId = itemConfig.id,
                    EquipItemId = itemId,
                    IsEquipped = equipmentCommand.IsEquip,
                    ItemConfigId = itemConfig.id,
                    EquipmentPart = itemConfig.equipmentPart,
                };
                step = 9;
                var propertyEquipPassiveCommand = new PropertyEquipmentPassiveCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                        CommandAuthority.Client, CommandExecuteType.Immediate),
                    EquipItemConfigId = equipConfigId,
                    EquipItemId = itemId,
                    PlayerItemType = itemConfig.itemType,
                    IsEquipped = equipmentCommand.IsEquip,
                };
                step = 10;
                if (!equipmentCommand.IsEquip && PlayerEquipmentState.TryUnequipped(ref playerEquipmentState, itemId, itemConfig.equipmentPart, out var unequippedEquipment))
                {
                    Constant.GameSyncManager.EnqueueServerCommand(propertyEquipmentChangedCommand);
                    Constant.GameSyncManager.EnqueueServerCommand(propertyEquipPassiveCommand);
                    return;
                }
                step = 11;
                var conditionChecker = GetConditionChecker(itemConfig.itemType, configId);
                step = 12;
                if (conditionChecker == null)
                {
                    return;
                }
                step = 13;
                if (conditionChecker.GetConditionCheckerHeader().TriggerType == TriggerType.None)
                {
                    Constant.GameSyncManager.EnqueueServerCommand(propertyEquipPassiveCommand);
                }

                step = 14;
                if (!PlayerEquipmentState.TryAddEquipmentData(ref playerEquipmentState, itemId,  equipmentCommand.EquipmentConfigId, itemConfig.equipmentPart, 
                        conditionChecker))
                {
                    Debug.LogWarning($"Can't equip this item {itemId} to player {header.ConnectionId}");
                    return;
                }

                step = 15;
                var mainAttribute = JsonConvert.DeserializeObject<AttributeIncreaseData[]>(equipmentCommand.EquipmentMainEffectData);
                step = 16;
                var subAttribute = JsonConvert.DeserializeObject<AttributeIncreaseData[]>(equipmentCommand.EquipmentPassiveEffectData);
                step = 17;
                if (!PlayerEquipmentState.TryAddEquipmentPassiveEffectData(ref playerEquipmentState, itemId, equipmentCommand.EquipmentConfigId, itemConfig.equipmentPart,mainAttribute, subAttribute))
                {
                    Debug.LogWarning($"Can't add passive effect data for item {itemId}");
                    return;
                }
                step = 18;

                Constant.GameSyncManager.EnqueueServerCommand(propertyEquipmentChangedCommand);
            }
            
            catch (ArgumentOutOfRangeException ex)
            {
                Debug.LogError($"[CommandEquipment] Exception at step {step}");
                Debug.LogError($"Parameter: {ex.ParamName}, Message: {ex.Message}");
                Debug.LogError($"StackTrace:\n{ex.StackTrace}");
            }
            
        }

        public static bool CommandTrigger(TriggerCommand triggerCommand, ref PlayerEquipmentState playerEquipmentState, int[] playerIds, EquipmentPart equipmentPart,
             int equipmentConfigId, int itemId)
        {
            if (!Constant.IsServer)
                return false;
            return false;
            
            Debug.Log($"Start handle trigger {triggerCommand.TriggerType}");
            var header = triggerCommand.Header;
            var data = triggerCommand.TriggerData;
            var configId = PlayerItemCalculator.GetItemConfigId(equipmentPart, equipmentConfigId);
            var checkParams = NetworkCommandExtensions.DeserializeBattleCondition(data);
            var isCheckPassed = PlayerEquipmentState.CheckConditions(ref playerEquipmentState, checkParams, out var conditionChecker);
            if (isCheckPassed)
            {
                var itemData = GameItemManager.GetGameItemData(itemId);
                var propertyEquipPassiveCommand = new PropertyEquipmentPassiveCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                        CommandAuthority.Server, CommandExecuteType.Immediate),
                    EquipItemConfigId = configId,
                    EquipItemId = itemId,
                    PlayerItemType = itemData.ItemType,
                    IsEquipped = true,
                    TargetIds = playerIds,
                    CountDownTime = conditionChecker.GetConditionCheckerHeader().Interval,
                };
                Constant.GameSyncManager.EnqueueServerCommand(propertyEquipPassiveCommand);
            }
            return isCheckPassed;
        }
        

        public static (int, int, EquipmentPart) GetDataByTriggerType(PlayerEquipmentState playerEquipmentState, TriggerType triggerType)
        {
            for (int i = 0; i < playerEquipmentState.EquipmentDatas.Count; i++)
            {
                var data = playerEquipmentState.EquipmentDatas[i];
                Debug.Log($"Start DeserializeBattleChecker for trigger {triggerType}");
                if (data.TriggerType != triggerType)
                {
                    continue;
                }
                return (data.ItemId, data.EquipConfigId, data.EquipmentPartType);
            }

            return default;
        }

        public static bool TryGetEquipmentTrigger(PlayerEquipmentState playerEquipmentState, TriggerType triggerType, out List<IConditionChecker> conditionCheckers)
        {
            conditionCheckers = new List<IConditionChecker>(playerEquipmentState.EquipmentDatas.Count);
            bool hasConditions = false;
            for (int i = 0; i < playerEquipmentState.EquipmentDatas.Count; i++)
            {
                var data = playerEquipmentState.EquipmentDatas[i];
                if (data.ConditionChecker != null)
                {
                    if (data.ConditionChecker.GetConditionCheckerHeader().TriggerType == triggerType)
                    {
                        conditionCheckers.Add(data.ConditionChecker);
                        hasConditions = true;
                    }
                }
            }

            return hasConditions;
        }
    }

    public class PlayerEquipmentConstant
    {
        public GameSyncManager GameSyncManager;
        public ItemConfig ItemConfig;
        public bool IsServer;
        public bool IsClient;
        public bool IsLocalPlayer;
        public SkillConfig SkillConfig;
    }
}