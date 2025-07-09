using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AOTScripts.Data;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Static;
using MemoryPack;
using Mirror;
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
        private readonly Dictionary<int, PropertyPredictionState> _propertyPredictionStates = new Dictionary<int, PropertyPredictionState>();
        private ImmutableList<BuffManagerData> _activeBuffs;
        private ImmutableList<TimedBuffData> _timedBuffs;
        private ImmutableList<EquipmentData> _activeEquipments;
        private ImmutableList<EquipmentPassiveData> _passiveBuffs;
        private ImmutableList<SkillBuffManagerData> _skillBuffs;
        private IConfigProvider _configProvider;
        private AnimationConfig _animationConfig;
        private ConstantBuffConfig _constantBuffConfig;
        private JsonDataConfig _jsonDataConfig;
        private TimedBuffConfig _timedBuffConfig;
        private RandomBuffConfig _randomBuffConfig;
        private PropertyConfig _propertyConfig;
        private PlayerInGameManager _playerInGameManager;
        private WeaponConfig _weaponConfig;
        private ArmorConfig _armorConfig;
        private ItemConfig _itemConfig;
        private SkillConfig _skillConfig;
        private GameConfigData _gameConfigData;
        private BattleEffectConditionConfig _battleEffectConfig;
        private float _timeBuffTimer;
        private readonly List<(BuffBase, int)> _previousNoUnionPlayerBuff = new List<(BuffBase, int)>();
        protected override CommandType CommandType => CommandType.Property;
        
        public event Action<int, PropertyTypeEnum, float> OnPropertyChange;
        
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _configProvider = configProvider;
            _jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _gameConfigData = _jsonDataConfig.GameConfig;
            _animationConfig = _configProvider.GetConfig<AnimationConfig>();
            _timedBuffConfig = _configProvider.GetConfig<TimedBuffConfig>();
            _constantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = configProvider.GetConfig<RandomBuffConfig>();
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
            _weaponConfig = _configProvider.GetConfig<WeaponConfig>();
            _armorConfig = _configProvider.GetConfig<ArmorConfig>();
            _itemConfig = _configProvider.GetConfig<ItemConfig>();
            _skillConfig = _configProvider.GetConfig<SkillConfig>();
            _battleEffectConfig = _configProvider.GetConfig<BattleEffectConditionConfig>();
            _playerInGameManager = PlayerInGameManager.Instance;
            _activeBuffs ??= ImmutableList<BuffManagerData>.Empty;
            _passiveBuffs??= ImmutableList<EquipmentPassiveData>.Empty;
            _timedBuffs ??= ImmutableList<TimedBuffData>.Empty;
            _activeEquipments ??= ImmutableList<EquipmentData>.Empty;
            _skillBuffs ??= ImmutableList<SkillBuffManagerData>.Empty;
            BuffDataReaderWriter.RegisterReaderWriter();
        }

        

        protected override void OnClientProcessStateUpdate(int connectionId, byte[] state, CommandType commandType)
        {
            if (commandType!= CommandType.Property)
            {
                return;
            }
            //var playerStates = MemoryPackSerializer.Deserialize<PlayerPredictablePropertyState>(state);
            var playerStates = NetworkCommandExtensions.DeserializePlayerState(state);
            
            if (playerStates is not PlayerPredictablePropertyState playerPredictablePropertyState)
            {
                Debug.LogError($"Player {playerStates.GetStateType().ToString()} is not a PlayerPredictablePropertyState.");
                return;
            }

            if (PropertyStates.ContainsKey(connectionId))
            {
                PropertyStates[connectionId] = playerStates;
            }
        }
        
        public override byte[] GetPlayerSerializedState(int connectionId)
        {
            if (PropertyStates.TryGetValue(connectionId, out var playerState))
            {
                if (playerState is PlayerPredictablePropertyState playerPredictablePropertyState)
                {
                    //return MemoryPackSerializer.Serialize(playerPredictablePropertyState);
                    return NetworkCommandExtensions.SerializePlayerState(playerPredictablePropertyState);
                }

                Debug.LogError($"Player {playerState.GetStateType().ToString()} property state is not PlayerPredictablePropertyState.");
                return null;
            }
            Debug.LogError($"Player {connectionId} property state not found.");
            return null;
        }
        
        [Server]
        public void AddBuffToAllPlayer(int currentRound)
        {
            var connections = NetworkServer.connections;
            var buffs = _timedBuffConfig.GetRandomBuffs(BuffSourceType.Round, connections.Count);
            if (buffs == null || buffs.Count == 0)
            {
                Debug.LogError($"No buffs available for {currentRound}");
                return;
            }
            foreach (var id in connections.Keys)
            {
                var buff = buffs.RandomSelect();
                buffs.Remove(buff);
                HandleTimedBuff(id, buff);
            }
        }

        [Server]
        public void AddBuffToLowScorePlayer(int currentRound)
        {
            var buff = _timedBuffConfig.GetRandomBuff(BuffSourceType.Score);
            if (buff == 0)
            {
                Debug.LogError($"No buffs available for {currentRound}");
                return;
            }
            var sortedPlayerProperties = GetSortedPlayerProperties(PropertyTypeEnum.Score, true, true);
            var maxScorePlayer = sortedPlayerProperties.Last().Key;
            sortedPlayerProperties.Remove(maxScorePlayer);
            var player = sortedPlayerProperties.SelectByWeight();
            HandleTimedBuff(player, buff);
        }

        public PropertyCalculator GetPropertyCalculator(int playerId, PropertyTypeEnum propertyType)
        {
            var playerState = GetPredictablePropertyState(playerId);
            if (playerState.MemoryProperty == null)
            {
                Debug.LogError($"No properties available for {playerId}");
                return default;
            }
            return playerState.MemoryProperty.GetValueOrDefault(propertyType);
        }
        
        [Server]
        public void AllPlayerGetSpeed()
        {
            var speedBuff = _timedBuffConfig.GetNoUnionSpeedBuffId();
            if (speedBuff == 0)
            {
                Debug.LogError("No speed buff available");
                return;
            }
            foreach (var connection in NetworkServer.connections.Values)
            {
                HandleTimedBuff(connection.connectionId, speedBuff);
            }   
        }
        
        private Dictionary<int, float> GetAllPlayerProperties(PropertyTypeEnum propertyType, bool isMaxValue = false)
        {
            var playerProperties = new Dictionary<int, float>();
            foreach (var playerId in PropertyStates.Keys)
            {
                var playerState = PropertyStates[playerId];
                if (playerState is PlayerPredictablePropertyState predictablePropertyState)
                {
                    if (predictablePropertyState.MemoryProperty.TryGetValue(propertyType, out var propertyValue))
                    {
                        playerProperties.Add(playerId, isMaxValue ? propertyValue.MaxCurrentValue : propertyValue.CurrentValue);
                    }
                }
            }
            return playerProperties;
        }

        private float GetPlayerOneProperty(int playerId, PropertyTypeEnum propertyType, bool isMaxValue = false)
        {
            var playerProperties = GetAllPlayerProperties(propertyType, isMaxValue);
            return playerProperties.GetValueOrDefault(playerId, 0);
        }

        private Dictionary<int, float> GetSortedPlayerProperties(PropertyTypeEnum propertyType, bool isAscending = true, bool isMaxValue = false)
        {
            var playerProperties = GetAllPlayerProperties(propertyType, isMaxValue);
            if (isAscending)
            {
                return playerProperties.OrderBy(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
            }
            return playerProperties.OrderByDescending(x => x.Value).ToDictionary(x => x.Key, x => x.Value);
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
                foreach (var property in playerPredictablePropertyState.MemoryProperty.Keys)
                {
                    var propertyValue = playerPredictablePropertyState.MemoryProperty[property];
                    OnPropertyChange?.Invoke(connectionId, propertyValue.PropertyType, propertyValue.CurrentValue);
                    if (property == PropertyTypeEnum.AttackSpeed)
                    {
                        playerController.SetAnimatorSpeed(AnimationState.Attack, propertyValue.CurrentValue);
                    }
                    else if (property == PropertyTypeEnum.Alpha)
                    {
                        playerController.RpcSetPlayerAlpha(propertyValue.CurrentValue);
                        
                    }
                }
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PropertyPredictionState>();
            var playerPropertyState = new PlayerPredictablePropertyState();
            var calculators = PlayerPropertyCalculator.GetPropertyCalculators();
            playerPropertyState.MemoryProperty = new MemoryDictionary<PropertyTypeEnum, PropertyCalculator>(calculators);
            playerPredictableState.RegisterProperties(playerPropertyState);
            PropertyStates.TryAdd(connectionId, playerPropertyState);
            _propertyPredictionStates.TryAdd(connectionId, playerPredictableState);
            RpcSetPlayerPropertyState(connectionId, NetworkCommandExtensions.SerializePlayerState(playerPropertyState));
            //RpcSetPlayerPropertyState(connectionId, MemoryPackSerializer.Serialize(playerPropertyState));
        }
        
        [ClientRpc]
        private void RpcSetPlayerPropertyState(int connectionId, byte[] playerPropertyState)
        {
            var syncState = NetworkServer.connections[connectionId].identity.GetComponent<PropertyPredictionState>();
            var playerState = NetworkCommandExtensions.DeserializePlayerState(playerPropertyState);
            syncState.InitCurrentState(playerState);
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
        
        private void UpdateTimedBuffs(float deltaTime)
        {
            if (_timedBuffs.Count <= 0)
                return;
            for (var i = _timedBuffs.Count - 1; i >= 0; i--)
            {
                var buffData = _timedBuffs[i];
                var newBuffData = _timedBuffConfig.GetCurrentBuffByDeltaTime(buffData.buffId, deltaTime);
                //_timedBuffs = _timedBuffs.SetItem(i, newBuffData);
                if (_timedBuffs[i].IsExpired())
                {
                    HandleTimedBuffRemove(_timedBuffs[i], i);
                }
                else
                {
                    ModifyPlayerTimedBuff(buffData, i, newBuffData);
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
                HandleAnimationCommand(header.ConnectionId, clientAnimationCommand.AnimationState, clientAnimationCommand.SkillId);
            }
            else if (command is PropertyServerAnimationCommand serverAnimationCommand)
            {
                HandleAnimationCommand(header.ConnectionId, serverAnimationCommand.AnimationState, serverAnimationCommand.SkillId);
            }
            else if (command is PropertyBuffCommand buffCommand)
            {
                HandleBuff(header.ConnectionId, buffCommand.BuffExtraData, buffCommand.CasterId, buffCommand.BuffSourceType);
            }
            else if (command is PropertyAttackCommand attackCommand)
            {
                var playerNetId = _playerInGameManager.GetPlayerNetId(header.ConnectionId);
                var hitPlayer = attackCommand.TargetIds.ToHashSet();
                if (_playerInGameManager.TryGetOtherPlayersInUnion(playerNetId, out var otherPlayers) && GameSyncManager.isRandomUnionStart)
                {
                    for (int i = 0; i < attackCommand.TargetIds.Length; i++)
                    {
                        var player = attackCommand.TargetIds[i];
                        if (otherPlayers.Contains(player))
                        {
                            hitPlayer.Remove(player);
                        }
                    }
                }
                var targetPlayerIds = _playerInGameManager.GetPlayersWithNetIds(hitPlayer.ToArray());
                HandlePlayerAttack(header.ConnectionId, targetPlayerIds);
            }
            else if (command is PropertySkillCommand skillCommand)
            {
                HandleSkill(header.ConnectionId, skillCommand.SkillId, skillCommand.HitPlayerIds);
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
            else if (command is PlayerTouchedBaseCommand)
            {
                HandlePlayerTouchedBase(header.ConnectionId);
            }
            else if(command is PlayerTraceOtherPlayerHpCommand playerTraceOtherPlayerHpCommand)
            {
                HandlePlayerTraceOtherPlayerHp(header.ConnectionId, playerTraceOtherPlayerHpCommand.TargetConnectionIds);
            }
            else if(command is NoUnionPlayerAddMoreScoreAndGoldCommand noUnionPlayerAddMoreScoreAndGoldCommand)
            {
                HandleNoUnionPlayerAddMoreScoreAndGold(header.ConnectionId, noUnionPlayerAddMoreScoreAndGoldCommand.PreNoUnionPlayer);
            }
            else if(command is PropertyUseSkillCommand propertyUseSkillCommand)
            {
                HandleUseSkill(header.ConnectionId, propertyUseSkillCommand.SkillConfigId);
            }
            else
            {
                Debug.LogError($"PlayerPropertySyncSystem: Unknown command type {command.GetType().Name}");
            }
            return null;
        }

        private void HandleUseSkill(int headerConnectionId, int skillConfigId)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(headerConnectionId);
            var skillConfigData = _skillConfig.GetSkillData(skillConfigId);
            var cost = skillConfigData.cost;
            var calculator = playerState.MemoryProperty[PropertyTypeEnum.Strength];
            if (calculator.CurrentValue < cost)
            {
                Debug.LogWarning($"PlayerPropertySyncSystem: {headerConnectionId} not enough strength to use skill {skillConfigId}");
                return;
            }
            calculator = calculator.UpdateCurrentValue(-cost);
            playerState.MemoryProperty[PropertyTypeEnum.Strength] = calculator;
            PropertyStates[headerConnectionId] = playerState;
            PropertyChange(headerConnectionId);
            
            var hpChangedCheckerParameters = SkillCastCheckerParameters.CreateParameters(
                TriggerType.OnSkillCast, cost/calculator.MaxCurrentValue, skillConfigData.skillType);
            GameSyncManager.EnqueueServerCommand(new TriggerCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(headerConnectionId, CommandType.Equipment),
                TriggerType = TriggerType.OnHpChange,
                TriggerData = NetworkCommandExtensions.SerializeBattleCondition(hpChangedCheckerParameters),
            });
        }

        private void HandleNoUnionPlayerAddMoreScoreAndGold(int headerConnectionId, int preNoUnionPlayer)
        {
            var noUnionBuffs = _constantBuffConfig.GetNoUnionBuffs();
            for (int i = 0; i < noUnionBuffs.Count; i++)
            {
                _previousNoUnionPlayerBuff.Add(AddBuffToPlayer(headerConnectionId, noUnionBuffs[i]));
            }
            if (preNoUnionPlayer != 0)
            {
                for (int i = 0; i < _previousNoUnionPlayerBuff.Count; i++)
                {
                    var buffTuple = _previousNoUnionPlayerBuff[i];
                    HandleBuffRemove(buffTuple.Item1, buffTuple.Item2);
                    _previousNoUnionPlayerBuff.RemoveAt(i);
                }
                
                for (int i = 0; i < noUnionBuffs.Count; i++)
                {
                    _previousNoUnionPlayerBuff.Add(AddBuffToPlayer(preNoUnionPlayer, noUnionBuffs[i]));
                }
            }
        }

        private void HandlePlayerTraceOtherPlayerHp(int headerConnectionId, int[] targetConnectionIds)
        {
            var playerController = _playerInGameManager.GetPlayerComponent<PlayerComponentController>(headerConnectionId);
            var list = new List<TracedPlayerInfo>();
            foreach (var id in targetConnectionIds)
            {
                var playerState = GetState<PlayerPredictablePropertyState>(id);
                var targetPlayer = _playerInGameManager.GetPlayerComponent<PlayerComponentController>(id);
                var tracedPlayerInfo = new TracedPlayerInfo
                {
                    PlayerId = id,
                    PlayerName = _playerInGameManager.GetPlayer(id).player.Nickname,
                    Hp = playerState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue,
                    MaxHp = playerState.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue,
                    Mana = playerState.MemoryProperty[PropertyTypeEnum.Strength].CurrentValue,
                    MaxMana = playerState.MemoryProperty[PropertyTypeEnum.Strength].MaxCurrentValue,
                    Position = targetPlayer.transform.position,
                };
                list.Add(tracedPlayerInfo);
            }
            playerController.HandleTracedPlayerHp(headerConnectionId, list);
        }

        private void HandlePlayerTouchedBase(int headerConnectionId)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(headerConnectionId);
            if (!_playerInGameManager.TryPlayerRecoverHpInBase(headerConnectionId, out var isPlayerInHisBase))
            {
                Debug.Log($"PlayerPropertySyncSystem: Player {headerConnectionId} not found in base");
                return;
            }

            var recoverHpRatio = isPlayerInHisBase ? _gameConfigData.gameBaseData.playerBaseHpRecoverRatioPerSec : -_gameConfigData.gameBaseData.playerBaseHpRecoverRatioPerSec;
            var recoverMpRatio = isPlayerInHisBase ? _gameConfigData.gameBaseData.playerBaseManaRecoverRatioPerSec : -_gameConfigData.gameBaseData.playerBaseManaRecoverRatioPerSec;
            playerState.MemoryProperty[PropertyTypeEnum.Health] = playerState.MemoryProperty[PropertyTypeEnum.Health].UpdateCurrentValueByRatio(recoverHpRatio);
            playerState.MemoryProperty[PropertyTypeEnum.Strength] = playerState.MemoryProperty[PropertyTypeEnum.Strength].UpdateCurrentValueByRatio(recoverMpRatio);
            
            PropertyStates[headerConnectionId] = playerState;
            PropertyChange(headerConnectionId);
        }

        private void HandleGoldChanged(int headerConnectionId, float gold)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(headerConnectionId);
            var propertyCalculator = playerState.MemoryProperty[PropertyTypeEnum.Gold];
            propertyCalculator = propertyCalculator.UpdateCurrentValue(gold);
            playerState.MemoryProperty[PropertyTypeEnum.Gold] = propertyCalculator;
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

        private void HandleBuff(int targetId, BuffExtraData buffExtraData, int? casterId = null, BuffSourceType buffSourceType = BuffSourceType.None)
        {
            var allPlayers = new HashSet<int> { targetId };
            if (GameSyncManager.isRandomUnionStart && buffSourceType is BuffSourceType.Collect or BuffSourceType.Consume)
            {
                var playerNetId = _playerInGameManager.GetPlayerNetId(targetId);
                if (_playerInGameManager.TryGetOtherPlayersInUnion(playerNetId, out var otherPlayers))
                {
                    foreach (var player in otherPlayers)
                    {
                        var playerConnectionId = _playerInGameManager.GetPlayerId(player);
                        allPlayers.Add(playerConnectionId);
                    }
                }
            }

            foreach (var player in allPlayers)
            {
                AddBuffToPlayer(player, buffExtraData, casterId);
            }
        }

        private void HandleTimedBuff(int targetId, int buffConfigId, bool giveAlly = false)
        {
            var allPlayers = new HashSet<int> { targetId };
            if (GameSyncManager.isRandomUnionStart && giveAlly)
            {
                var playerNetId = _playerInGameManager.GetPlayerNetId(targetId);
                if (_playerInGameManager.TryGetOtherPlayersInUnion(playerNetId, out var otherPlayers))
                {
                    foreach (var player in otherPlayers)
                    {
                        var playerConnectionId = _playerInGameManager.GetPlayerId(player);
                        allPlayers.Add(playerConnectionId);
                    }
                }
            }
            foreach (var player in allPlayers)
            {
                AddTimedBuffToPlayer(player, buffConfigId);
            }
        }

        private void AddTimedBuffToPlayer(int player, int buffConfigId)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(player);
            var buffData = _timedBuffConfig.GetTimedBuffData(buffConfigId);
            var playerConnection = GameSyncManager.GetPlayerConnection(player);
            playerConnection?.RpcPlayEffect(buffData.playerEffectType);
            var newBuff = new TimedBuffData
            {
                targetPlayerId = player,
                buffId = buffConfigId,
                propertyType = buffData.propertyType,
                duration = buffData.duration.max,
                increaseValue = buffData.increaseRange.min,
                increaseType =  buffData.increaseType,
                operationType = buffData.operationType,
                sourceType = buffData.sourceType,
                isPermanent = buffData.isPermanent,
                playerEffectType = buffData.playerEffectType,
            };
            AddTimedBuff(player, playerState, newBuff);
        }

        private void AddTimedBuff(int player, PlayerPredictablePropertyState playerState, TimedBuffData buff)
        {
            _timedBuffs = _timedBuffs.Add(buff);
            var propertyCalculator = playerState.MemoryProperty[buff.propertyType];
            playerState.MemoryProperty[buff.propertyType] = propertyCalculator.UpdateCalculator(propertyCalculator, new BuffIncreaseData
            {
                increaseValue = buff.increaseValue,
                increaseType = buff.increaseType,
            });
            PropertyStates[player] = playerState;
            PropertyChange(player);
        }

        private void ModifyPlayerTimedBuff(TimedBuffData oldBuff, int oldIndex, TimedBuffData newBuff)
        {
            HandleTimedBuffRemove(oldBuff, oldIndex);
            var state = GetState<PlayerPredictablePropertyState>(oldBuff.targetPlayerId);
            AddTimedBuff(oldBuff.targetPlayerId, state, newBuff);
        }

        private (BuffBase, int) AddBuffToPlayer(int targetId, BuffExtraData buffExtraData, int? casterId = null)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(targetId);
            var buff = buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(buffExtraData) : _randomBuffConfig.GetBuff(buffExtraData);
            var newBuff = new BuffBase(buff, targetId, casterId);
            var buffManagerData = new BuffManagerData
            {
                BuffData = newBuff,
            };
            var propertyCalculator = playerState.MemoryProperty[newBuff.BuffData.propertyType];
            var preHealth = playerState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue;
            var preMana = playerState.MemoryProperty[PropertyTypeEnum.Strength].CurrentValue;
            playerState.MemoryProperty[newBuff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, newBuff);
            _activeBuffs = _activeBuffs.Add(buffManagerData);
            var index = _activeBuffs.Count - 1;
            PropertyStates[targetId] = playerState;
            var changedHp = playerState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue - preHealth;
            var changedMp = playerState.MemoryProperty[PropertyTypeEnum.Strength].CurrentValue - preMana;
            var maxHp = playerState.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue;
            var maxMana = playerState.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue;
            HandlePlayerPropertyDifference(targetId, propertyCalculator, playerState.MemoryProperty[newBuff.BuffData.propertyType], newBuff.BuffData.propertyType);
            PropertyChange(targetId);
            if (changedHp > 0)
            {
                var hpChangedCheckerParameters = HpChangeCheckerParameters.CreateParameters(
                    TriggerType.OnHpChange, changedHp / maxHp);
                GameSyncManager.EnqueueServerCommand(new TriggerCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(targetId, CommandType.Equipment),
                    TriggerType = TriggerType.OnHpChange,
                    TriggerData = NetworkCommandExtensions.SerializeBattleCondition(hpChangedCheckerParameters),
                });
            }

            if (changedMp > 0)
            {
                var mpChangedCheckerParameters = MpChangeCheckerParameters.CreateParameters(
                    TriggerType.OnManaChange, changedMp / maxMana);
                GameSyncManager.EnqueueServerCommand(new TriggerCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(targetId, CommandType.Equipment),
                    TriggerType = TriggerType.OnManaChange,
                    TriggerData = NetworkCommandExtensions.SerializeBattleCondition(mpChangedCheckerParameters),
                });
            }
            var playerConnection = GameSyncManager.GetPlayerConnection(targetId);
            playerConnection?.RpcPlayEffect(buff.playerEffectType);
            return (newBuff, index);
        }

        private void HandlePlayerPropertyDifference(int targetId, PropertyCalculator oldCalculator, PropertyCalculator newCalculator, PropertyTypeEnum propertyType)
        {
            var difference = PropertyCalculator.GetDifferences(oldCalculator, newCalculator);
            if (difference.Count == 0)
            {
                return;
            }
            var currentValueDifference = difference.FirstOrDefault(x => x.Item1 == BuffIncreaseType.Current);
            if (currentValueDifference.Item2 != 0)
            {
                var playerController = GameSyncManager.GetPlayerConnection(targetId);
                var tracedPlayerInfo = new TracedPlayerInfo
                {
                    PlayerId = targetId,
                    PlayerName = _playerInGameManager.GetPlayer(targetId).player.Nickname,
                    Hp = newCalculator.CurrentValue,
                    MaxHp = newCalculator.MaxCurrentValue,
                    Mana = newCalculator.CurrentValue,
                    MaxMana = newCalculator.MaxCurrentValue,
                    Position = playerController.transform.position,
                    PropertyDifferentPropertyType = propertyType,
                    PropertyDifferentValue = currentValueDifference.Item2,
                };
                playerController.HandlePlayerPropertyDifference(MemoryPackSerializer.Serialize(tracedPlayerInfo));
            }
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
            var propertyCalculator = playerState.MemoryProperty[newBuff.BuffData.propertyType];
            playerState.MemoryProperty[newBuff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, newBuff);
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
            var propertyCalculator = playerState.MemoryProperty[newBuff.BuffData.propertyType];
            playerState.MemoryProperty[newBuff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, newBuff);
            _passiveBuffs = _passiveBuffs.Add(buffManagerData);
            PropertyStates[targetId] = playerState;
            PropertyChange(targetId);
        }

        private void HandleBuffRemove(BuffBase buff, int index)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(buff.TargetPlayerId);
            var propertyCalculator = playerState.MemoryProperty[buff.BuffData.propertyType];
            for (var i = 0; i < buff.BuffData.increaseDataList.Count; i++)
            {
                var buffIncreaseData = buff.BuffData.increaseDataList[i];
                buffIncreaseData.operationType = BuffOperationType.Subtract;
                buff.BuffData.increaseDataList[i] = buffIncreaseData;
            }
            playerState.MemoryProperty[buff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, buff);
            _activeBuffs = _activeBuffs.RemoveAt(index);
            PropertyStates[buff.TargetPlayerId] = playerState;
            PropertyChange(buff.TargetPlayerId);
        }
        
        public void HandleTimedBuffRemove(TimedBuffData buff, int index = -1)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(buff.targetPlayerId);
            var propertyCalculator = playerState.MemoryProperty[buff.propertyType];
            var increaseData = new BuffIncreaseData
            {
                increaseValue = buff.increaseValue,
            };
            increaseData.operationType = buff.operationType switch
            {
                BuffOperationType.Add => BuffOperationType.Subtract,
                BuffOperationType.Subtract => BuffOperationType.Add,
                BuffOperationType.Multiply => BuffOperationType.Divide,
                BuffOperationType.Divide => BuffOperationType.Multiply,
                _ => throw new ArgumentOutOfRangeException()
            };
            playerState.MemoryProperty[buff.propertyType] = propertyCalculator.UpdateCalculator(propertyCalculator, increaseData);
            PropertyStates[buff.targetPlayerId] = playerState;
            PropertyChange(buff.targetPlayerId);
            if (index != -1)
                _timedBuffs = _timedBuffs.RemoveAt(index);
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
            var damageDatas = PlayerPropertyCalculator.HandleAttack(attacker, ref propertyState, ref defendersState, _jsonDataConfig.GetDamage);
            for (int i = 0; i < defenderPlayerIds.Length; i++)
            {
                PropertyStates[defenderPlayerIds[i]] = defendersState[defenderPlayerIds[i]];
                PropertyChange(defenderPlayerIds[i]);
                if (PropertyStates[defenderPlayerIds[i]] is PlayerPredictablePropertyState playerPropertyState &&
                    playerPropertyState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue <= 0)
                {
                    var deadManId = _playerInGameManager.GetPlayerNetId(defenderPlayerIds[i]);
                    var deadTime = _jsonDataConfig.GameConfig.GetPlayerDeathTime((int)defendersState[defenderPlayerIds[i]].MemoryProperty[PropertyTypeEnum.Score].CurrentValue);
                    if (!_playerInGameManager.TryAddDeathPlayer(deadManId, deadTime, attacker, OnPlayerDeath, OnPlayerRespawn))
                    {
                        Debug.LogError($"PlayerPropertySyncSystem: Failed to add death player {deadManId}");
                    }
                }
            }

            foreach (var damageData in damageDatas)
            {
                var attackHitCheckerParameters = AttackHitCheckerParameters.CreateParameters(TriggerType.OnAttackHit,
                    damageData.DamageRatio, damageData.DamageCalculateResult.Damage, AttackRangeType.None);
                GameSyncManager.EnqueueServerCommand(new TriggerCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(attacker, CommandType.Equipment),
                    TriggerType = TriggerType.OnAttackHit,
                    TriggerData = NetworkCommandExtensions.SerializeBattleCondition(attackHitCheckerParameters),
                });
                if (damageData.DamageCalculateResult.IsCritical)
                {
                    var criticalHitCheckerParameters = CriticalHitCheckerParameters.CreateParameters(
                        TriggerType.OnCriticalHit,
                        damageData.DamageRatio, damageData.DamageCalculateResult.Damage, DamageType.Physical);
                    
                    GameSyncManager.EnqueueServerCommand(new TriggerCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(attacker, CommandType.Equipment),
                        TriggerType = TriggerType.OnCriticalHit,
                        TriggerData = NetworkCommandExtensions.SerializeBattleCondition(criticalHitCheckerParameters),
                    });

                }

                if (damageData.IsDead)
                {
                    var deadManId = damageData.Defender;
                    var hitterPlayerId = damageData.Hitter;
                    var killCheckerParameters = KillCheckerParameters.CreateParameters(TriggerType.OnKill);
                    var deathCheckerParameters = DeathCheckerParameters.CreateParameters(TriggerType.OnDeath);
                    
                    GameSyncManager.EnqueueServerCommand(new TriggerCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(deadManId, CommandType.Equipment),
                        TriggerType = TriggerType.OnDeath,
                        TriggerData = NetworkCommandExtensions.SerializeBattleCondition(deathCheckerParameters),
                    });

                    GameSyncManager.EnqueueServerCommand(new TriggerCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(hitterPlayerId, CommandType.Equipment),
                        TriggerType = TriggerType.OnKill,
                        TriggerData = NetworkCommandExtensions.SerializeBattleCondition(killCheckerParameters),
                    });
                }

                if (damageData.DamageCalculateResult.Damage > 0 && !damageData.IsDead)
                {
                    var takeDamageCheckerParameters = TakeDamageCheckerParameters.CreateParameters(
                        TriggerType.OnTakeDamage, DamageType.Physical, 
                        damageData.HpRemainRatio, damageData.DamageRatio);
                    
                    var hpChangedCheckerParameters = HpChangeCheckerParameters.CreateParameters(
                        TriggerType.OnHpChange, damageData.DamageRatio);
                    
                    GameSyncManager.EnqueueServerCommand(new TriggerCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(damageData.Defender, CommandType.Equipment),
                        TriggerType = TriggerType.OnHpChange,
                        TriggerData = NetworkCommandExtensions.SerializeBattleCondition(hpChangedCheckerParameters),
                    });
                    
                    GameSyncManager.EnqueueServerCommand(new TriggerCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(damageData.Defender, CommandType.Equipment),
                        TriggerType = TriggerType.OnTakeDamage,
                        TriggerData = NetworkCommandExtensions.SerializeBattleCondition(takeDamageCheckerParameters),
                    });
                }

                if (damageData.IsDodged)
                {
                    var dodgeCheckerParameters = DodgeCheckerParameters.CreateParameters(TriggerType.OnDodge);
                    GameSyncManager.EnqueueServerCommand(new TriggerCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(damageData.Defender, CommandType.Equipment),
                        TriggerType = TriggerType.OnDodge,
                        TriggerData = NetworkCommandExtensions.SerializeBattleCondition(dodgeCheckerParameters),
                    });
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
            var killerPlayer = GameSyncManager.GetPlayerConnection(killerId);
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

        private void HandleSkill(int attacker, int skillConfigId, int[] hitPlayerIds)
        {
            var skillData = _skillConfig.GetSkillData(skillConfigId);
            var playerNetId = _playerInGameManager.GetPlayerNetId(attacker);
            var effectData = skillData.extraEffects;
            for (int i = 0; i < hitPlayerIds.Length; i++)
            {
                var hitPlayerId = hitPlayerIds[i];
                var otherPlayerNetId = _playerInGameManager.GetPlayerNetId(attacker);
                var isAlly = _playerInGameManager.IsOtherPlayerAlly(playerNetId, otherPlayerNetId);
                if (skillData.conditionTarget == ConditionTargetType.Ally && isAlly)
                {
                    for (int j = 0; j < effectData.Length; j++)
                    {
                        var effect = effectData[j];
                        HandleSkillHit(attacker, effect, hitPlayerId, true);
                    }
                }
                else if (skillData.conditionTarget == ConditionTargetType.Enemy && !isAlly)
                {
                    for (int j = 0; j < effectData.Length; j++)
                    {
                        var hitPlayerState = GetState<PlayerPredictablePropertyState>(hitPlayerId);
                        var preHealth = hitPlayerState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue;
                        var effect = effectData[j];
                        HandleSkillHit(attacker, effect, hitPlayerId, false);
                        if (effect.effectProperty == PropertyTypeEnum.Health)
                        {
                            var changedHp = hitPlayerState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue - preHealth;
                            var maxHp = hitPlayerState.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue;
                            var currentHp = hitPlayerState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue;
                            var skillHitData = SkillHitCheckerParameters.CreateParameters(TriggerType.OnSkillHit,
                                changedHp, skillData.skillType, currentHp / maxHp);
                            GameSyncManager.EnqueueServerCommand(new TriggerCommand
                            {
                                Header = GameSyncManager.CreateNetworkCommandHeader(attacker, CommandType.Equipment),
                                TriggerType = TriggerType.OnSkillHit,
                                TriggerData = NetworkCommandExtensions.SerializeBattleCondition(skillHitData),
                            });
                        }
                    }
                }
            }
        }

        private void HandleSkillHit(int attacker, SkillHitExtraEffectData skillHitExtraEffectData, int hitPlayerId, bool isAlly)
        {
            var hitPlayerState = GetState<PlayerPredictablePropertyState>(hitPlayerId);
            if(!isAlly && skillHitExtraEffectData.effectProperty == PropertyTypeEnum.Health && hitPlayerState.SubjectedState == SubjectedStateType.IsInvisible)
                return;
            var playerState = GetState<PlayerPredictablePropertyState>(attacker);
            var propertyCalculator = hitPlayerState.MemoryProperty[skillHitExtraEffectData.effectProperty];
            var playerCalculator = playerState.MemoryProperty[skillHitExtraEffectData.buffProperty];
            float value;
            var preHealth = hitPlayerState.MemoryProperty[PropertyTypeEnum.Health].CurrentValue;
            if (skillHitExtraEffectData.baseValue < 1)
            {
                value = playerCalculator.CurrentValue * (skillHitExtraEffectData.baseValue + skillHitExtraEffectData.extraRatio);
            }
            else
            {
                value = skillHitExtraEffectData.baseValue + playerCalculator.CurrentValue * skillHitExtraEffectData.extraRatio;
            }
            var buffIncreaseData = new BuffIncreaseData
            {
                increaseValue = value,
                operationType = skillHitExtraEffectData.buffOperation,
                increaseType = skillHitExtraEffectData.buffIncreaseType,
            };
            propertyCalculator = propertyCalculator.UpdateCalculator(propertyCalculator, buffIncreaseData);
            if (skillHitExtraEffectData.duration > 0)
            {
                var data = new SkillBuffManagerData();
                data.playerId = hitPlayerId;
                data.value = value;
                data.duration = skillHitExtraEffectData.duration;
                data.operationType = skillHitExtraEffectData.buffOperation;
                data.increaseType = skillHitExtraEffectData.buffIncreaseType;
                data.propertyType = skillHitExtraEffectData.effectProperty;
                data.currentTime = skillHitExtraEffectData.duration;
                data.skillType = skillHitExtraEffectData.controlSkillType;
                _skillBuffs = _skillBuffs.Add(data);
            }
            hitPlayerState.MemoryProperty[skillHitExtraEffectData.effectProperty] = propertyCalculator;
            PropertyStates[hitPlayerId] = hitPlayerState;
            PropertyChange(hitPlayerId);
            HandlePlayerControl(hitPlayerId, skillHitExtraEffectData.controlSkillType);
        }

        private void HandlePlayerControl(int playerId, ControlSkillType controlSkillType)
        {
            var playerController = GameSyncManager.GetPlayerConnection(playerId);
            playerController.RpcPlayControlledEffect(controlSkillType);
        }

        protected override void OnBroadcastStateUpdate()
        {
            UpdateBuffs(GameSyncManager.TickSeconds);
            UpdateSkillBuffs(GameSyncManager.TickSeconds);
            _timeBuffTimer += GameSyncManager.TickSeconds;
            if (_timeBuffTimer >= 0.3f)
            {
                _timeBuffTimer = 0;
                UpdateTimedBuffs(GameSyncManager.TickSeconds);
            }

            base.OnBroadcastStateUpdate();
        }

        private void UpdateSkillBuffs(float deltaTime)
        {
            if (_skillBuffs.Count <= 0)
                return;
            for (var i = _skillBuffs.Count - 1; i >= 0; i--)
            {
                _skillBuffs = _skillBuffs.SetItem(i, _skillBuffs[i].Update(deltaTime));
                if (_skillBuffs[i].currentTime > _skillBuffs[i].duration)
                {
                    HandleSkillBuffRemove(_skillBuffs[i], i);
                }
            }
        }

        private void HandleSkillBuffRemove(SkillBuffManagerData buff, int index)
        {
            var playerState = GetState<PlayerPredictablePropertyState>(buff.playerId);
            var propertyCalculator = playerState.MemoryProperty[buff.propertyType];
            var buffOperationType = buff.operationType.GetNegativeOperationType();
            var buffIncreaseData = new BuffIncreaseData
            {
                increaseValue = buff.value,
                operationType = buffOperationType,
                increaseType = buff.increaseType,
            };
            playerState.MemoryProperty[buff.propertyType] = propertyCalculator.UpdateCalculator(propertyCalculator, buffIncreaseData);
            _skillBuffs = _skillBuffs.RemoveAt(index);
            PropertyStates[buff.playerId] = playerState;
            PropertyChange(buff.playerId);
            HandlePlayerControl(buff.playerId, ControlSkillType.None);
        }

        private void HandleAnimationCommand(int connectionId, AnimationState command, int skillId = -1)
        {
            float cost;
            if (skillId != -1)
            {
                var skillData = _skillConfig.GetSkillData(skillId);
                cost = skillData.cost;
            }
            else
            {
                cost = _animationConfig.GetPlayerAnimationCost(command);
            }
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
            
            var speed = playerState.MemoryProperty[PropertyTypeEnum.Speed];
            PlayerPropertyCalculator.UpdateSpeed(speed, isSprinting, hasInputMovement,
                environmentType);
            playerState.MemoryProperty[PropertyTypeEnum.Speed] = speed;
            playerController.HandleEnvironmentChange(ref playerState, hasInputMovement, environmentType, isSprinting);
            PropertyStates[connectionId] = playerState;
            PropertyChange(connectionId);
        }

        public Dictionary<PropertyTypeEnum, PropertyCalculator> GetPlayerProperty(int connectionId)
        {
            return GetState<PlayerPredictablePropertyState>(connectionId).MemoryProperty;
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
            return GetState<PlayerPredictablePropertyState>(connectionId).MemoryProperty[propertyType].CurrentValue;
        }
        
        public float GetPlayerMaxProperty(int connectionId, PropertyTypeEnum propertyType)
        {
            return GetState<PlayerPredictablePropertyState>(connectionId).MemoryProperty[propertyType].MaxCurrentValue;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _propertyPredictionStates[connectionId];
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
            _propertyPredictionStates.Clear();
        }


        public IEnumerable<(int, float)> GetPlayerPropertiesWithCondition(Func<PropertyCalculator, bool> condition, bool isCurrentValue = false)
        {
            foreach (var playerState in PropertyStates.Keys)
            {
                var value = PropertyStates[playerState];
                if (value is PlayerPredictablePropertyState playerPredictablePropertyState)
                {
                    foreach (var property in playerPredictablePropertyState.MemoryProperty.Keys)
                    {
                        var propertyValue = playerPredictablePropertyState.MemoryProperty[property];
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
        private struct SkillBuffManagerData
        {
            public int playerId;
            public float value;
            public float duration;
            public BuffOperationType operationType;
            public BuffIncreaseType increaseType;
            public float currentTime;
            public PropertyTypeEnum propertyType;
            public ControlSkillType skillType;
            
            public SkillBuffManagerData(int playerId, float value, float duration, BuffOperationType operationType,
                BuffIncreaseType increaseType, PropertyTypeEnum propertyType, ControlSkillType skillType)
            {
                this.playerId = playerId;
                this.value = value;
                this.duration = duration;
                this.operationType = operationType;
                this.increaseType = increaseType;
                currentTime = duration;
                this.propertyType = propertyType;
                this.skillType = skillType;
            }

            public SkillBuffManagerData Update(float deltaTime)
            {
                currentTime -= deltaTime;
                return new SkillBuffManagerData
                {
                    playerId = playerId,
                    value = value,
                    duration = duration,
                    operationType = operationType,
                    increaseType = increaseType,
                    currentTime = currentTime,
                    propertyType = propertyType,
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

    [MemoryPackable]
    public partial struct TracedPlayerInfo
    {
        [MemoryPackOrder(0)]
        public int PlayerId;
        [MemoryPackOrder(1)]
        public string PlayerName;
        [MemoryPackOrder(2)]
        public Vector3 Position;
        [MemoryPackOrder(3)]
        public float Hp;
        [MemoryPackOrder(4)]
        public float MaxHp;
        [MemoryPackOrder(5)]
        public float Score;
        [MemoryPackOrder(6)]
        public float Mana;
        [MemoryPackOrder(7)]
        public float MaxMana;
        [MemoryPackOrder(8)]
        public PropertyTypeEnum PropertyDifferentPropertyType;
        [MemoryPackOrder(9)]
        public float PropertyDifferentValue;
    }
}