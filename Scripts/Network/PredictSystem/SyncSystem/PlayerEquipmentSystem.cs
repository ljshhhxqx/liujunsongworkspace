using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerEquipmentSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerEquipmentSyncState> _playerEquipmentSyncStates = new Dictionary<int, PlayerEquipmentSyncState>();
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private WeaponConfig _weaponConfig;
        private ArmorConfig _armorConfig;
        private ItemConfig _itemConfig;
        private BattleEffectConditionConfig _battleEffectConfig;

        [Inject]
        private void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            _weaponConfig = configProvider.GetConfig<WeaponConfig>();
            _armorConfig = configProvider.GetConfig<ArmorConfig>();
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            _battleEffectConfig = configProvider.GetConfig<BattleEffectConditionConfig>();
            UpdateEquipmentCd(_tokenSource.Token).Forget();
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

        private int GetItemConfigId(EquipmentPart part, int equipmentConfigId)
        {
            switch (part)
            {
                case EquipmentPart.Weapon:
                    return _weaponConfig.GetWeaponConfigData(equipmentConfigId).itemID;
                case EquipmentPart.Body:
                case EquipmentPart.Head:
                case EquipmentPart.Leg:
                case EquipmentPart.Feet:
                case EquipmentPart.Waist:
                    return _armorConfig.GetArmorConfigData(equipmentConfigId).itemID;
                default:
                    return 0;
            }
        }

        private ConditionCheckerHeader GetConditionCheckerHeader(PlayerItemType itemType, int itemConfigId)
        {
            var conditionConfigId = 0;
            switch (itemType)
            {
                case PlayerItemType.Weapon:
                    conditionConfigId = _weaponConfig.GetWeaponConfigByItemID(itemConfigId).battleEffectConditionId;
                    break;
                case PlayerItemType.Armor:
                    conditionConfigId = _armorConfig.GetArmorConfigByItemID(itemConfigId).battleEffectConditionId;
                    break;
            }
            var congfig = _battleEffectConfig.GetConditionData(conditionConfigId);

            var header = ConditionCheckerHeader.Create(congfig.triggerType, congfig.interval, congfig.probability,
                congfig.conditionParam, congfig.targetType, congfig.targetCount);
            return header;
        }

        private IConditionChecker GetConditionChecker(PlayerItemType itemType, int itemConfigId)
        {
            var header = GetConditionCheckerHeader(itemType, itemConfigId);
            if (header.CheckParams == null)
            {
                Debug.LogWarning($"Can't find condition params for item {itemConfigId}");
                return null;
            }
            var conditionChecker = IConditionChecker.CreateChecker(header);
            return conditionChecker;
        }

        private async UniTaskVoid UpdateEquipmentCd(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1 / GameSyncManager.TickRate), cancellationToken: token);
                foreach (var playerId in PropertyStates.Keys)
                {
                    var playerState = PropertyStates[playerId];
                    if (playerState is PlayerEquipmentState playerEquipmentSyncState)
                    {
                        PlayerEquipmentState.UpdateCheckerCd(ref playerEquipmentSyncState, GameSyncManager.TickRate);
                        PropertyStates[playerId] = playerEquipmentSyncState;
                    }
                }
            }
        }

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerEquipmentState>>(state);
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
            var playerPredictableState = player.GetComponent<PlayerEquipmentSyncState>();
            var playerInputState = new PlayerEquipmentState();
            PropertyStates.Add(connectionId, playerInputState);
            _playerEquipmentSyncStates.Add(connectionId, playerPredictableState);
        }

        public override CommandType HandledCommandType => CommandType.Equipment;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (!header.CommandType.HasAnyState(CommandType.Equipment))
                return null;
            if (command is EquipmentCommand equipmentCommand)
            {
                var playerState = PropertyStates[header.ConnectionId];
                if (playerState is PlayerEquipmentState playerEquipmentState)
                {
                    var configId = GetItemConfigId(equipmentCommand.EquipmentPart, equipmentCommand.EquipmentConfigId);
                    var itemConfig = _itemConfig.GetGameItemData(configId); 
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
                        IsEquipped = false,
                    };

                    if (!equipmentCommand.IsEquip)
                    {
                        PlayerEquipmentState.TryUnequipped(ref playerEquipmentState, itemId, itemConfig.equipmentPart);
                        GameSyncManager.EnqueueServerCommand(propertyEquipmentChangedCommand);
                        GameSyncManager.EnqueueServerCommand(propertyEquipPassiveCommand);
                        //todo: 
                        return playerEquipmentState;
                    }
                    var conditionChecker = GetConditionChecker(itemConfig.itemType, configId);
                    if (conditionChecker == null)
                    {
                        Debug.LogWarning($"Can't find condition checker for item {itemId}");
                        return null;
                    }

                    if (!PlayerEquipmentState.TryAddEquipmentData(ref playerEquipmentState, itemId, itemConfig.equipmentPart, conditionChecker))
                    {
                        Debug.LogWarning($"Can't equip this item {itemId} to player {header.ConnectionId}");
                        return null;
                    }
                    GameSyncManager.EnqueueServerCommand(propertyEquipmentChangedCommand);
                    //todo:
                    return playerEquipmentState;
                }
            }
            else if (command is TriggerCommand triggerCommand)
            {
                var playerState = PropertyStates[header.ConnectionId];
                if (playerState is PlayerEquipmentState playerEquipmentState)
                {
                    var data = triggerCommand.TriggerData;
                    var checkParams = MemoryPackSerializer.Deserialize<IConditionCheckerParameters>(data);
                    var isCheckPassed = PlayerEquipmentState.CheckConditions(ref playerEquipmentState, checkParams);
                    PropertyStates[header.ConnectionId] = playerEquipmentState;
                    if (isCheckPassed)
                    {
                        return playerEquipmentState;
                    }
                }
            }

            return null;
        }
        

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _playerEquipmentSyncStates[connectionId];
            playerPredictableState.ApplyState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _playerEquipmentSyncStates.Clear();
            _tokenSource?.Dispose();
            _tokenSource?.Cancel();
        }
    }
}