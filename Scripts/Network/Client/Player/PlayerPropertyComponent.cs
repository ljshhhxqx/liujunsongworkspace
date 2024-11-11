using System;
using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using Common;
using Config;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.UI.UIs.Overlay;
using Mirror;
using Tool.Coroutine;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerPropertyComponent : NetworkMonoComponent
    {
        private readonly Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>> _maxCurrentProperties = new Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>>();
        private readonly Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>> _currentProperties = new Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>>();
        private PlayerDataConfig _playerDataConfig;
        private UIManager _uiManager;
        [SyncVar(hook = nameof(OnCurrentAnimationStateChanged))] 
        private AnimationState _currentAnimationState;
        [SyncVar(hook = nameof(OnCurrentChestTypeChanged))] 
        private ChestType _currentChestType;
        [SyncVar(hook = nameof(OnPlayerStateChanged))]
        private PlayerState _playerState;
        [SyncVar(hook = nameof(OnHasMovementInputChanged))] 
        private bool _hasMovementInput;
        
        private void OnCurrentAnimationStateChanged(AnimationState oldValue, AnimationState newValue)
        {
            if (newValue != oldValue)
            {
                Debug.Log($"OnCurrentAnimationStateChanged oldValue: {oldValue}, newValue: {newValue}");
            }

            CurrentAnimationStateProperty.Value = newValue;
        }
        
        private void OnCurrentChestTypeChanged(ChestType oldValue, ChestType newValue)
        {
            if (newValue != oldValue)
            {
                Debug.Log($"OnCurrentChestTypeChanged oldValue: {oldValue}, newValue: {newValue}");
            }
            CurrentChestTypeProperty.Value = newValue;
        }
        
        private void OnPlayerStateChanged(PlayerState oldValue, PlayerState newValue)
        {
            if (newValue != oldValue)
            {
                Debug.Log($"OnPlayerStateChanged oldValue: {oldValue}, newValue: {newValue}");
            }
            PlayerStateProperty.Value = newValue;
        }

        private void OnHasMovementInputChanged(bool oldValue, bool newValue)
        {
            if (newValue != oldValue)
            {
                Debug.Log($"OnHasMovementInputChanged oldValue: {oldValue}, newValue: {newValue}");
            }
            HasMovementInputProperty.Value = newValue;
        }

        public AnimationState CurrentAnimationState
        {
            get => _currentAnimationState;
            set
            {
                if (isServer)
                {
                    _currentAnimationState = value;
                }
                else if (isClient)
                {
                    CmdChangeAnimationState(value);
                }
            }
        }
        
        public ChestType CurrentChestType
        {
            get => _currentChestType;
            set
            {
                if (isServer)
                {
                    _currentChestType = value;
                }
                else if (isClient)
                {
                    CmdChangeChestType(value);
                }
            }
        }

        public PlayerState PlayerState
        {
            get => _playerState;
            set
            {
                if (isServer)
                {
                    _playerState = value;
                }
                else if (isClient)
                {
                    CmdChangePlayerState(value);
                }
            }
        }

        public bool HasMovementInput
        {
            get => _hasMovementInput;
            set
            {
                if (isServer)
                {
                    _hasMovementInput = value;
                }
                else if (isClient)
                {
                    CmdChangeHasMovementInput(value);
                }
            }
        }
        
        [Command]
        private void CmdChangeAnimationState(AnimationState animationState)
        {
            _currentAnimationState = animationState;
        }

        [Command]
        private void CmdChangeChestType(ChestType value)
        {
            _currentChestType = value;
        }

        [Command]
        public void CmdChangeHasMovementInput(bool movementInput)
        {
            _hasMovementInput = movementInput;
        }
        
        [Command]
        public void CmdChangePlayerState(PlayerState state)
        {
            _playerState = state;
        }

        public ReactiveProperty<AnimationState> CurrentAnimationStateProperty { get; } = new ReactiveProperty<AnimationState>();
        public ReactiveProperty<ChestType> CurrentChestTypeProperty { get; } = new ReactiveProperty<ChestType>();
        public ReactiveProperty<PlayerState> PlayerStateProperty { get; } = new ReactiveProperty<PlayerState>();
        public ReactiveProperty<bool> HasMovementInputProperty { get; } = new ReactiveProperty<bool>();

        private readonly Dictionary<PropertyTypeEnum, float> _configBaseProperties = new Dictionary<PropertyTypeEnum, float>(); 
        private readonly Dictionary<PropertyTypeEnum, float> _configMinProperties = new Dictionary<PropertyTypeEnum, float>();
        private readonly Dictionary<PropertyTypeEnum, float> _configMaxProperties = new Dictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncBaseProperties = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncPropertyBuffs = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncPropertyMultipliers = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncPropertyCorrectionFactors = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncCurrentProperties = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncMaxCurrentProperties = new SyncDictionary<PropertyTypeEnum, float>();
        
        [Inject]
        private void Init(IConfigProvider configProvider, UIManager uiManager, RepeatedTask repeated)
        {
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            InitializeProperties();
            CurrentChestTypeProperty.Value = ChestType.Attack;
            var properties = uiManager.SwitchUI<PlayerPropertiesOverlay>();
            properties.SetPlayerProperties(this);
            
            _syncCurrentProperties.OnAdd += OnCurrentPropertyAdd;
            _syncCurrentProperties.OnRemove += OnCurrentPropertyRemove;
            _syncCurrentProperties.OnSet += OnCurrentPropertySet;
                
            _syncMaxCurrentProperties.OnAdd += OnMaxCurrentPropertyAdd;
            _syncMaxCurrentProperties.OnRemove += OnMaxCurrentPropertyRemove;
            _syncMaxCurrentProperties.OnSet += OnMaxCurrentPropertySet;
        }

        private void OnMaxCurrentPropertyRemove(PropertyTypeEnum arg1, float arg2)
        {
            _maxCurrentProperties.Remove(arg1);
        }

        private void OnMaxCurrentPropertySet(PropertyTypeEnum arg1, float arg2)
        {
            //Debug.Log("OnMaxCurrentPropertySet");
            MaxPropertyChanged(arg1,arg2);
        }

        private void OnMaxCurrentPropertyAdd(PropertyTypeEnum obj)
        {
            _maxCurrentProperties.Add(obj, new ReactiveProperty<PropertyType>(new PropertyType(obj, _syncMaxCurrentProperties[obj])));
        }

        private void OnCurrentPropertySet(PropertyTypeEnum arg1, float arg2)
        {
            //Debug.Log("OnCurrentPropertySet");
            PropertyChanged(arg1,arg2);
        }

        private void OnCurrentPropertyRemove(PropertyTypeEnum arg1, float arg2)
        {
            _currentProperties.Remove(arg1);
        }

        private void OnCurrentPropertyAdd(PropertyTypeEnum obj)
        {
            _currentProperties.Add(obj, new ReactiveProperty<PropertyType>(new PropertyType(obj, _syncCurrentProperties[obj])));
        }

        private void Awake()
        {
            ObjectInjectProvider.Instance.Inject(this);
        }

        private void InitializeProperties()
        {
            for (var i = (int)PropertyTypeEnum.Speed; i <= (int)PropertyTypeEnum.Score; i++)
            {
                var propertyType = (PropertyTypeEnum)i;
                var minProperties = _playerDataConfig.PlayerConfigData.MinProperties.Find(x => x.TypeEnum == propertyType);
                var baseProperties = _playerDataConfig.PlayerConfigData.BaseProperties.Find(x => x.TypeEnum == propertyType);
                var maxProperties = _playerDataConfig.PlayerConfigData.MaxProperties.Find(x => x.TypeEnum == propertyType);
                if (maxProperties.Value == 0)
                {
                    throw new Exception("Property value cannot be zero.");
                }
                var maxProperty = new PropertyType(propertyType, maxProperties.Value);
                var baseProperty = new PropertyType(propertyType, baseProperties.Value);
                var minProperty = new PropertyType(propertyType, minProperties.Value);
                _configMinProperties.Add(propertyType, minProperty.Value);
                _configMaxProperties.Add(propertyType, maxProperty.Value);
                _configBaseProperties.Add(propertyType, baseProperties.Value);
                _currentProperties.Add(propertyType, new ReactiveProperty<PropertyType>(baseProperty));
                _maxCurrentProperties.Add(propertyType, new ReactiveProperty<PropertyType>(baseProperty));
                for (var j = (int)BuffIncreaseType.Base; j <= (int)BuffIncreaseType.CorrectionFactor; j++)
                {
                    switch ((BuffIncreaseType)j)
                    {
                        case BuffIncreaseType.Base:
                            _syncBaseProperties.Add(propertyType, baseProperty.Value);
                            break;
                        case BuffIncreaseType.Multiplier:
                            _syncPropertyMultipliers.Add(propertyType, 1f);
                            break;
                        case BuffIncreaseType.Extra:
                            _syncPropertyBuffs.Add(propertyType, 0f);
                            break;
                        case BuffIncreaseType.CorrectionFactor:
                            _syncPropertyCorrectionFactors.Add(propertyType, 1f);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                }
                _syncCurrentProperties.Add(propertyType, baseProperty.Value);
                _syncMaxCurrentProperties.Add(propertyType, baseProperty.Value);
            }
        }

        public void IncreaseProperty(PropertyTypeEnum type, List<BuffIncreaseData> buffIncreaseData)
        {
            foreach (var data in buffIncreaseData)
            {
                IncreaseProperty(type, data.increaseType, data.increaseValue);
            }
        }
        

        public void IncreaseProperty(PropertyTypeEnum type, BuffIncreaseType increaseType, float amount)
        {
            if (isServer)
            {
                IncreasePropertyAndUpdate(type, increaseType, amount);
            }
            else if (isClient)
            {
                CmdIncreaseProperty(type, increaseType, amount);
            }
        }

        private void IncreasePropertyAndUpdate(PropertyTypeEnum type, BuffIncreaseType increaseType, float amount)
        {
            switch (increaseType)
            {
                case BuffIncreaseType.Base:
                    _syncBaseProperties[type] = Mathf.Max(_configBaseProperties[type], _syncBaseProperties[type] + amount);
                    break;
                case BuffIncreaseType.Multiplier:
                    _syncPropertyMultipliers[type] = Mathf.Max(0f, _syncPropertyMultipliers[type] + amount);
                    break;
                case BuffIncreaseType.Extra:
                    _syncPropertyBuffs[type] += amount;
                    break;
                case BuffIncreaseType.CorrectionFactor:
                    _syncPropertyCorrectionFactors[type] = Mathf.Max(0f, _syncPropertyCorrectionFactors[type] + amount);
                    break;
                case BuffIncreaseType.Current:
                    var currentValue = Mathf.Clamp(_syncCurrentProperties[type] + amount, _configMinProperties[type], _syncMaxCurrentProperties[type]);
                    if (type == PropertyTypeEnum.Score)
                    {
                        currentValue = Mathf.Round(currentValue);
                    }
                    _syncCurrentProperties[type] = currentValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(increaseType), increaseType, null);
            }
            
            UpdateProperty(type);
        }

        [Command]
        private void CmdIncreaseProperty(PropertyTypeEnum type, BuffIncreaseType increaseType, float amount)
        {
            IncreasePropertyAndUpdate(type, increaseType, amount);
        }

        /// <summary>
        /// 更新当前属性，计算方法：（基础属性 * 加成系数 + 增益属性） * 修正系数
        /// </summary>
        /// <param name="type"></param>
        private void UpdateProperty(PropertyTypeEnum type)
        {
            var propertyConsumeType = type.GetConsumeType();
            var propertyVal = (_syncBaseProperties[type] * _syncPropertyMultipliers[type] + _syncPropertyBuffs[type]) * _syncPropertyCorrectionFactors[type];
            switch (propertyConsumeType)
            {
                case PropertyConsumeType.Consume:
                    _syncMaxCurrentProperties[type] = Mathf.Clamp(propertyVal, 
                        _configBaseProperties[type],
                        _configMaxProperties[type]);
                    _syncCurrentProperties[type] = Mathf.Clamp(_syncCurrentProperties[type], _configMinProperties[type], _syncMaxCurrentProperties[type]);
                    break;
                case PropertyConsumeType.Number:
                    _syncCurrentProperties[type] = Mathf.Clamp(propertyVal, 
                        _configMinProperties[type],
                        _configMaxProperties[type]);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            // MaxPropertyChanged(_syncMaxCurrentProperties[type]);
            // PropertyChanged(_syncCurrentProperties[type]);
        }
        
        private void MaxPropertyChanged(PropertyTypeEnum type,float value)
        {
            var property = new PropertyType(type, value);
            _maxCurrentProperties[type].Value = property;
            _maxCurrentProperties[type].SetValueAndForceNotify(property);
        }
        
        private void PropertyChanged(PropertyTypeEnum type,float value)
        {
            var property = new PropertyType(type, value);
            _currentProperties[type].Value = property;
            _currentProperties[type].SetValueAndForceNotify(property); 
        }
        
        public ReactiveProperty<PropertyType> GetProperty(PropertyTypeEnum type)
        {
            return _currentProperties.GetValueOrDefault(type);
        }
        
        public ReactiveProperty<PropertyType> GetMaxProperty(PropertyTypeEnum type)
        {
            return _maxCurrentProperties.GetValueOrDefault(type);
        }

        public bool StrengthCanDoAnimation(AnimationState animationState)
        {
            var strength = _syncCurrentProperties[PropertyTypeEnum.Strength];
            var cost = _playerDataConfig.GetPlayerAnimationCost(animationState);
            if (cost != 0f)
            {
                if (animationState == AnimationState.Sprint)
                {
                    return strength >= cost * 0.1f;
                }

                return strength >= cost;
            }
            Debug.LogError($"Animation {animationState} not found in config.");
            return strength > 0f;
        }

        public bool DoAnimation(AnimationState animationState)
        {
            switch (animationState)
            {
                case AnimationState.Idle:
                case AnimationState.Move:
                case AnimationState.Dead:
                    CurrentAnimationState = animationState;
                    return true;
                case AnimationState.Sprint:
                case AnimationState.Jump:
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                        return true;
                    }
                    return false;
                case AnimationState.Dash:
                    if (_currentChestType != ChestType.Dash)
                    {
                        Debug.LogWarning($"Player {gameObject.name} has no chest to dash.");
                        return false;
                    }
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                        return true;
                    }
                    return false;
                case AnimationState.Attack:
                    if (_currentChestType != ChestType.Attack)
                    {
                        Debug.LogWarning($"Player {gameObject.name} has no chest to attack.");
                        return false;
                    }
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                        return true;
                    }
                    return false;
                default:
                    throw new ArgumentOutOfRangeException(nameof(animationState), animationState, null);
            }
        }

        private void ChangeAnimationState(AnimationState animationState)
        {
            var cost = _playerDataConfig.GetPlayerAnimationCost(animationState);
            CurrentAnimationState = animationState;
            if (animationState != AnimationState.Sprint)
            {
                IncreaseProperty(PropertyTypeEnum.Strength, BuffIncreaseType.Current, -cost);
            }
        }

        private void FixedUpdate()
        {
            var recoveredStrength = _playerDataConfig.PlayerConfigData.StrengthRecoveryPerSecond;
            var isSprinting = CurrentAnimationState == AnimationState.Sprint;
            if (isSprinting)
            {
                var cost = _playerDataConfig.GetPlayerAnimationCost(CurrentAnimationState);
                recoveredStrength -= cost;
            }
            ChangeSpeed(isSprinting, recoveredStrength);
        }

        private void ChangeSpeed(bool isSprinting, float recoveredStrength)
        {
            if (isServer)
            {
                ChangeSpeedAndUpdate(isSprinting, recoveredStrength);
            }
            else if (isClient)
            {
                CmdChangeSpeed(isSprinting, recoveredStrength);
            }
        }

        [Command]
        private void CmdChangeSpeed(bool isSprinting, float recoveredStrength)
        {
            ChangeSpeedAndUpdate(isSprinting, recoveredStrength);
        }

        private void ChangeSpeedAndUpdate(bool isSprinting, float recoveredStrength)
        {
            _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] = HasMovementInput ? 1f : 0f;
            
            switch (PlayerState)
            {
                case PlayerState.InAir:
                    break;
                case PlayerState.OnGround:
                    _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] *= isSprinting ? _playerDataConfig.PlayerConfigData.SprintSpeedFactor : 1f;
                    break;
                case PlayerState.OnStairs:
                    _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] *= isSprinting ? _playerDataConfig.PlayerConfigData.OnStairsSpeedRatioFactor * _playerDataConfig.PlayerConfigData.SprintSpeedFactor : _playerDataConfig.PlayerConfigData.OnStairsSpeedRatioFactor;
                    break;
                default:
                    throw new Exception($"playerState:{PlayerState} is not valid.");
            }
            UpdateProperty(PropertyTypeEnum.Speed);
            IncreaseProperty(PropertyTypeEnum.Strength, BuffIncreaseType.Current, recoveredStrength * Time.fixedDeltaTime);
        }
    }
}