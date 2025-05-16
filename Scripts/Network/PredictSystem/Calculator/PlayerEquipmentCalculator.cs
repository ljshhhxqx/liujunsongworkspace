
using System.Collections.Immutable;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using MemoryPack;
using Newtonsoft.Json;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerEquipmentCalculator : IPlayerStateCalculator
    {
        public static PlayerEquipmentConstant Constant { get; private set; }
        
        public static void SetConstant(PlayerEquipmentConstant constant)
        {
            Constant = constant;
        }
        
        private static IConditionChecker GetConditionChecker(PlayerItemType itemType, int itemConfigId)
        {
            var header = PlayerItemCalculator.GetConditionCheckerHeader(itemType, itemConfigId);
            if (header.CheckParams == null)
            {
                Debug.LogWarning($"Can't find condition params for item {itemConfigId}");
                return null;
            }
            var conditionChecker = IConditionChecker.CreateChecker(header);
            return conditionChecker;
        }
        
        public static ISyncPropertyState CommandEquipment(EquipmentCommand equipmentCommand, ref PlayerEquipmentState playerEquipmentState)
        {
            if (!Constant.IsServer)
                return playerEquipmentState;
            var header = equipmentCommand.Header;
            var configId = PlayerItemCalculator.GetItemConfigId(equipmentCommand.EquipmentPart, equipmentCommand.EquipmentConfigId);
            var itemConfig = Constant.ItemConfig.GetGameItemData(configId); 
            var itemId = equipmentCommand.ItemId;
            if (itemId == 0 || !GameItemManager.HasGameItemData(itemId))
            {
                Debug.LogWarning($"Can't find item data {itemId}"); 
                return null;
            }

            var propertyEquipmentChangedCommand = new PropertyEquipmentChangedCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                    CommandAuthority.Server, CommandExecuteType.Immediate),
                EquipConfigId = configId,
                EquipItemId = itemId,
                IsEquipped = equipmentCommand.IsEquip,
            };
            var propertyEquipPassiveCommand = new PropertyEquipmentPassiveCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                    CommandAuthority.Server, CommandExecuteType.Immediate),
                EquipItemConfigId = configId,
                EquipItemId = itemConfig.id,
                PlayerItemType = itemConfig.itemType,
                IsEquipped = equipmentCommand.IsEquip,
            };

            if (!equipmentCommand.IsEquip && PlayerEquipmentState.TryUnequipped(ref playerEquipmentState, itemId, itemConfig.equipmentPart))
            {
                Constant.GameSyncManager.EnqueueServerCommand(propertyEquipmentChangedCommand);
                Constant.GameSyncManager.EnqueueServerCommand(propertyEquipPassiveCommand);
                //todo: 
                return playerEquipmentState;
            }
            var conditionChecker = GetConditionChecker(itemConfig.itemType, configId);
            if (conditionChecker == null)
            {
                Debug.LogWarning($"Can't find condition checker for item {itemId}");
                return null;
            }

            if (!PlayerEquipmentState.TryAddEquipmentData(ref playerEquipmentState, itemId, itemConfig.equipmentPart, 
                    conditionChecker))
            {
                Debug.LogWarning($"Can't equip this item {itemId} to player {header.ConnectionId}");
                return null;
            }

            var mainAttribute = JsonConvert.DeserializeObject<AttributeIncreaseData[]>(equipmentCommand.EquipmentMainEffectData);
            var subAttribute = JsonConvert.DeserializeObject<AttributeIncreaseData[]>(equipmentCommand.EquipmentPassiveEffectData);
            if (!PlayerEquipmentState.TryAddEquipmentPassiveEffectData(ref playerEquipmentState, itemId, itemConfig.equipmentPart,mainAttribute, subAttribute))
            {
                Debug.LogWarning($"Can't add passive effect data for item {itemId}");
                return null;
            }

            Constant.GameSyncManager.EnqueueServerCommand(propertyEquipmentChangedCommand);
            //todo:
            return playerEquipmentState;
        }

        public static bool CommandTrigger(TriggerCommand triggerCommand, ref PlayerEquipmentState playerEquipmentState)
        {
            var header = triggerCommand.Header;
            var data = triggerCommand.TriggerData;
            var checkParams = MemoryPackSerializer.Deserialize<IConditionCheckerParameters>(data);
            var isCheckPassed = PlayerEquipmentState.CheckConditions(ref playerEquipmentState, checkParams);

            if (isCheckPassed)
            {
                var propertyEquipPassiveCommand = new PropertyEquipmentPassiveCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                        CommandAuthority.Server, CommandExecuteType.Immediate),
                    EquipItemConfigId = configId,
                    EquipItemId = itemConfig.id,
                    PlayerItemType = itemConfig.itemType,
                    IsEquipped = equipmentCommand.IsEquip,
                };
            }
            return isCheckPassed;
        }
    }

    public struct PlayerEquipmentConstant
    {
        public GameSyncManager GameSyncManager;
        public ItemConfig ItemConfig;
        public bool IsServer;
    }
}