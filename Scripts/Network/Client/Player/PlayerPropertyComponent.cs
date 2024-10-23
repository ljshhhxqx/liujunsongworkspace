using System;
using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using Config;
using HotUpdate.Scripts.Config;
using Mirror;
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
        [SyncVar] 
        public AnimationState currentAnimationState;
        [SyncVar] 
        public ChestType currentChestType;
        [SyncVar]
        public PlayerState playerState;
        
        private readonly Dictionary<PropertyTypeEnum, float> _configBaseProperties = new Dictionary<PropertyTypeEnum, float>(); 
        private readonly SyncDictionary<PropertyTypeEnum, PropertyType> _syncCurrentProperties = new SyncDictionary<PropertyTypeEnum, PropertyType>();
        private readonly SyncDictionary<PropertyTypeEnum, PropertyType> _syncMaxProperties = new SyncDictionary<PropertyTypeEnum, PropertyType>();
        private readonly SyncDictionary<PropertyTypeEnum, Dictionary<BuffIncreaseType, float>> _syncBuffIncreases = new SyncDictionary<PropertyTypeEnum, Dictionary<BuffIncreaseType, float>>();
        
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
                var baseProperties = _playerDataConfig.PlayerConfigData.BaseProperties.Find(x => x.TypeEnum == propertyType);
                var maxProperties = _playerDataConfig.PlayerConfigData.MaxProperties.Find(x => x.TypeEnum == propertyType);
                if (maxProperties.Value == 0)
                {
                    throw new Exception("Property value cannot be zero.");
                }

                var property = new PropertyType(propertyType, maxProperties.Value);
                var baseProperty = new PropertyType(propertyType, baseProperties.Value);
                _maxCurrentProperties.Add(propertyType, new ReactiveProperty<PropertyType>(property));
                _currentProperties.Add(propertyType, new ReactiveProperty<PropertyType>(baseProperty));
                _configBaseProperties.Add(propertyType, baseProperties.Value);
                if (isServer)
                {
                    var buffIncreases = new Dictionary<BuffIncreaseType, float>();
                    for (var j = (int)BuffIncreaseType.Base; j <= (int)BuffIncreaseType.CorrectionFactor; j++)
                    {
                        switch ((BuffIncreaseType)j)
                        {
                            case BuffIncreaseType.Base:
                                buffIncreases.Add((BuffIncreaseType)j, baseProperty.Value);
                                break;
                            case BuffIncreaseType.Multiplier:
                                buffIncreases.Add((BuffIncreaseType)j, 1f);
                                break;
                            case BuffIncreaseType.Extra:
                                buffIncreases.Add((BuffIncreaseType)j, 0f);
                                break;
                            case BuffIncreaseType.CorrectionFactor:
                                buffIncreases.Add((BuffIncreaseType)j, 1f);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                    _syncBuffIncreases.Add(propertyType, buffIncreases);
                    _syncCurrentProperties.Add(propertyType, baseProperty);
                    _syncMaxProperties.Add(propertyType, baseProperty);
                }
            }
        }

        public void IncreaseProperty(PropertyTypeEnum type, List<BuffIncreaseData> buffIncreaseData)
        {
            if (_syncBuffIncreases.TryGetValue(type, out var increase))
            {
                foreach (var increaseData in buffIncreaseData)
                {
                    IncreaseProperty(type, increaseData.increaseType, increaseData.increaseValue);
                }
                UpdateProperty(type);
            }
        }

        public void IncreaseProperty(PropertyTypeEnum type, BuffIncreaseType increaseType, float amount)
        {
            if (_syncBuffIncreases.TryGetValue(type, out var increase))
            {
                switch (increaseType)
                {
                    case BuffIncreaseType.Base:
                        increase[BuffIncreaseType.Base] += amount;
                        break;
                    case BuffIncreaseType.Multiplier:
                        increase[BuffIncreaseType.Multiplier] += amount;
                        if (increase[BuffIncreaseType.Multiplier] < 0f)
                        {
                            increase[BuffIncreaseType.Multiplier] = 0f;
                        }
                        break;
                    case BuffIncreaseType.Extra:
                        increase[BuffIncreaseType.Extra] += amount;
                        break;
                    case BuffIncreaseType.CorrectionFactor:
                        increase[BuffIncreaseType.CorrectionFactor] += amount;
                        if (increase[BuffIncreaseType.CorrectionFactor] < 0f)
                        {
                            increase[BuffIncreaseType.CorrectionFactor] = 0f;
                        }
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(increaseType), increaseType, null);
                }
                UpdateProperty(type);
            }
        }

        /// <summary>
        /// 更新当前属性，计算方法：（基础属性 * 加成系数 + 增益属性） * 修正系数
        /// </summary>
        /// <param name="type"></param>
        private void UpdateProperty(PropertyTypeEnum type)
        {
            var property = _syncBuffIncreases[type];
            var propertyVal = (property[BuffIncreaseType.Base] * property[BuffIncreaseType.Multiplier] + property[BuffIncreaseType.Extra]) * property[BuffIncreaseType.CorrectionFactor];
            propertyVal = Mathf.Min(_maxCurrentProperties[type].Value.Value, propertyVal);
            propertyVal = Mathf.Max(_configBaseProperties[type], propertyVal);
            _syncCurrentProperties[type] = new PropertyType(type, propertyVal);
            PropertyChanged(_syncCurrentProperties[type]);
        }
        
        private void PropertyChanged(PropertyType property)
        {
            _currentProperties[property.TypeEnum].Value = property;
            _currentProperties[property.TypeEnum].SetValueAndForceNotify(property); 
        }
        
        public PropertyType GetProperty(PropertyTypeEnum type)
        {
            return _syncCurrentProperties.TryGetValue(type, out var property) ? property : default;
        }
        
        public PropertyType GetMaxProperty(PropertyTypeEnum type)
        {
            return _syncMaxProperties.TryGetValue(type, out var property) ? property : default;
        }

        public bool StrengthCanDoAnimation(AnimationState animationState)
        {
            var strength = _syncCurrentProperties[PropertyTypeEnum.Strength];
            if (_playerDataConfig.PlayerConfigData.AnimationStrengthCosts.TryGetValue(animationState, out var animationCost))
            {
                if (animationState == AnimationState.Sprint)
                {
                    return strength.Value >= animationCost * 0.1f;
                }

                return strength.Value >= animationCost;
            }
            Debug.LogError($"Animation {animationState} not found in config.");
            return strength.Value > 0f;
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
                    return true;
                case AnimationState.Sprint:
                    if (isServer && StrengthCanDoAnimation(animationState))
                    {
                        currentAnimationState = AnimationState.Sprint;
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
            _syncCurrentProperties[PropertyTypeEnum.Strength].IncreaseValue(-_playerDataConfig.PlayerConfigData.AnimationStrengthCosts[animationState]);
            PropertyChanged(_syncCurrentProperties[PropertyTypeEnum.Strength]);
            if (isServer)
            {
                currentAnimationState = animationState;
            }
        }

        private void FixedUpdate()
        {
            if (isServer)
            {
                var recoveredStrength = _playerDataConfig.PlayerConfigData.StrengthRecoveryPerSecond;
                if (currentAnimationState == AnimationState.Sprint)
                {
                    recoveredStrength -= _playerDataConfig.PlayerConfigData.AnimationStrengthCosts[AnimationState.Sprint];
                }

                switch (playerState)
                {
                    case PlayerState.InAir:
                        break;
                    case PlayerState.OnGround:
                        
                        break;
                    case PlayerState.OnStairs:
                        break;
                    default:
                        throw new Exception($"playerState:{playerState} is not valid.");
                }
               // incre
                _syncCurrentProperties[PropertyTypeEnum.Strength].IncreaseValue(recoveredStrength * Time.fixedDeltaTime);
                PropertyChanged(_syncCurrentProperties[PropertyTypeEnum.Strength]);
            }
        }
    }
}