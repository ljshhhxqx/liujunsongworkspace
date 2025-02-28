using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Server.InGame;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using PlayerPropertyState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerPropertyState;
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
        private readonly List<BuffManagerData> _activeBuffs = new List<BuffManagerData>();
        private IConfigProvider _configProvider;
        private AnimationConfig _animationConfig;
        private ConstantBuffConfig _constantBuffConfig;
        private JsonDataConfig _jsonDataConfig;
        private RandomBuffConfig _randomBuffConfig;
        private PropertyConfig _propertyConfig;
        private PlayerInGameManager _playerInGameManager;

        [Inject]
        private void InitContainers(IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
            _configProvider = configProvider;
            _jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _animationConfig = _configProvider.GetConfig<AnimationConfig>();
            _constantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = configProvider.GetConfig<RandomBuffConfig>();
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
            _playerInGameManager = playerInGameManager;
            ConfigPlayerMinProperties = _propertyConfig.GetPlayerMinProperties();
            ConfigPlayerMaxProperties = _propertyConfig.GetPlayerMaxProperties();
            ConfigPlayerBaseProperties = _propertyConfig.GetPlayerBaseProperties();
            BuffDataReaderWriter.RegisterReaderWriter();
        }

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerPropertyState>>(state);
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
            var playerPredictableState = player.GetComponent<PropertyPredictionState>();
            var playerPropertyState = new PlayerPropertyState();
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
                _activeBuffs[i] = _activeBuffs[i].Update(deltaTime);
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
        public override IPropertyState ProcessCommand(INetworkCommand command)
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
            else
            {
                Debug.LogError($"PlayerPropertySyncSystem: Unknown command type {command.GetType().Name}");
            }
            return null;
        }

        //todo: 下面有关ProcessCommand的代码都需要由PlayerComponentController来处理实际的计算
        private void HandleInvincibleChanged(int headerConnectionId, bool isInvincible)
        {
            var playerState = GetState<PlayerPropertyState>(headerConnectionId);
            playerState.IsInvisible = isInvincible;
            PropertyStates[headerConnectionId] = playerState;
        }
        
        private void HandlePropertyRecover(int connectionId)
        {
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            var playerState = GetState<PlayerPropertyState>(connectionId);
            playerController.HandlePropertyRecover(ref playerState);
            PropertyStates[connectionId] = playerState;
        }

        private void HandleBuff(int targetId, BuffExtraData buffExtraData, int? casterId = null)
        {
            var playerState = GetState<PlayerPropertyState>(targetId);
            var buff = buffExtraData.buffType == BuffType.Constant ? _constantBuffConfig.GetBuff(buffExtraData) : _randomBuffConfig.GetBuff(buffExtraData);
            var newBuff = new BuffBase(buff, targetId, casterId);
            var buffManagerData = new BuffManagerData
            {
                BuffData = newBuff,
                Size = buffExtraData.collectObjectBuffSize,
            };
            var propertyCalculator = playerState.Properties[newBuff.BuffData.propertyType];
            playerState.Properties[newBuff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, newBuff);
            _activeBuffs.Add(buffManagerData);
            PropertyStates[targetId] = playerState;
        }

        private void HandleBuffRemove(BuffBase buff, int index)
        {
            var playerState = GetState<PlayerPropertyState>(buff.TargetPlayerId);
            var propertyCalculator = playerState.Properties[buff.BuffData.propertyType];
            for (var i = 0; i < buff.BuffData.increaseDataList.Count; i++)
            {
                var buffIncreaseData = buff.BuffData.increaseDataList[i];
                buffIncreaseData.operationType = BuffOperationType.Subtract;
                buff.BuffData.increaseDataList[i] = buffIncreaseData;
            }
            playerState.Properties[buff.BuffData.propertyType] = HandleBuffInfo(propertyCalculator, buff);
            _activeBuffs.RemoveAt(index);
        }


        private PropertyCalculator HandleBuffInfo(PropertyCalculator propertyCalculator, BuffBase buffData)
        {
            return propertyCalculator.UpdateCalculator(buffData.BuffData.increaseDataList);
        }

        private void HandlePlayerAttack(int attacker, int[] defenderPlayerIds)
        {
            var propertyState = GetState<PlayerPropertyState>(attacker);
            var defendersState = PropertyStates
                .Where(x => defenderPlayerIds.Contains(x.Key))
                .ToDictionary(x => x.Key, x => (PlayerPropertyState)x.Value);
            var playerController = GameSyncManager.GetPlayerConnection(attacker);
            playerController.HandleAttackProperty(ref propertyState, ref defendersState, _jsonDataConfig.GetDamage);
            for (int i = 0; i < defenderPlayerIds.Length; i++)
            {
                PropertyStates[defenderPlayerIds[i]] = defendersState[defenderPlayerIds[i]];
                if (PropertyStates[defenderPlayerIds[i]] is PlayerPropertyState playerPropertyState &&
                    playerPropertyState.Properties[PropertyTypeEnum.Health].CurrentValue <= 0)
                {
                    //todo: handle dead player logic
                }
            }
            PropertyStates[attacker] = propertyState;   
        }

        private void HandleSkill(int connectionId, int skillId)
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
            var playerState = GetState<PlayerPropertyState>(connectionId);
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            playerController.HandleAnimationCost(ref playerState, command, cost);
            PropertyStates[connectionId] = playerState;
        }

        private void HandleEnvironmentChange(int connectionId, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            var playerState = GetState<PlayerPropertyState>(connectionId);
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            playerController.HandleEnvironmentChange(ref playerState, hasInputMovement, environmentType, isSprinting);
            PropertyStates[connectionId] = playerState;
        }

        public Dictionary<PropertyTypeEnum, PropertyCalculator> GetPlayerProperty(int connectionId)
        {
            return GetState<PlayerPropertyState>(connectionId).Properties;
        }
        
        public float GetPlayerProperty(int connectionId, PropertyTypeEnum propertyType)
        {
            return GetState<PlayerPropertyState>(connectionId).Properties[propertyType].CurrentValue;
        }
        
        public float GetPlayerMaxProperty(int connectionId, PropertyTypeEnum propertyType)
        {
            return GetState<PlayerPropertyState>(connectionId).Properties[propertyType].MaxCurrentValue;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = PlayerPredictionState[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(IPropertyState oldState, IPropertyState newState)
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
            
        }

        [Serializable]
        private struct BuffManagerData
        {
            public BuffBase BuffData;
            public CollectObjectBuffSize Size;

            public BuffManagerData Update(float deltaTime)
            {
                return new BuffManagerData
                {
                    BuffData = BuffData.Update(deltaTime),
                    Size = Size
                };
            }
        }
    }
}