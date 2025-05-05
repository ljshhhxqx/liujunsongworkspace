using System;
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
        private JsonDataConfig _jsonDataConfig;
        private BindingKey _bindKey;
        private BindingKey _goldBindKey;
        private BindingKey _playerDeathTimeBindKey;
        private ReactiveDictionary<int, PropertyItemData> _uiPropertyData;
        private ReactiveProperty<GoldData> _goldData;

        public PlayerPredictablePropertyState PlayerPredictablePropertyState => (PlayerPredictablePropertyState)CurrentState;

        protected override CommandType CommandType => CommandType.Property;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            if (isLocalPlayer)
            {
                _bindKey = new BindingKey(UIPropertyDefine.PlayerProperty, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _goldBindKey = new BindingKey(UIPropertyDefine.PlayerBaseData, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _playerDeathTimeBindKey = new BindingKey(UIPropertyDefine.PlayerDeathTime, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _uiPropertyData = UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_bindKey);
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
        public event Action<SubjectedStateType> OnStateChanged;
        public event Action<float> OnPlayerDead; 
        public event Action OnPlayerRespawned; 
        
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
        
        private SubjectedStateType _subjectedStateType;

        private void PropertyChanged(PlayerPredictablePropertyState predictablePropertyState)
        {
            if (!_subjectedStateType.HasAnyState(SubjectedStateType.IsDead) && predictablePropertyState.SubjectedState.HasAnyState(SubjectedStateType.IsDead))
            {
                var countDown = _jsonDataConfig.GameConfig.GetPlayerDeathTime((int)predictablePropertyState.Properties[PropertyTypeEnum.Score].CurrentValue);
                OnPlayerDead?.Invoke(countDown);
                
            }
            else if (_subjectedStateType.HasAnyState(SubjectedStateType.IsDead) && !predictablePropertyState.SubjectedState.HasAnyState(SubjectedStateType.IsDead))
            {
                OnPlayerRespawned?.Invoke();
            }
            _subjectedStateType = predictablePropertyState.SubjectedState;
            OnStateChanged?.Invoke(predictablePropertyState.SubjectedState);
            var goldData = new GoldData();
            foreach (var key in predictablePropertyState.Properties.Keys)
            {
                var property = predictablePropertyState.Properties[key];
                var data = _uiPropertyData[(int)key];
                switch (key)
                {
                    case PropertyTypeEnum.Gold:
                        goldData.Gold = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Attack:
                        goldData.Attack = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Defense:
                        goldData.Health = property.CurrentValue;
                        goldData.MaxHealth = property.MaxCurrentValue;
                        break;
                    case PropertyTypeEnum.Speed:
                        goldData.Speed = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Experience:
                        goldData.Exp = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Score:
                        goldData.Score = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Strength:
                        goldData.Mana = property.CurrentValue;
                        goldData.MaxMana = property.MaxCurrentValue;
                        break;
                }
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
            UIPropertyBinder.SetProperty(_goldBindKey, goldData);
        }
    }
}