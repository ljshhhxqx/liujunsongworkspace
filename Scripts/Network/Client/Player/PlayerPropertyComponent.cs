using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ECS;
using Common;
using Config;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.UI.UIs.Overlay;
using Mirror;
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
        [SyncVar] 
        public AnimationState currentAnimationState;
        [SyncVar] 
        public ChestType currentChestType;
        [SyncVar]
        public PlayerState playerState;
        [SyncVar] 
        public bool hasMovementInput;
        
        private readonly Dictionary<PropertyTypeEnum, float> _configBaseProperties = new Dictionary<PropertyTypeEnum, float>(); 
        private readonly Dictionary<PropertyTypeEnum, float> _configMinProperties = new Dictionary<PropertyTypeEnum, float>();
        private readonly Dictionary<PropertyTypeEnum, float> _configMaxProperties = new Dictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncBaseProperties = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncPropertyBuffs = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncPropertyMultipliers = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, float> _syncPropertyCorrectionFactors = new SyncDictionary<PropertyTypeEnum, float>();
        private readonly SyncDictionary<PropertyTypeEnum, PropertyType> _syncCurrentProperties = new SyncDictionary<PropertyTypeEnum, PropertyType>();
        private readonly SyncDictionary<PropertyTypeEnum, PropertyType> _syncMaxCurrentProperties = new SyncDictionary<PropertyTypeEnum, PropertyType>();
        
        [Inject]
        private void Init(IConfigProvider configProvider, UIManager uiManager)
        {
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            InitializeProperties();
            var properties = uiManager.SwitchUI<PlayerPropertiesOverlay>();
            properties.SetPlayerProperties(this);
        }
        
        [Command]
        public void ChangeHasMovementInput(bool movementInput)
        {
            hasMovementInput = movementInput;
        }
        
        [Command]
        public void ChangePlayerState(PlayerState state)
        {
            playerState = state;
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
                _maxCurrentProperties.Add(propertyType, new ReactiveProperty<PropertyType>(baseProperty));
                _configMinProperties.Add(propertyType, minProperty.Value);
                _currentProperties.Add(propertyType, new ReactiveProperty<PropertyType>(baseProperty));
                _configMaxProperties.Add(propertyType, maxProperty.Value);
                _configBaseProperties.Add(propertyType, baseProperties.Value);
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
                _syncCurrentProperties.Add(propertyType, baseProperty);
                _syncMaxCurrentProperties.Add(propertyType, baseProperty);
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
            else
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
                    var currentValue = Mathf.Clamp(_syncCurrentProperties[type].Value + amount, _configMinProperties[type], _maxCurrentProperties[type].Value.Value);
                    if (type == PropertyTypeEnum.Score)
                    {
                        currentValue = Mathf.Round(currentValue);
                    }
                    _syncCurrentProperties[type] = new PropertyType(type, currentValue);
                    PropertyChanged(_syncCurrentProperties[type]);
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
            var propertyVal = (_syncBaseProperties[type] * _syncPropertyMultipliers[type] + _syncPropertyBuffs[type]) * _syncPropertyCorrectionFactors[type];
            _syncMaxCurrentProperties[type] = new PropertyType(type, Mathf.Clamp(propertyVal, 
                _configMinProperties[type],
                _configMaxProperties[type]));
            _syncCurrentProperties[type] = new PropertyType(type, Mathf.Clamp(_syncCurrentProperties[type].Value, _configMinProperties[type], _maxCurrentProperties[type].Value.Value));
            MaxPropertyChanged(_syncMaxCurrentProperties[type]);
            PropertyChanged(_syncCurrentProperties[type]);
        }
        
        private void MaxPropertyChanged(PropertyType property)
        {
            _maxCurrentProperties[property.TypeEnum].Value = property;
            _maxCurrentProperties[property.TypeEnum].SetValueAndForceNotify(property);
        }
        
        private void PropertyChanged(PropertyType property)
        {
            _currentProperties[property.TypeEnum].Value = property;
            _currentProperties[property.TypeEnum].SetValueAndForceNotify(property); 
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
                    return strength.Value >= cost * 0.1f;
                }

                return strength.Value >= cost;
            }
            Debug.LogError($"Animation {animationState} not found in config.");
            return strength.Value > 0f;
        }
        
        [Command]
        private void CmdChangeAnimationState(AnimationState animationState)
        {
            currentAnimationState = animationState;
        }

        public bool DoAnimation(AnimationState animationState)
        {
            switch (animationState)
            {
                case AnimationState.Idle:
                case AnimationState.Move:
                case AnimationState.Dead:
                    if (isServer)
                    {
                        currentAnimationState = animationState;
                    }
                    else
                    {
                        CmdChangeAnimationState(animationState);
                    }
                    return true;
                case AnimationState.Sprint:
                    if (isServer && StrengthCanDoAnimation(animationState))
                    {
                        if (isServer)
                        {
                            currentAnimationState = animationState;
                        }
                        else
                        {
                            CmdChangeAnimationState(animationState);
                        }
                    }
                    return true;
                case AnimationState.Jump:
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                    }
                    return true;
                case AnimationState.Dash:
                    if (currentChestType != ChestType.Dash)
                    {
                        Debug.LogWarning($"Player {gameObject.name} has no chest to dash.");
                        return false;
                    }
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                    }
                    return true;
                case AnimationState.Attack:
                    if (currentChestType != ChestType.Attack)
                    {
                        Debug.LogWarning($"Player {gameObject.name} has no chest to attack.");
                        return false;
                    }
                    if (StrengthCanDoAnimation(animationState))
                    {
                        ChangeAnimationState(animationState);
                    }
                    return true;
                default:
                    throw new ArgumentOutOfRangeException(nameof(animationState), animationState, null);
            }
        }

        private void ChangeAnimationState(AnimationState animationState)
        {
            var cost = _playerDataConfig.GetPlayerAnimationCost(animationState);
            IncreaseProperty(PropertyTypeEnum.Strength, BuffIncreaseType.Current, cost);
            
            if (isServer)
            {
                currentAnimationState = animationState;
            }
            else
            {
                CmdChangeAnimationState(animationState);
            }
        }

        private void FixedUpdate()
        {
            var recoveredStrength = _playerDataConfig.PlayerConfigData.StrengthRecoveryPerSecond;
            var isSprinting = currentAnimationState == AnimationState.Sprint;
            if (isSprinting)
            {
                var cost = _playerDataConfig.GetPlayerAnimationCost(currentAnimationState);
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
            else
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
            _syncPropertyCorrectionFactors[PropertyTypeEnum.Speed] = hasMovementInput ? _configBaseProperties[PropertyTypeEnum.Speed] : _configMinProperties[PropertyTypeEnum.Speed];
            
            switch (playerState)
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
                    throw new Exception($"playerState:{playerState} is not valid.");
            }
            IncreaseProperty(PropertyTypeEnum.Strength, BuffIncreaseType.Current, recoveredStrength * Time.fixedDeltaTime);
        }
    }
}