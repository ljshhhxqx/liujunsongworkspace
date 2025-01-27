using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem
{
    public class PlayerPropertySyncSystem : BaseSyncSystem
    {
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerMinProperties { get; private set; }
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerMaxProperties { get; private set; }
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerBaseProperties { get; private set; }
        public Dictionary<int, PropertyPredictionState> ConfigPlayerPredictionState { get; private set; }
        private IConfigProvider _configProvider;
        private AnimationConfig _animationConfig;
        private BuffManager _buffManager;

        [Inject]
        private void InitContainers(IConfigProvider configProvider, BuffManager buffManager)
        {
            _configProvider = configProvider;
            _buffManager = buffManager;
            _buffManager.OnServerBuffRemoved += HandleServerBuffRemoved;
            var jsonConfig = _configProvider.GetConfig<JsonDataConfig>();
            _animationConfig = _configProvider.GetConfig<AnimationConfig>();
            ConfigPlayerMinProperties = jsonConfig.GetPlayerMaxProperties();
            ConfigPlayerMaxProperties = jsonConfig.GetPlayerMaxProperties();
            ConfigPlayerBaseProperties = jsonConfig.GetPlayerBaseProperties();
        }

        private void HandleServerBuffRemoved(int connectionId, List<BuffIncreaseData> arg2)
        {
            
        }

        protected override void OnClientProcessStateUpdate(string stateJson)
        {
            var playerStates = JsonConvert.DeserializeObject<Dictionary<int, PlayerPropertyState>>(stateJson);
            foreach (var playerState in playerStates)
            {
                if (!PropertyStates.ContainsKey(playerState.Key))
                {
                    continue;
                }
                PropertyStates[playerState.Key] = playerState.Value;
                SetState(playerState.Key, PropertyStates[playerState.Key]);
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PropertyPredictionState>();
            var playerPropertyState = new PlayerPropertyState();
            playerPropertyState.Properties = GetPropertyCalculators();
            PropertyStates.Add(connectionId, playerPropertyState);
            ConfigPlayerPredictionState.Add(connectionId, playerPredictableState);
        }

        private Dictionary<PropertyTypeEnum, PropertyCalculator> GetPropertyCalculators()
        {
            var dictionary = new Dictionary<PropertyTypeEnum, PropertyCalculator>();
            var enumValues = (PropertyTypeEnum[])Enum.GetValues(typeof(PropertyTypeEnum));
            foreach (var propertyType in enumValues)
            {
                var propertyData = new PropertyCalculator.PropertyData();
                propertyData.baseValue = ConfigPlayerBaseProperties[propertyType];
                propertyData.additive = 0;
                propertyData.multiplier = 1;
                propertyData.correction = 1;
                propertyData.currentValue = propertyData.baseValue;
                propertyData.maxCurrentValue = propertyData.baseValue;
                var calculator = new PropertyCalculator(propertyType, propertyData,ConfigPlayerMinProperties[propertyType], ConfigPlayerMaxProperties[propertyType]);
                dictionary.Add(propertyType, calculator);
            }
            return dictionary;
        }

        public override CommandType HandledCommandType => CommandType.Property;
        public override IPropertyState ProcessCommand(INetworkCommand command, NetworkIdentity identity)
        {
            if (command is PropertyCommand propertyCommand)
            {
                var header = propertyCommand.GetHeader();
                switch (propertyCommand.Operation)
                {
                    case PropertyCommandAutoRecover:
                        if (identity.isServer)
                        {
                            return null;
                        }
                        HandlePropertyRecover(header.connectionId);
                        break;
                    case PropertyCommandEnvironmentChange environmentChange:
                        if (identity.isServer)
                        {
                            return null;
                        }
                        HandleEnvironmentChange(header.connectionId, environmentChange.hasInputMovement, environmentChange.environmentType, environmentChange.isSprinting);
                        break;
                    case PropertyAnimationCommand animationCommand:
                        if (!identity.isServer)
                        {
                            return null;
                        }
                        HandleAnimationCommand(header.connectionId, animationCommand.animationState);
                        break;
                    case PropertyCommandBuff buff:
                        if (!identity.isServer)
                        {
                            return null;
                        }
                        HandleBuff(header.connectionId, buff.buffId, buff.buffType);
                        break;
                    case PropertyCommandAttack attack:
                        if (!identity.isServer)
                        {
                            return null;
                        }
                        HandleAttack(header.connectionId, attack.targetIds);
                        break;
                    case PropertyCommandSkill skill:
                        if (!identity.isServer)
                        {
                            return null;
                        }
                        HandleSkill(header.connectionId, skill.skillId);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                return null;
            }
            Debug.LogError($"PlayerPropertySyncSystem: Unknown command type {command.GetType().Name}");

            return null;
        }

        private void HandlePropertyRecover(int connectionId)
        {
            var playerState = GetState<PlayerPropertyState>(connectionId);
            var healthRecover = playerState.Properties[PropertyTypeEnum.HealthRecovery];
            var strengthRecover = playerState.Properties[PropertyTypeEnum.StrengthRecovery];
            var health = playerState.Properties[PropertyTypeEnum.Health];
            var strength = playerState.Properties[PropertyTypeEnum.Strength];
            playerState.Properties[PropertyTypeEnum.Health] = health.UpdateCalculator(health, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = healthRecover.CurrentValue * GameSyncManager.TickRate,
            });
            playerState.Properties[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = strengthRecover.CurrentValue * GameSyncManager.TickRate,
            });
            PropertyStates[connectionId] = playerState;
        }

        private void HandleBuff(int connectionId, int buffId, BuffType buffType)
        {
            
        }
        
        private void HandleAttack(int connectionId, int[] attackIds)
        {
            
        }
        
        private void HandleSkill(int connectionId, int skillId)
        {
            
        }

        private void HandleAnimationCommand(int connectionId, AnimationState command)
        {
            var cost = _animationConfig.GetPlayerAnimationCost(command);
            if (cost <= 0)
            {
                return;
            }
            var playerState = GetState<PlayerPropertyState>(connectionId);
            cost *= command == AnimationState.Sprint ? GameSyncManager.TickRate : 1f;
            var strength = playerState.Properties[PropertyTypeEnum.Strength];
            if (cost > strength.CurrentValue)
            {
                Debug.LogError($"PlayerPropertySyncSystem: {connectionId} does not have enough strength to perform {command} animation.");
                return;
            }
            playerState.Properties[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = cost,
                operationType = BuffOperationType.Subtract,
            });
            PropertyStates[connectionId] = playerState;
        }

        private void HandleEnvironmentChange(int connectionId, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            var playerState = GetState<PlayerPropertyState>(connectionId);
            var speed = playerState.Properties[PropertyTypeEnum.Speed];
            var sprintRatio = playerState.Properties[PropertyTypeEnum.SprintSpeedRatio];
            var stairsRatio = playerState.Properties[PropertyTypeEnum.StairsSpeedRatio];
            if (!hasInputMovement)
            {
                speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                {
                    increaseType = BuffIncreaseType.CorrectionFactor,
                    increaseValue = 0,
                });
            }
            else
            {
                switch (environmentType)
                {
                    case PlayerEnvironmentState.InAir:
                        break;
                    case PlayerEnvironmentState.OnGround:
                        speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                        {
                            increaseType = BuffIncreaseType.CorrectionFactor,
                            increaseValue = isSprinting ? sprintRatio.CurrentValue : 1,
                            operationType = BuffOperationType.Multiply,
                        });
                        break;
                    case PlayerEnvironmentState.OnStairs:
                        speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                        {
                            increaseType = BuffIncreaseType.CorrectionFactor,
                            increaseValue = isSprinting ? sprintRatio.CurrentValue * stairsRatio.CurrentValue : stairsRatio.CurrentValue,
                            operationType = BuffOperationType.Multiply,
                        });
                        break;
                    case PlayerEnvironmentState.Swimming:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(environmentType), environmentType, null);
                }
            }
            playerState.Properties[PropertyTypeEnum.Speed] = speed;
            PropertyStates[connectionId] = playerState;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = ConfigPlayerPredictionState[connectionId];
            playerPredictableState.SetServerState(state);
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
    }
}