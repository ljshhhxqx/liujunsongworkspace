using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Buff;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem
{
    public class PlayerPropertySyncSystem : BaseSyncSystem, IPropertySyncSystem
    {
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerMinProperties { get; private set; }
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerMaxProperties { get; private set; }
        public Dictionary<PropertyTypeEnum, float> ConfigPlayerBaseProperties { get; private set; }
        public Dictionary<int, PropertyPredictionState> ConfigPlayerPredictionState { get; private set; }
        private IConfigProvider _configProvider;
        private BuffManager _buffManager;

        [Inject]
        private void InitContainers(IConfigProvider configProvider, BuffManager buffManager)
        {
            _configProvider = configProvider;
            _buffManager = buffManager;
            var jsonConfig = _configProvider.GetConfig<JsonDataConfig>();
            ConfigPlayerMinProperties = jsonConfig.GetPlayerMaxProperties();
            ConfigPlayerMaxProperties = jsonConfig.GetPlayerMaxProperties();
            ConfigPlayerBaseProperties = jsonConfig.GetPlayerBaseProperties();
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
                        HandleEnvironmentChange(header.connectionId, environmentChange.environmentType);
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
            health.UpdateCalculator(new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = healthRecover.CurrentValue * GameSyncManager.TickRate,
            });
            strength.UpdateCalculator(new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = strengthRecover.CurrentValue * GameSyncManager.TickRate,
            });
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

        private void HandleEnvironmentChange(int connectionId, PlayerEnvironmentState environmentType)
        {
            
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

        public void ModifyProperty(PropertyCommand command, NetworkIdentity identity)
        {
            if (!identity || !identity.isServer)
            {
                return;
            }
        }

        public void ProcessServerCommand(INetworkCommand command, NetworkIdentity identity)
        {
            if (command is PropertyCommand propertyCommand)
            {
                ModifyProperty(propertyCommand, identity);
            }
        }
    }
}