using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Data.State;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.State;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using UnityEngine;
using VContainer;
using AnimationState = AOTScripts.Data.AnimationState;
using INetworkCommand = AOTScripts.Data.INetworkCommand;
using PlayerPredictablePropertyState = HotUpdate.Scripts.Network.State.PlayerPredictablePropertyState;
using PropertyCalculator = HotUpdate.Scripts.Network.State.PropertyCalculator;

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
        private HReactiveDictionary<int, PropertyItemData> _uiPropertyData = new HReactiveDictionary<int, PropertyItemData>();
        private HReactiveProperty<ValuePropertyData> _goldData;

        public PlayerPredictablePropertyState PlayerPredictablePropertyState => (PlayerPredictablePropertyState)CurrentState;

        protected override CommandType CommandType => CommandType.Property;

        public bool IsAttackable => PlayerPredictablePropertyState.IsAttackable;
        public bool IsMovable => PlayerPredictablePropertyState.IsMoveable;
        public float NowSpeedRatio => PlayerPredictablePropertyState.NowSpeedRatio;
        
        public SubjectedStateType NowStateType => PlayerPredictablePropertyState.ControlSkillType;

        public bool CanDoAnimation(AnimationState animationState)
        {
            if (animationState == AnimationState.Move || animationState == AnimationState.Sprint)
            {
                return IsMovable;
            }
            if (animationState == AnimationState.Attack)
            {
                return IsAttackable;
            }
            return true;
        
        }

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
            PropertyChanged(propertyState, true);
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

        private void PropertyChanged(PlayerPredictablePropertyState predictablePropertyState, bool isRecover = false)
        {
            // foreach (var key in predictablePropertyState.MemoryProperty.Keys)
            // {
            //     var property = PlayerPredictablePropertyState.MemoryProperty[key];
            //     //Debug.Log($"PropertyChanged {key}: {property}");
            // }
            //Debug.Log($"[PropertyChanged] {predictablePropertyState.ToString()}");
            if (!LocalPlayerHandler || _isDead)
            {
                Debug.LogError($"PropertyChanged {predictablePropertyState.ToString()} {!NetworkIdentity.isLocalPlayer} is not a player or is dead {_isDead}");
                return;
            }
            if (!_isDead && !_subjectedStateType.HasAnyState(SubjectedStateType.IsDead) && predictablePropertyState.ControlSkillType.HasAnyState(SubjectedStateType.IsDead))
            {
                var countDown = _jsonDataConfig.GameConfig.GetPlayerDeathTime((int)predictablePropertyState.MemoryProperty[PropertyTypeEnum.Score].CurrentValue);
                OnPlayerDead?.Invoke(countDown);
                //Debug.Log($"OnPlayerDead {countDown}");
                _isDead = true;
                return;
            }
            else if (_subjectedStateType.HasAnyState(SubjectedStateType.IsDead) && !predictablePropertyState.ControlSkillType.HasAnyState(SubjectedStateType.IsDead))
            {
                OnPlayerRespawned?.Invoke();
            }
            _subjectedStateType = _subjectedStateType.AddState(predictablePropertyState.ControlSkillType);
            OnStateChanged?.Invoke(predictablePropertyState.ControlSkillType);
            var goldData = ObjectPoolManager<ValuePropertyData>.Instance.Get(15);
            foreach (var kvp in predictablePropertyState.MemoryProperty)
            {
                var property = kvp.Value;
                if (property.IsShowInHud())
                {
                    if (!_uiPropertyData.TryGetValue((int)kvp.Key, out var propertyData))
                    {
                        propertyData = new PropertyItemData
                        {
                            Name = _propertyConfig.GetPropertyConfigData((PropertyTypeEnum)kvp.Key).description,
                            PropertyType = (PropertyTypeEnum)kvp.Key,
                            CurrentProperty = property.CurrentValue,
                            MaxProperty = property.MaxCurrentValue,
                            ConsumeType = _propertyConfig.GetPropertyConfigData((PropertyTypeEnum)kvp.Key).consumeType,
                            IsPercentage = _propertyConfig.IsHundredPercent((PropertyTypeEnum)kvp.Key),
                        };
                        _uiPropertyData[(int)kvp.Key] = propertyData;
                    }
                    else
                    {
                        propertyData.CurrentProperty = property.CurrentValue;
                        propertyData.MaxProperty = property.MaxCurrentValue;
                        propertyData.IsAutoRecover = isRecover;
                        _uiPropertyData[(int)kvp.Key] = propertyData;
                    }
                    OnPropertyChanged?.Invoke(kvp.Key, property);
                    UIPropertyBinder.UpdateDictionary(_propertyBindKey, (int)kvp.Key, propertyData);
                }
                //Debug.Log($"predictablePropertyState.MemoryProperty changed {kvp}: {property.CurrentValue} {property.MaxCurrentValue}");
                //;

                // data.CurrentProperty = property.CurrentValue;
                // data.MaxProperty = property.MaxCurrentValue;
                // data.IsAutoRecover = isRecover;
                // UIPropertyBinder.UpdateDictionary(_propertyBindKey, (int)kvp.Key, data);
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
                    // if (data.ConsumeType!= PropertyConsumeType.None)
                    // {
                    //     data.CurrentProperty = property.CurrentValue;
                    //     data.MaxProperty = property.MaxCurrentValue;
                    //     data.IsPercentage = property.IsPercentage();
                    //     data.IsAutoRecover = isRecover;
                    //     _uiPropertyData[(int)kvp.Key] = data;
                    //     Debug.Log($"uiPropertyData[{kvp.Key}]: {data}");
                    // }
                    continue;
                }
                
                OnPropertyChanged?.Invoke(kvp.Key, property);
                // if (data.ConsumeType!= PropertyConsumeType.None)
                // {
                //     data.CurrentProperty = property.CurrentValue;
                //     data.IsPercentage = property.IsPercentage();
                //     data.IsAutoRecover = isRecover;
                //     _uiPropertyData[(int)kvp.Key] = data;
                //     Debug.Log($"uiPropertyData[{kvp.Key}]: {data}");
                // }
                if (kvp.Key == PropertyTypeEnum.AttackSpeed)
                {
                    PlayerComponentController.SetAnimatorSpeed(AnimationState.Attack, property.CurrentValue);
                }
            }
            UIPropertyBinder.SetProperty(_goldBindKey, goldData);
        }

        protected override void InjectLocalPlayerCallback()
        {
            _propertyBindKey = new BindingKey(UIPropertyDefine.PlayerProperty, DataScope.LocalPlayer,
                UIPropertyBinder.LocalPlayerId);
            var playerPropertiesOverlay = _uiManager.SwitchUI<PlayerPropertiesOverlay>();
            playerPropertiesOverlay.BindPlayerProperty(
                UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_propertyBindKey));
            Debug.Log($"PropertyPredictionState [InjectLocalPlayerCallback]  ");
            _goldBindKey = new BindingKey(UIPropertyDefine.PlayerBaseData, DataScope.LocalPlayer,
                UIPropertyBinder.LocalPlayerId);
            _playerDeathTimeBindKey = new BindingKey(UIPropertyDefine.PlayerDeathTime, DataScope.LocalPlayer,
                UIPropertyBinder.LocalPlayerId);
            _playerControlBindKey = new BindingKey(UIPropertyDefine.PlayerControl, DataScope.LocalPlayer,
                UIPropertyBinder.LocalPlayerId);
            _uiPropertyData = UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_propertyBindKey);
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

                var baseProperties = _propertyConfig.GetBaseValue(propertyType);
                Debug.Log($"PropertyPredictionState [OnStartLocalPlayer]_{propertyType}_{baseProperties}");
                var displayName = propertyConfig.description;
                var consumeType = propertyConfig.consumeType;
                itemDatas.Add((int)propertyType, new PropertyItemData
                {
                    Name = displayName,
                    PropertyType = propertyType,
                    CurrentProperty = baseProperties,
                    MaxProperty = baseProperties,
                    ConsumeType = consumeType,
                    IsPercentage = _propertyConfig.IsHundredPercent(propertyType),
                });
            }

            UIPropertyBinder.OptimizedBatchAdd(_bindKey, itemDatas);
        }
    }
}