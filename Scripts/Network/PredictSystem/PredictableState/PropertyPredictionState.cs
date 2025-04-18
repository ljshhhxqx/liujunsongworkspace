﻿using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using UniRx;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using PropertyCalculator = HotUpdate.Scripts.Network.PredictSystem.State.PropertyCalculator;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PropertyPredictionState: PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        private AnimationConfig _animationConfig;
        private PropertyConfig _propertyConfig;
        private BindingKey _bindKey;
        private ReactiveDictionary<int, PropertyItemData> _uiPropertyData;

        public PlayerPredictablePropertyState PlayerPredictablePropertyState => (PlayerPredictablePropertyState)CurrentState;

        protected override CommandType CommandType => CommandType.Property;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
            if (isLocalPlayer)
            {
                _bindKey = new BindingKey(UIPropertyDefine.PlayerProperty, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                var itemDatas = new Dictionary<int, IUIDatabase>();
                var enumValues = Enum.GetValues(typeof(PropertyTypeEnum));
                for (var i = 0; i < enumValues.Length; i++)
                {
                    var propertyType = (PropertyTypeEnum)enumValues.GetValue(i);
                    var propertyConfig = _propertyConfig.GetPropertyConfigData(propertyType);
                    if (!propertyConfig.showInHud)
                    {
                        continue;
                    }

                    var displayName = propertyConfig.description;
                    var consumeType = propertyConfig.consumeType;
                    itemDatas.Add((int)propertyType, new PropertyItemData
                    {
                        Name = displayName,
                        PropertyType = propertyType,
                        CurrentProperty = 1,
                        MaxProperty = 1,
                        ConsumeType = consumeType
                    });
                }
                UIPropertyBinder.OptimizedBatchAdd(_bindKey, itemDatas);
            }
        }
        
        public float GetProperty(PropertyTypeEnum propertyType)
        {
            return PlayerPredictablePropertyState.Properties[propertyType].CurrentValue;
        }
        
        public float GetMaxProperty(PropertyTypeEnum propertyType)
        {
            if (PlayerPredictablePropertyState.Properties[propertyType].IsResourceProperty())
            {
                return PlayerPredictablePropertyState.Properties[propertyType].MaxCurrentValue;
            }
            return PlayerPredictablePropertyState.Properties[propertyType].CurrentValue;
        }

        public event Action<PropertyTypeEnum, PropertyCalculator> OnPropertyChanged;
        
        public override void ApplyServerState<T>(T state)
        {
            if (state is PlayerPredictablePropertyState propertyState)
            {
                base.ApplyServerState(propertyState);
                PropertyChanged(propertyState);
            }
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is null || state is not PlayerPredictablePropertyState propertyState)
                return false;
            return !PlayerPredictablePropertyState.IsEqual(propertyState);
        }

        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (CurrentState is not PlayerPredictablePropertyState playerState || header.CommandType.HasAnyState(CommandType.Property))
                return;
            if (command is PropertyAutoRecoverCommand)
            {
                HandlePropertyRecover(ref playerState);
            }
            else if (command is PropertyClientAnimationCommand clientAnimationCommand)
            {
                HandleAnimationCommand(ref playerState, clientAnimationCommand.AnimationState);
            }
            else
            {
                return;
            }
            var propertyState = PlayerPredictablePropertyState;
            PropertyChanged(propertyState);
            
        }
        
        private void HandlePropertyRecover(ref PlayerPredictablePropertyState propertyState)
        {
            PlayerComponentController.HandlePropertyRecover(ref propertyState);
            PropertyChanged(propertyState);
        }

        private void HandleAnimationCommand(ref PlayerPredictablePropertyState propertyState, AnimationState command)
        {
            var cost = _animationConfig.GetPlayerAnimationCost(command);
            if (cost <= 0)
            {
                return;
            }
            PlayerComponentController.HandleAnimationCost(ref propertyState, command, cost);
            PropertyChanged(propertyState);
        }

        public void RegisterProperties(PlayerPredictablePropertyState predictablePropertyState)
        {
            PropertyChanged(predictablePropertyState);
        }

        private void PropertyChanged(PlayerPredictablePropertyState predictablePropertyState)
        {
            _uiPropertyData ??= UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_bindKey);
            foreach (var key in predictablePropertyState.Properties.Keys)
            {
                var property = predictablePropertyState.Properties[key];
                var data = _uiPropertyData[(int)key];
                if (property.IsResourceProperty())
                {
                    OnPropertyChanged?.Invoke(key, property);
                    data.CurrentProperty = property.CurrentValue;
                    data.MaxProperty = property.MaxCurrentValue;
                    _uiPropertyData[(int)key] = data;
                    continue;
                }
                OnPropertyChanged?.Invoke(key, property);
                data.CurrentProperty = property.CurrentValue;
                _uiPropertyData[(int)key] = data;
                if (key == PropertyTypeEnum.AttackSpeed)
                {
                    PlayerComponentController.SetAnimatorSpeed(AnimationState.Attack, property.CurrentValue);
                }
            }
        }
    }
}