using System;
using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.UI.UIs.Overlay;
using Mirror;
using Tool.Coroutine;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerPropertyComponent : NetworkMonoComponent
    {
        private readonly Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>> _maxCurrentProperties = new Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>>();
        private readonly Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>> _currentProperties = new Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>>();
        private JsonDataConfig _jsonDataConfig;
        private UIManager _uiManager;
        private AnimationState _currentAnimationState;
        
        [SyncVar(hook = nameof(OnCurrentChestTypeChanged))] 
        private ChestType _currentChestType;
        [SyncVar(hook = nameof(OnPlayerStateChanged))]
        private PlayerEnvironmentState _playerEnvironmentState;
        [SyncVar(hook = nameof(OnHasMovementInputChanged))] 
        private bool _hasMovementInput;
        [SyncVar(hook = nameof(OnIsInvincibleChanged))]
        private bool _isInvincible;
        [SyncVar(hook = nameof(OnPlayerAttackDataChanged))]
        private PlayerAttackData _playerAttackData;
        
        
        private PlayerAttackData _configPlayerAttackData;

        public PlayerAttackData PlayerAttackData
        {
            get => _playerAttackData;
            set
            {
                if (isServer)
                {
                    _playerAttackData = value;
                }
                else if (isClient)
                {
                    Debug.Log("Client cannot change PlayerAttackData.");
                }
            }   
        }

        private ReactiveProperty<PlayerAttackData> _playerAttackDataProperty { get; } = new ReactiveProperty<PlayerAttackData>();
        public IReadOnlyReactiveProperty<PlayerAttackData> PlayerAttackDataProperty => _playerAttackDataProperty;

        private void OnPlayerAttackDataChanged(PlayerAttackData oldValue, PlayerAttackData newValue)
        {
            
        }

        private void OnCurrentChestTypeChanged(ChestType oldValue, ChestType newValue)
        {
            _currentChestTypeProperty.Value = newValue;
        }
        
        private void OnPlayerStateChanged(PlayerEnvironmentState oldValue, PlayerEnvironmentState newValue)
        {
            _playerStateProperty.Value = newValue;
        }

        private void OnHasMovementInputChanged(bool oldValue, bool newValue)
        {
            _hasMovementInputProperty.Value = newValue;
        }
        
        private void OnIsInvincibleChanged(bool oldValue, bool newValue)
        {
            _isInvisibleProperty.Value = newValue;
        }

        public bool IsInvincible
        {
            get => _isInvincible;
            set
            {
                if (isServer)
                {
                    _isInvincible = value;
                }
                else if (isClient)
                {
                    Debug.Log("Client cannot change invincibility.");
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
                    Debug.Log("Client cannot change CurrentChestType.");
                }
            }
        }
        
        

        public PlayerEnvironmentState PlayerEnvironmentState
        {
            get => _playerEnvironmentState;
            set
            {
                if (isServer)
                {
                    _playerEnvironmentState = value;
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
        public void CmdChangeHasMovementInput(bool movementInput)
        {
            _hasMovementInput = movementInput;
        }
        
        [Command]
        public void CmdChangePlayerState(PlayerEnvironmentState environmentState)
        {
            _playerEnvironmentState = environmentState;
        }
        private ReactiveProperty<ChestType> _currentChestTypeProperty { get; } = new ReactiveProperty<ChestType>();
        public IReadOnlyReactiveProperty<ChestType> CurrentChestTypeProperty => _currentChestTypeProperty;
        private ReactiveProperty<PlayerEnvironmentState> _playerStateProperty { get; } = new ReactiveProperty<PlayerEnvironmentState>();
        public IReadOnlyReactiveProperty<PlayerEnvironmentState> PlayerStateProperty => _playerStateProperty;
        private ReactiveProperty<bool> _hasMovementInputProperty { get; } = new ReactiveProperty<bool>();
        public IReadOnlyReactiveProperty<bool> HasMovementInputProperty => _hasMovementInputProperty;
        private ReactiveProperty<bool> _isSprintingProperty { get; } = new ReactiveProperty<bool>();
        public IReadOnlyReactiveProperty<bool> IsSprintingProperty => _isSprintingProperty;
        private ReactiveProperty<bool> _isInvisibleProperty { get; } = new ReactiveProperty<bool>();
        public IReadOnlyReactiveProperty<bool> IsInvisibleProperty => _isInvisibleProperty;

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
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _configPlayerAttackData = _jsonDataConfig.PlayerConfig.BaseAttackData;
            InitializeProperties();
            _currentChestTypeProperty.Value = ChestType.Attack;
            
            _syncCurrentProperties.OnAdd += OnCurrentPropertyAdd;
            _syncCurrentProperties.OnRemove += OnCurrentPropertyRemove;
            _syncCurrentProperties.OnSet += OnCurrentPropertySet;
                
            _syncMaxCurrentProperties.OnAdd += OnMaxCurrentPropertyAdd;
            _syncMaxCurrentProperties.OnRemove += OnMaxCurrentPropertyRemove;
            _syncMaxCurrentProperties.OnSet += OnMaxCurrentPropertySet;
            var playerAnimationComponent = GetComponent<PlayerAnimationComponent>();
            if (playerAnimationComponent)
            {
                playerAnimationComponent.CurrentState.Subscribe(state =>
                {
                    _currentAnimationState = state;
                    _isSprintingProperty.Value = state == AnimationState.Sprint;
                });
            }
            if (!isLocalPlayer) return;
            var properties = uiManager.SwitchUI<PlayerPropertiesOverlay>();
            properties.SetPlayerProperties(this);
        }

        private void OnMaxCurrentPropertyRemove(PropertyTypeEnum arg1, float arg2)
        {
            _maxCurrentProperties.Remove(arg1);
        }

        private void OnMaxCurrentPropertySet(PropertyTypeEnum arg1, float arg2)
        {
            MaxPropertyChanged(arg1,arg2);
        }

        private void OnMaxCurrentPropertyAdd(PropertyTypeEnum obj)
        {
            _maxCurrentProperties.Add(obj, new ReactiveProperty<PropertyType>(new PropertyType(obj, _syncMaxCurrentProperties[obj])));
        }

        private void OnCurrentPropertySet(PropertyTypeEnum arg1, float arg2)
        {
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

        private void InitializeProperties()
        {
            var enumValues = Enum.GetValues(typeof(PropertyTypeEnum));
            for (var i = 0; i < enumValues.Length; i++)
            {
                var propertyType = (PropertyTypeEnum)enumValues.GetValue(i);
                var minProperties = _jsonDataConfig.PlayerConfig.MinProperties.Find(x => x.TypeEnum == propertyType);
                var baseProperties = _jsonDataConfig.PlayerConfig.BaseProperties.Find(x => x.TypeEnum == propertyType);
                var maxProperties = _jsonDataConfig.PlayerConfig.MaxProperties.Find(x => x.TypeEnum == propertyType);
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
                Debug.Log("Client cannot increase property.");
                //CmdIncreaseProperty(type, increaseType, amount);
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
                    float currentValue;
                    if (type == PropertyTypeEnum.Score)
                    {
                        currentValue = Mathf.Clamp(_syncCurrentProperties[type] + amount * _syncPropertyCorrectionFactors[type], _configMinProperties[type],
                            _configMaxProperties[type]);
                        Debug.Log($"Current score:isServer{isServer}: {_syncCurrentProperties[type]} + {currentValue}");
                    }
                    else
                    {
                        currentValue = Mathf.Clamp(_syncCurrentProperties[type] + amount, _configMinProperties[type], _syncMaxCurrentProperties[type]);
                    }
                    _syncCurrentProperties[type] = currentValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(increaseType), increaseType, null);
            }
            
            UpdateProperty(type);
        }

        /// <summary>
        /// 更新当前属性，计算方法：（基础属性 * 加成系数 + 增益属性） * 修正系数
        /// </summary>
        /// <param name="type"></param>
        private void UpdateProperty(PropertyTypeEnum type)
        {
            if (type == PropertyTypeEnum.Score)
            {
                return;
            }
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
        
        public float GetPropertyValue(PropertyTypeEnum type)
        {
            return GetProperty(type).Value.Value;
        }
        
        public ReactiveProperty<PropertyType> GetMaxProperty(PropertyTypeEnum type)
        {
            return _maxCurrentProperties.GetValueOrDefault(type);
        }

        public bool StrengthCanDoAnimation(AnimationState animationState)
        {
            var strength = _syncCurrentProperties[PropertyTypeEnum.Strength];
            var cost = _jsonDataConfig.GetPlayerAnimationCost(animationState);
            if (cost != 0f)
            {
                if (animationState == AnimationState.Sprint)
                {
                    return strength >= cost * Time.fixedDeltaTime * 1.1f;
                }

                return strength >= cost;
            }
            return strength > 0.01f;
        }
        
        
        public void DoAnimation(AnimationState animationState)
        {
            switch (animationState)
            {
                case AnimationState.Move:
                case AnimationState.Idle:
                    ChangeAnimationState(animationState);
                    break;
                case AnimationState.Jump:
                case AnimationState.SprintJump:
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                    }
                    break;
                case AnimationState.Roll:
                    // if (_currentChestType != ChestType.Dash)
                    // {
                    //     Debug.LogWarning($"Player {gameObject.name} has no chest to dash.");
                    //     return false;
                    // }
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                    }
                    break;
                case AnimationState.Attack:
                    // if (_currentChestType != ChestType.Attack)
                    // {
                    //     Debug.LogWarning($"Player {gameObject.name} has no chest to attack.");
                    //     return false;
                    // }
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                    }
                    break;
            }
        }

        private void ChangeAnimationState(AnimationState animationState)
        {
            var cost = _jsonDataConfig.GetPlayerAnimationCost(animationState);
            if (animationState != AnimationState.Sprint)
            {
                IncreaseProperty(PropertyTypeEnum.Strength, BuffIncreaseType.Current, -cost);
            }
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer)
                return;
            var recoveredStrength = 5f;
            var isSprinting = _currentAnimationState == AnimationState.Sprint;
            var isSprintingJump = _currentAnimationState == AnimationState.SprintJump;
            if (isSprinting)
            {
                var cost = _jsonDataConfig.GetPlayerAnimationCost(_currentAnimationState);
                recoveredStrength -= cost;
            }
            ChangeSpeed(isSprinting, recoveredStrength, isSprintingJump);
        }

        private void ChangeSpeed(bool isSprinting, float recoveredStrength, bool isSprintingJump)
        {
            if (isServer)
            {
                ChangeSpeedAndUpdate(isSprinting, recoveredStrength, isSprintingJump);
            }
            else if (isClient)
            {
                CmdChangeSpeed(isSprinting, recoveredStrength, isSprintingJump);
            }
        }

        [Command]
        private void CmdChangeSpeed(bool isSprinting, float recoveredStrength, bool isSprintingJump)
        {
            ChangeSpeedAndUpdate(isSprinting, recoveredStrength, isSprintingJump);
        }

        private void ChangeSpeedAndUpdate(bool isSprinting, float recoveredStrength, bool isSprintingJump)
        {
            _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] = HasMovementInput ? 1f : 0f;
            switch (PlayerEnvironmentState)
            {
                case PlayerEnvironmentState.InAir:
                    _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] *= isSprintingJump ? _jsonDataConfig.PlayerConfig.SprintSpeedFactor : 1f;
                    break;
                case PlayerEnvironmentState.OnGround:
                    _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] *= isSprinting ? _jsonDataConfig.PlayerConfig.SprintSpeedFactor : 1f;
                    break;
                case PlayerEnvironmentState.OnStairs:
                    _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] *= isSprinting ? _jsonDataConfig.PlayerConfig.OnStairsSpeedRatioFactor * _jsonDataConfig.PlayerConfig.SprintSpeedFactor : _jsonDataConfig.PlayerConfig.OnStairsSpeedRatioFactor;
                    break;
                default:
                    throw new Exception($"playerState:{PlayerEnvironmentState} is not valid.");
            }
            UpdateProperty(PropertyTypeEnum.Speed);
            IncreaseProperty(PropertyTypeEnum.Strength, BuffIncreaseType.Current, recoveredStrength * Time.fixedDeltaTime);
        }
    }
}