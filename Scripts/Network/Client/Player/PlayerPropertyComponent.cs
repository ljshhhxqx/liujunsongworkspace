using System;
using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerPropertyComponent : NetworkMonoComponent
    {
        private readonly Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>> _properties = new Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>>();
        private PlayerDataConfig _playerDataConfig;
        [SyncVar] 
        public AnimationState currentAnimationState;
        private readonly SyncDictionary<PropertyTypeEnum, PropertyType> _syncProperties = new SyncDictionary<PropertyTypeEnum, PropertyType>();
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            for (var i = (int)PropertyTypeEnum.Speed; i <= (int)PropertyTypeEnum.Score; i++)
            {
                var propertyType = (PropertyTypeEnum)i;
                var configProperty = _playerDataConfig.PlayerConfigData.MaxProperties.Find(x => x.TypeEnum == propertyType);
                if (configProperty.Value == 0)
                {
                    throw new Exception("Property value cannot be zero.");
                }

                var property = new PropertyType(propertyType, configProperty.Value);
                _properties.Add(propertyType, new ReactiveProperty<PropertyType>(property));
                if (isServer)
                {
                    _syncProperties.Add(propertyType, property);
                }
            }
        }

        public void IncreaseProperty(PropertyTypeEnum type, float amount)
        {
            if (_syncProperties.TryGetValue(type, out var property))
            {
                property.IncreaseValue(amount);
                PropertyChanged(property);
            }
        }

        private void PropertyChanged(PropertyType property)
        {
            _properties[property.TypeEnum].Value = property;
            _properties[property.TypeEnum].SetValueAndForceNotify(property); 
        }
        
        public PropertyType GetProperty(PropertyTypeEnum type)
        {
            return _syncProperties.TryGetValue(type, out var property) ? property : default;
        }

        public bool StrengthCanDoAnimation(AnimationState animationState)
        {
            var strength = GetProperty(PropertyTypeEnum.Strength);
            if (_playerDataConfig.PlayerConfigData.AnimationStrengthCosts.TryGetValue(animationState, out var animationCost))
            {
                return strength.Value >= animationCost;
            }
            Debug.LogError($"Animation {animationState} not found in config.");
            return strength.Value > 0f;
        }

        public bool DoAnimation(AnimationState animationState)
        {
            if (StrengthCanDoAnimation(animationState))
            {
                var strength = GetProperty(PropertyTypeEnum.Strength);
                strength.IncreaseValue(-_playerDataConfig.PlayerConfigData.AnimationStrengthCosts[animationState]);
                PropertyChanged(strength);
                return true;
            }
            return false;
        }

        private void FixedUpdate()
        {
            if (isServer)
            {
                var strength = GetProperty(PropertyTypeEnum.Strength);
                var recoveredStrength = _playerDataConfig.PlayerConfigData.StrengthRecoveryPerSecond;
                if (currentAnimationState == AnimationState.Sprint)
                {
                    recoveredStrength -= _playerDataConfig.PlayerConfigData.AnimationStrengthCosts[AnimationState.Sprint];
                }
                strength.IncreaseValue(recoveredStrength * Time.fixedDeltaTime);
                PropertyChanged(strength);
            }
        }
    }
}