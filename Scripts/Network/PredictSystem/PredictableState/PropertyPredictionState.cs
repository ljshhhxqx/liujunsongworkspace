﻿using System;
using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
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
        private UIManager _uiManager;
        private BindingKey _bindKey;
        private BindingKey _goldBindKey;
        private BindingKey _playerDeathTimeBindKey;
        private BindingKey _playerControlBindKey;
        private BindingKey _propertyBindKey;
        private ReactiveDictionary<int, PropertyItemData> _uiPropertyData = new ReactiveDictionary<int, PropertyItemData>();
        private ReactiveProperty<ValuePropertyData> _goldData;

        public PlayerPredictablePropertyState PlayerPredictablePropertyState => (PlayerPredictablePropertyState)CurrentState;

        protected override CommandType CommandType => CommandType.Property;

        
        [Inject]
        protected void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider, UIManager uiManager)
        {
            base.Init(gameSyncManager, configProvider);
            _uiManager = uiManager;
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _propertyConfig = configProvider.GetConfig<PropertyConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            if (NetworkIdentity.isLocalPlayer)
            {
                //Debug.Log($"PropertyPredictionState [OnStartLocalPlayer]  ");
                _propertyBindKey = new BindingKey(UIPropertyDefine.PlayerProperty, DataScope.LocalPlayer,
                    UIPropertyBinder.LocalPlayerId);
                _bindKey = new BindingKey(UIPropertyDefine.PlayerProperty, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _goldBindKey = new BindingKey(UIPropertyDefine.PlayerBaseData, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _playerDeathTimeBindKey = new BindingKey(UIPropertyDefine.PlayerDeathTime, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _playerControlBindKey = new BindingKey(UIPropertyDefine.PlayerControl, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _uiPropertyData = UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_bindKey);
                var itemDatas = new Dictionary<int, PropertyItemData>();
                var enumValues = Enum.GetValues(typeof(PropertyTypeEnum));
                for (var i = 0; i < enumValues.Length; i++)
                {
                    var propertyType = (PropertyTypeEnum)enumValues.GetValue(i);
                    if (propertyType == PropertyTypeEnum.None)
                    {
                        continue;
                    }
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
                var playerPropertiesOverlay = _uiManager.SwitchUI<PlayerPropertiesOverlay>();
                playerPropertiesOverlay.BindPlayerProperty(UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_propertyBindKey));
            }
        }

        public float GetProperty(PropertyTypeEnum propertyType)
        {
            return PlayerPredictablePropertyState.MemoryProperty[propertyType].CurrentValue;
        }

        public PropertyCalculator GetCalculator(PropertyTypeEnum propertyType)
        {
            return PlayerPredictablePropertyState.MemoryProperty[propertyType];
        }

        public float GetMoveSpeed() => PlayerPredictablePropertyState.PlayerState.CurrentMoveSpeed;
        
        public float GetMaxProperty(PropertyTypeEnum propertyType)
        {
            if (PlayerPredictablePropertyState.MemoryProperty[propertyType].IsResourceProperty())
            {
                return PlayerPredictablePropertyState.MemoryProperty[propertyType].MaxCurrentValue;
            }
            return PlayerPredictablePropertyState.MemoryProperty[propertyType].CurrentValue;
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
                //Debug.Log($"PropertyPredictionState [ApplyServerState] {propertyState.ToString()}");
                PropertyChanged(propertyState);
            }
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is null || state is not PlayerPredictablePropertyState propertyState || CurrentState is null || CurrentState is not PlayerPredictablePropertyState currentState)
                return false;
            return !propertyState.IsEqual(currentState);
        }
        private float _timer;
        private int _frameCount;

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
                // if (clientAnimationCommand.AnimationState == AnimationState.Sprint)
                // {
                //     _timer+=Time.fixedDeltaTime;
                //     _frameCount++;
                //     if (_timer >= 1f)
                //     {
                //         Debug.Log($"[PropertyClientAnimationCommand] 理论frameCount => {1/Time.fixedDeltaTime} 实际frameCount => {_frameCount}");
                //         
                //         _timer = 0;
                //         _frameCount = 0;
                //     }
                // }
                HandleAnimationCommand(ref playerState, clientAnimationCommand);
            }
            else if(command is PropertyEnvironmentChangeCommand environmentChangeCommand)
            {
                HandleEnvironmentChangeCommand(ref playerState, environmentChangeCommand);
            }
            
            //Debug.Log($"PropertyPredictionState [Simulate] {command.GetCommandType()}");
            var propertyState = playerState;
            CurrentState = propertyState;
            PropertyChanged(propertyState);
            
        }

        private void HandleEnvironmentChangeCommand(ref PlayerPredictablePropertyState playerState, PropertyEnvironmentChangeCommand environmentChangeCommand)
        {
            PlayerPropertyCalculator.UpdateSpeed(ref playerState, environmentChangeCommand.IsSprinting, environmentChangeCommand.HasInputMovement,
                environmentChangeCommand.PlayerEnvironmentState);
            CurrentState = playerState;
            PropertyChanged(playerState);
        }

        private void HandlePropertyRecover(ref PlayerPredictablePropertyState propertyState)
        {
            PlayerComponentController.HandlePropertyRecover(ref propertyState);
            PropertyChanged(propertyState);
        }

        private void HandleAnimationCommand(ref PlayerPredictablePropertyState propertyState, PropertyClientAnimationCommand command)
        {
            //Debug.Log($"[HandleAnimationCommand] {command} [{connectionId}] {skillId}");
            var cost = _animationConfig.GetPlayerAnimationCost(command.AnimationState);
            
            if (cost <= 0)
            {
                return;
            }
            PlayerComponentController.HandleAnimationCost(ref propertyState, command.AnimationState, cost);
            PropertyChanged(propertyState);
        }

        public void RegisterProperties(PlayerPredictablePropertyState predictablePropertyState)
        {
            //PropertyChanged(predictablePropertyState);
        }
        
        private SubjectedStateType _subjectedStateType;

        private bool _isDead;

        private void PropertyChanged(PlayerPredictablePropertyState predictablePropertyState)
        {
            // foreach (var key in predictablePropertyState.MemoryProperty.Keys)
            // {
            //     var property = PlayerPredictablePropertyState.MemoryProperty[key];
            //     //Debug.Log($"PropertyChanged {key}: {property}");
            // }
            //Debug.Log($"[PropertyChanged] {predictablePropertyState.ToString()}");
            if (!NetworkIdentity.isLocalPlayer || _isDead)
            {
                Debug.LogError($"PropertyChanged {predictablePropertyState.ToString()} {!NetworkIdentity.isLocalPlayer} is not a player or is dead {_isDead}");
                return;
            }
            if (!_isDead && !_subjectedStateType.HasAnyState(SubjectedStateType.IsDead) && predictablePropertyState.SubjectedState.HasAnyState(SubjectedStateType.IsDead))
            {
                var countDown = _jsonDataConfig.GameConfig.GetPlayerDeathTime((int)predictablePropertyState.MemoryProperty[PropertyTypeEnum.Score].CurrentValue);
                OnPlayerDead?.Invoke(countDown);
                //Debug.Log($"OnPlayerDead {countDown}");
                _isDead = true;
                return;
            }
            else if (_subjectedStateType.HasAnyState(SubjectedStateType.IsDead) && !predictablePropertyState.SubjectedState.HasAnyState(SubjectedStateType.IsDead))
            {
                OnPlayerRespawned?.Invoke();
            }
            _subjectedStateType = predictablePropertyState.SubjectedState;
            OnStateChanged?.Invoke(predictablePropertyState.SubjectedState);
            var goldData = ObjectPoolManager<ValuePropertyData>.Instance.Get(15);
            var uiPropertyData = UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_propertyBindKey);
            foreach (var kvp in predictablePropertyState.MemoryProperty)
            {
                var property = kvp.Value;
                //Debug.Log($"PropertyChanged {kvp}: {property.CurrentValue} {property.MaxCurrentValue}");
                uiPropertyData.TryGetValue((int)kvp.Key, out var data);
                switch (kvp.Key)
                {
                    case PropertyTypeEnum.Gold:
                        goldData.Gold = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Attack:
                        goldData.Attack = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Health:
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
                        //Debug.Log($"goldData.MaxMana: {goldData.MaxMana} goldData.Mana: {goldData.Mana}");
                        break;
                    case PropertyTypeEnum.View:
                        goldData.Fov = property.CurrentValue;
                        break;
                    case PropertyTypeEnum.Alpha:
                        goldData.Alpha = property.CurrentValue;
                        break;
                }
                if (property.IsResourceProperty())
                {
                    OnPropertyChanged?.Invoke(kvp.Key, property);
                    if (data.ConsumeType!= PropertyConsumeType.None)
                    {
                        data.CurrentProperty = property.CurrentValue;
                        data.MaxProperty = property.MaxCurrentValue;
                        data.IsPercentage = property.IsPercentage();
                        uiPropertyData[(int)kvp.Key] = data;
                    }
                    continue;
                }
                
                OnPropertyChanged?.Invoke(kvp.Key, property);
                if (data.ConsumeType!= PropertyConsumeType.None)
                {
                    data.CurrentProperty = property.CurrentValue;
                    data.IsPercentage = property.IsPercentage();
                    uiPropertyData[(int)kvp.Key] = data;
                }
                if (kvp.Key == PropertyTypeEnum.AttackSpeed)
                {
                    PlayerComponentController.SetAnimatorSpeed(AnimationState.Attack, property.CurrentValue);
                }
            }
            UIPropertyBinder.SetProperty(_goldBindKey, goldData);
        }
    }
}