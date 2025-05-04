using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.Server.InGame;
using MemoryPack;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using PropertyAttackCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyAttackCommand;
using PropertyAutoRecoverCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyAutoRecoverCommand;
using PropertyBuffCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyBuffCommand;
using PropertyCalculator = HotUpdate.Scripts.Network.PredictSystem.State.PropertyCalculator;
using PropertyClientAnimationCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyClientAnimationCommand;
using PropertyEnvironmentChangeCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyEnvironmentChangeCommand;
using PropertyServerAnimationCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyServerAnimationCommand;
using PropertySkillCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertySkillCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerPropertySyncSystem : BaseSyncSystem
    {
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerMinProperties { get; private set; }
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerMaxProperties { get; private set; }
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerBaseProperties { get; private set; }
        public Dictionary<int, PropertyPredictionState> PlayerPredictionState { get; private set; }
        private ImmutableList<BuffManagerData> _activeBuffs;
        private ImmutableList<EquipmentData> _activeEquipments;
        private ImmutableList<EquipmentPassiveData> _passiveBuffs;
        private IConfigProvider _configProvider;
        private AnimationConfig _animationConfig;
        private ConstantBuffConfig _constantBuffConfig;
        private JsonDataConfig _jsonDataConfig;
        private RandomBuffConfig _randomBuffConfig;
        private PropertyConfig _propertyConfig;
        private PlayerInGameManager _playerInGameManager;
        private WeaponConfig _weaponConfig;
        private ArmorConfig _armorConfig;
        private ItemConfig _itemConfig;
        private BattleEffectConditionConfig _battleEffectConfig;
        
        public event Action<int, PropertyTypeEnum, float> OnPropertyChange;

        [Inject]
        private void InitContainers(IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
            _configProvider = configProvider;
            _jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _animationConfig = _configProvider.GetConfig<AnimationConfig>();
            _constantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = configProvider.GetConfig<RandomBuffConfig>();
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
            _weaponConfig = _configProvider.GetConfig<WeaponConfig>();
            _armorConfig = _configProvider.GetConfig<ArmorConfig>();
            _itemConfig = _configProvider.GetConfig<ItemConfig>();
            _battleEffectConfig = _configProvider.GetConfig<BattleEffectConditionConfig>();
            _playerInGameManager = playerInGameManager;
            ConfigPlayerMinProperties = _propertyConfig.GetPlayerMinProperties();
            ConfigPlayerMaxProperties = _propertyConfig.GetPlayerMaxProperties();
            ConfigPlayerBaseProperties = _propertyConfig.GetPlayerBaseProperties();
            _activeBuffs ??= ImmutableList<BuffManagerData>.Empty;
            _passiveBuffs??= ImmutableList<EquipmentPassiveData>.Empty;
            _activeEquipments ??= ImmutableList<EquipmentData>.Empty;
            BuffDataReaderWriter.RegisterReaderWriter();
        }

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerPredictablePropertyState>>(state);
            foreach (var playerState in playerStates)
            {
                if (!PropertyStates.ContainsKey(playerState.Key))
                {
                    continue;
                }
                PropertyStates[playerState.Key] = playerState.Value;
                PropertyChange(playerState.Key);
            }
            
        }

        public PlayerPredictablePropertyState GetPredictablePropertyState(int playerId)
        {
            if (PropertyStates.TryGetValue(playerId, out var predictionState))
            {
                if (predictionState is PlayerPredictablePropertyState state)
                {
                    return state;
                }
            }
            return default;
        }

        private void PropertyChange(int connectionId)
        {
            var playerState = PropertyStates[connectionId];
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            if (playerState is PlayerPredictablePropertyState playerPredictablePropertyState)
            {
                foreach (var property in playerPredictablePropertyState.Properties.Keys)
                {
                    var propertyValue = playerPredictablePropertyState.Properties[property];
                    OnPropertyChange?.Invoke(connectionId, propertyValue.PropertyType, propertyValue.CurrentValue);
                    if (property == PropertyTypeEnum.AttackSpeed)
                    {
                        playerController.SetAnimatorSpeed(AnimationState.Attack, propertyValue.CurrentValue);
                    }
                }
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PropertyPredictionState>();
            var playerPropertyState = new PlayerPredictablePropertyState();
            playerPropertyState.Properties = GetPropertyCalculators();
            playerPredictableState.RegisterProperties(playerPropertyState);
            PropertyStates.Add(connectionId, playerPropertyState);
            PlayerPredictionState.Add(connectionId, playerPredictableState);
        }

        public Dictionary<PropertyTypeEnum, PropertyCalculator> GetPropertyCalculators()
        {
            var dictionary = new Dictionary<PropertyTypeEnum, PropertyCalculator>();
            var enumValues = (PropertyTypeEnum[])Enum.GetValues(typeof(PropertyTypeEnum));
            foreach (var propertyType in enumValues)
            {
                var propertyData = new PropertyCalculator.PropertyData();
                var propertyConfig = _propertyConfig.GetPropertyConfigData(propertyType);
                propertyData.BaseValue = ConfigPlayerBaseProperties[propertyType];
                propertyData.Additive = 0;
                propertyData.Multiplier = 1;
                propertyData.Correction = 1;
                propertyData.CurrentValue = propertyData.BaseValue;
                propertyData.MaxCurrentValue = propertyData.BaseValue;
                var calculator = new PropertyCalculator(propertyType, propertyData,ConfigPlayerMinProperties[propertyType], ConfigPlayerMaxProperties[propertyType], propertyConfig.consumeType == PropertyConsumeType.Consume);
                dictionary.Add(propertyType, calculator);
            }
            return dictionary;
        }

        private void UpdateBuffs(float deltaTime)
        {
            if (_activeBuffs.Count <= 0)
                return;
            for (var i = _activeBuffs.Count - 1; i >= 0; i--)
            {
                _activeBuffs = _activeBuffs.SetItem(i, _activeBuffs[i].Update(deltaTime));
                if (_activeBuffs[i].BuffData.IsExpired())
                {
                    var buffData = _activeBuffs[i].BuffData;
                    for (int j = 0; j < buffData.BuffData.increaseDataList.Count; j++)
                    {
                        var buff = buffData.BuffData.increaseDataList[j];
                        buff.operationType = BuffOperationType.Subtract;
                        buffData.BuffData.increaseDataList[j] = buff;
                    }
                    HandleBuffRemove(_activeBuffs[i].BuffData, i);
                }
            }
        }

        public override CommandType HandledCommandType => CommandType.Property;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (command is PropertyAutoRecoverCommand)
            {
                HandlePropertyRecover(header.ConnectionId);
            }
            else if (command is PropertyClientAnimationCommand clientAnimationCommand)
            {
                HandleAnimationCommand(header.ConnectionId, clientAnimationCommand.AnimationState);
            }
            else if (command is PropertyServerAnimationCommand serverAnimationCommand)
            {
                HandleAnimationCommand(header.ConnectionId, serverAnimationCommand.AnimationState);
            }
            else if (command is PropertyBuffCommand buffCommand)
            {
                HandleBuff(header.ConnectionId, buffCommand.BuffExtraData, buffCommand.CasterId);
            }
            else if (command is PropertyAttackCommand attackCommand)
            {
                var targetPlayerIds = _playerInGameManager.GetPlayersWithNetIds(attackCommand.TargetIds);
                HandlePlayerAttack(header.ConnectionId, targetPlayerIds);
            }
            else if (command is PropertySkillCommand skillCommand)
            {
                HandleSkill(header.ConnectionId, skillCommand.SkillId);
            }
            else if (command is PropertyEnvironmentChangeCommand environmentChangeCommand)
            {
                HandleEnvironmentChange(header.ConnectionId, environmentChangeCommand.HasInputMovement, environmentChangeCommand.PlayerEnvironmentState, environmentChangeCommand.IsSprinting);
            }
            else if (command is PropertyInvincibleChangedCommand invincibleChangedCommand)
            {
                HandleInvincibleChanged(header.ConnectionId, invincibleChangedCommand.IsInvincible);
            }
            else if (command is PropertyEquipmentChangedCommand equipmentChangedCommand)
            {
                HandleEquipmentChanged(header.ConnectionId, equipmentChangedCommand.EquipConfigId, equipmentChangedCommand.EquipItemId, equipmentChangedCommand.IsEquipped);
            }
            else if (command is PropertyEquipmentPassiveCommand propertyEquipmentPassiveCommand)
            {
                HandlePropertyEquipmentPassiveCommand(header.ConnectionId, propertyEquipmentPassiveCommand.EquipItemConfigId, propertyEquipmentPassiveCommand.EquipItemId, propertyEquipmentPassiveCommand.IsEquipped, propertyEquipmentPassiveCommand.PlayerItemType);
            }
            else if (command is GoldChangedCommand goldChangedCommand)
            {
                HandleGoldChanged(header.ConnectionId, goldChangedCommand.Gold);
            }
            else
            {
                Debug.LogError($"PlayerPropertySyncSystem: Unknown command type {command.GetType().Name}");
            }
            return null;
        }

        private void HandleGoldChanged(int headerConnectionId, float gold)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(headerConnectionId);
            var propertyCalculator = playerState.Properties[PropertyTypeEnum.Gold];
            propertyCalculator = propertyCalculator.UpdateCurrentValue(gold);
            playerState.Properties[PropertyTypeEnum.Gold] = propertyCalculator;
            PropertyStates[headerConnectionId] = playerState;
            PropertyChange(headerConnectionId);
        }

        private void HandlePropertyEquipmentPassiveCommand(int targetId, int equipConfigId, int equipItemId, bool isEquipped, PlayerItemType playerItemType)
        {
            BattleEffectConditionConfigData effectConditionConfigData;
            switch (playerItemType)
            {
                case PlayerItemType.Weapon:
                    var weaponConfigData = _weaponConfig.GetWeaponConfigData(equipConfigId);
                    effectConditionConfigData = _battleEffectConfig.GetConditionData(weaponConfigData.battleEffectConditionId);
                    break;
                case PlayerItemType.Armor:
                    var armorConfigData = _armorConfig.GetArmorConfigData(equipConfigId);
                    effectConditionConfigData = _battleEffectConfig.GetConditionData(armorConfigData.battleEffectConditionId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            var buffExtra = _randomBuffConfig.GetEquipmentBuffNoType();
            if (isEquipped)
            {
                HandleEquipPassiveProperty(targetId, buffExtra, equipConfigId, equipItemId, playerItemType, effectConditionConfigData.buffWeight);

            }
            else
            {
                HandleEquipPassivePropertyUnload(targetId, equipConfigId, equipItemId);
            }
        }

        private void HandleEquipmentChanged(int targetId, int equipConfigId, int equipConfigItemId, bool isEquipped)
        {
            var buffExtraData = _itemConfig.GetGameItemData(equipConfigId).buffExtraData;

            if (isEquipped)
            {
                foreach (var buffData in buffExtraData)
                {
                    HandleEquipProperty(targetId, buffData, equipConfigId, equipConfigItemId);
                }
            }
            else
            {
                HandleEquipPropertyUnload(targetId, equipConfigId, equipConfigItemId);
            }
        }

        private void HandleEquipPropertyUnload(int targetId, int equipConfigId, int equipItemId)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(targetId);
            var changed = false;
            for (int i = 0; i < _activeEquipments.Count; i++)
            {
                var equipment = _activeEquipments[i];
                if (equipment.equipItemConfigId == equipConfigId && equipment.equipItemId == equipItemId)
                {
                    _activeEquipments = _activeEquipments.RemoveAt(i);
                    changed = true;
                    break;
                }
            }

            if (changed)
            {
                PropertyStates[targetId] = playerState;
                PropertyChange(targetId);
                return;
            }
            Debug.Log($"PlayerPropertySyncSystem: Equipment {equipConfigId} {equipItemId} not found in active equipments list");
        }

        private void HandleInvincibleChanged(int headerConnectionId, bool isInvincible)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(headerConnectionId);
            if (isInvincible)
            {
                playerState.SubjectedState = playerState.SubjectedState.AddState(SubjectedStateType.IsInvisible);
            }
            else
            {
                playerState.SubjectedState = playerState.SubjectedState.RemoveState(SubjectedStateType.IsInvisible);
            }
            PropertyStates[headerConnectionId] = playerState;
            PropertyChange(headerConnectionId);
        }
        
        private void HandlePropertyRecover(int connectionId)
        {
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            var playerState = GetState<PlayerPredictablePropertyState>(connectionId);
            playerController.HandlePropertyRecover(ref playerState);
            PropertyStates[connectionId] = playerState;
            PropertyChange(connectionId);
        }

        private void HandleBuff(int targetId, BuffExtraData buffExtraData, int? casterId = null)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(targetId);
            var buff = buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(buffExtraData) : _randomBuffConfig.GetBuff(buffExtraData);
            var newBuff = new BuffBase(buff, targetId, casterId);
            var buffManagerData = new BuffManagerData
            {
                BuffData = newBuff,
            };
            var propertyCalculator = playerState.Properties[newBuff.BuffData.propertyType];
            playerState.Properties[newBuff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, newBuff);
            _activeBuffs = _activeBuffs.Add(buffManagerData);
            PropertyStates[targetId] = playerState;
            PropertyChange(targetId);
        }

        private void HandleEquipProperty(int targetId, BuffExtraData buffExtraData, int equipItemConfigId, int equipItemId)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(targetId);
            var buff = buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(buffExtraData) : _randomBuffConfig.GetBuff(buffExtraData);
            var newBuff = new BuffBase(buff, targetId);
            var buffManagerData = new EquipmentData
            {
                BuffData = newBuff,
                equipItemConfigId = equipItemConfigId,
                equipItemId = equipItemId,
            };
            var propertyCalculator = playerState.Properties[newBuff.BuffData.propertyType];
            playerState.Properties[newBuff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, newBuff);
            _activeEquipments = _activeEquipments.Add(buffManagerData);
            PropertyStates[targetId] = playerState;
            PropertyChange(targetId);
        }

        private void HandleEquipPassivePropertyUnload(int targetId, int equipItemConfigId, int equipItemId)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(targetId);
            
            var changed = false;
            for (int i = 0; i < _passiveBuffs.Count; i++)
            {
                var passiveBuff = _passiveBuffs[i];
                if (passiveBuff.equipConfigId == equipItemConfigId && passiveBuff.equipItemId == equipItemId)
                {
                    changed = true;
                    _passiveBuffs = _passiveBuffs.RemoveAt(i);
                }
            }
            if (changed)
            {
                PropertyStates[targetId] = playerState;
                PropertyChange(targetId);
                return;
            }
            Debug.Log($"PlayerPropertySyncSystem: Equipment {equipItemConfigId} {equipItemId} not found in passive buffs list");
        }

        private void HandleEquipPassiveProperty(int targetId, BuffExtraData buffExtraData, int equipItemConfigId, int equipItemId, PlayerItemType playerItemType, float weight = 1)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(targetId);
            var buff = buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(buffExtraData) : _randomBuffConfig.GetBuff(buffExtraData, weight);
            var newBuff = new BuffBase(buff, targetId);
            var buffManagerData = new EquipmentPassiveData
            {
                BuffData = newBuff,
                playerItemType = playerItemType,
                equipConfigId = equipItemConfigId,
                equipItemId = equipItemId,
            };
            var propertyCalculator = playerState.Properties[newBuff.BuffData.propertyType];
            playerState.Properties[newBuff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, newBuff);
            _passiveBuffs = _passiveBuffs.Add(buffManagerData);
            PropertyStates[targetId] = playerState;
            PropertyChange(targetId);
        }

        private void HandleBuffRemove(BuffBase buff, int index)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(buff.TargetPlayerId);
            var propertyCalculator = playerState.Properties[buff.BuffData.propertyType];
            for (var i = 0; i < buff.BuffData.increaseDataList.Count; i++)
            {
                var buffIncreaseData = buff.BuffData.increaseDataList[i];
                buffIncreaseData.operationType = BuffOperationType.Subtract;
                buff.BuffData.increaseDataList[i] = buffIncreaseData;
            }
            playerState.Properties[buff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, buff);
            _activeBuffs = _activeBuffs.RemoveAt(index);
        }


        private PropertyCalculator HandleBuffInfo(PropertyCalculator propertyCalculator, BuffBase buffData)
        {
            return propertyCalculator.UpdateCalculator(buffData.BuffData.increaseDataList);
        }

        private void HandlePlayerAttack(int attacker, int[] defenderPlayerIds)
        {
            var propertyState = GetState<PlayerPredictablePropertyState>(attacker);
            var defendersState = PropertyStates
                .Where(x => defenderPlayerIds.Contains(x.Key))
                .ToDictionary(x => x.Key, x => (PlayerPredictablePropertyState)x.Value);
            var playerController = GameSyncManager.GetPlayerConnection(attacker);
            playerController.HandleAttackProperty(ref propertyState, ref defendersState, _jsonDataConfig.GetDamage);
            for (int i = 0; i < defenderPlayerIds.Length; i++)
            {
                PropertyStates[defenderPlayerIds[i]] = defendersState[defenderPlayerIds[i]];
                PropertyChange(defenderPlayerIds[i]);
                if (PropertyStates[defenderPlayerIds[i]] is PlayerPredictablePropertyState playerPropertyState &&
                    playerPropertyState.Properties[PropertyTypeEnum.Health].CurrentValue <= 0)
                {
                    //todo: handle dead player logic
                    var deadManId = _playerInGameManager.GetPlayerNetId(defenderPlayerIds[i]);
                    var deadTime = _jsonDataConfig.GameConfig.GetPlayerDeathTime((int)defendersState[defenderPlayerIds[i]].Properties[PropertyTypeEnum.Score].CurrentValue);
                    if (!_playerInGameManager.TryAddDeathPlayer(deadManId, deadTime, attacker, OnPlayerDeath, OnPlayerRespawn))
                    {
                        Debug.LogError($"PlayerPropertySyncSystem: Failed to add death player {deadManId}");
                    }
                }
            }
            PropertyStates[attacker] = propertyState;   
            PropertyChange(attacker);
        }

        private void OnPlayerDeath(uint playerId, int killerId, float countdownTime)
        {
            var playerConnection = _playerInGameManager.GetPlayerId(playerId);
            var playerController = GameSyncManager.GetPlayerConnection(playerConnection);
            var playerState = GetState<PlayerPredictablePropertyState>(playerConnection);
            playerState.SubjectedState = playerState.SubjectedState.AddState(SubjectedStateType.IsDead);
            playerState = playerController.HandlePlayerDie(playerState, countdownTime);
            GameSyncManager.EnqueueServerCommand(new PlayerDeathCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(playerConnection, CommandType.Input, CommandAuthority.Server, CommandExecuteType.Immediate),
                DeadCountdownTime = countdownTime,
                KillerId = killerId,
            });
            PropertyStates[playerConnection] = playerState;
            PropertyChange(playerConnection);
        }

        private void OnPlayerRespawn(uint playerId)
        {
            var playerConnection = _playerInGameManager.GetPlayerId(playerId);
            var playerState = GetState<PlayerPredictablePropertyState>(playerConnection);
            playerState.SubjectedState = playerState.SubjectedState.RemoveState(SubjectedStateType.IsDead);
            var playerController = GameSyncManager.GetPlayerConnection(playerConnection);
            playerState = playerController.HandlePlayerRespawn(playerState);
            GameSyncManager.EnqueueServerCommand(new PlayerRebornCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(playerConnection, CommandType.Input, CommandAuthority.Server, CommandExecuteType.Immediate),
                RebornPosition = _playerInGameManager.GetPlayerRebornPoint(playerId)
            });
            PropertyStates[playerConnection] = playerState;
            PropertyChange(playerConnection);
        }

        private void HandleSkill(int attacker, int skillId)
        {
            
        }

        protected override void OnBroadcastStateUpdate()
        {
            UpdateBuffs(GameSyncManager.TickRate);
            base.OnBroadcastStateUpdate();
        }

        private void HandleAnimationCommand(int connectionId, AnimationState command)
        {
            var cost = _animationConfig.GetPlayerAnimationCost(command);
            if (cost <= 0)
            {
                return;
            }
            var playerState = GetState<PlayerPredictablePropertyState>(connectionId);
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            playerController.HandleAnimationCost(ref playerState, command, cost);
            PropertyStates[connectionId] = playerState;
            PropertyChange(connectionId);
        }

        private void HandleEnvironmentChange(int connectionId, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(connectionId);
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            playerController.HandleEnvironmentChange(ref playerState, hasInputMovement, environmentType, isSprinting);
            PropertyStates[connectionId] = playerState;
            PropertyChange(connectionId);
        }

        public Dictionary<PropertyTypeEnum, PropertyCalculator> GetPlayerProperty(int connectionId)
        {
            return GetState<PlayerPredictablePropertyState>(connectionId).Properties;
        }

        public bool TryUseGold(int connectionId, int costGold, out float currentGold)
        {
            var gold = GetPlayerProperty(connectionId, PropertyTypeEnum.Gold);
            currentGold = gold - costGold;
            return currentGold >= 0;
        }
        
        public float GetPlayerGold(int connectionId)
        {
            return GetPlayerProperty(connectionId, PropertyTypeEnum.Gold);
        }

        public float GetPlayerProperty(int connectionId, PropertyTypeEnum propertyType)
        {
            return GetState<PlayerPredictablePropertyState>(connectionId).Properties[propertyType].CurrentValue;
        }
        
        public float GetPlayerMaxProperty(int connectionId, PropertyTypeEnum propertyType)
        {
            return GetState<PlayerPredictablePropertyState>(connectionId).Properties[propertyType].MaxCurrentValue;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = PlayerPredictionState[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            if (oldState == null || newState == null)
            {
                Debug.LogError($"PlayerPropertySyncSystem: oldState and newState are required.");
                return false;
            }
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _activeBuffs = _activeBuffs.Clear();
            PlayerPredictionState.Clear();
        }


        public IEnumerable<(int, float)> GetPlayerPropertiesWithCondition(Func<PropertyCalculator, bool> condition, bool isCurrentValue = false)
        {
            foreach (var playerState in PropertyStates.Keys)
            {
                var value = PropertyStates[playerState];
                if (value is PlayerPredictablePropertyState playerPredictablePropertyState)
                {
                    foreach (var property in playerPredictablePropertyState.Properties.Keys)
                    {
                        var propertyValue = playerPredictablePropertyState.Properties[property];
                        if (condition(propertyValue))
                        {
                            yield return (playerState, isCurrentValue ? propertyValue.CurrentValue : propertyValue.MaxValue);
                        }
                    }
                }
            }
        }

        public (int, float)[] SortPlayerProperties((int, float)[] properties, Func<(int, float), int> sortFunc)
        {
            for (int i = 0; i < properties.Length; i++)
            {
                for (int j = i + 1; j < properties.Length; j++)
                {
                    if (sortFunc(properties[i]) > sortFunc(properties[j]))
                    {
                        (properties[i], properties[j]) = (properties[j], properties[i]);
                    }
                }
            }
            return properties;
        }

        public (int, float)[] GetSortedPlayerPropertiesWithCondition(Func<PropertyCalculator, bool> condition,
            Func<(int, float), int> sortFunc, bool isCurrentValue = false)
        {
            var properties = GetPlayerPropertiesWithCondition(condition, isCurrentValue).ToArray();
            return SortPlayerProperties(properties, sortFunc);
        }

        [Serializable]
        private struct BuffManagerData
        {
            public BuffBase BuffData;

            public BuffManagerData Update(float deltaTime)
            {
                return new BuffManagerData
                {
                    BuffData = BuffData.Update(deltaTime),
                };
            }
        }
        
        [Serializable]
        private struct EquipmentData
        {
            public int equipItemConfigId;
            public int equipItemId;
            public BuffBase BuffData;
        }
        
        [Serializable]
        private struct EquipmentPassiveData
        {
            public PlayerItemType playerItemType;
            public int equipConfigId;
            public int equipItemId;
            public BuffBase BuffData;
        }
    }
}