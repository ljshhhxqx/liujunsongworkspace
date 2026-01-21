using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Data.State;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.State;
using UnityEngine;
using AnimationState = AOTScripts.Data.AnimationState;
using PlayerPredictablePropertyState = HotUpdate.Scripts.Network.State.PlayerPredictablePropertyState;
using PropertyCalculator = HotUpdate.Scripts.Network.State.PropertyCalculator;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerPropertyCalculator : IPlayerStateCalculator
    {
        public static PropertyCalculatorConstant CalculatorConstant { get; private set; }
        public static Dictionary<PropertyTypeEnum, float> ConfigPlayerMinProperties { get; private set; }
        public static Dictionary<PropertyTypeEnum, float> ConfigPlayerMaxProperties { get; private set; }
        public static Dictionary<PropertyTypeEnum, float> ConfigPlayerBaseProperties { get; private set; }
        public SubjectedStateType SubjectedStateType { get; private set; }
        public bool IsClient { get; private set; }
        public event Action<PropertyTypeEnum, float> OnPropertyChanged;
        public Dictionary<PropertyTypeEnum, PropertyCalculator> Properties { get; private set; }

        public PlayerPropertyCalculator(Dictionary<PropertyTypeEnum, PropertyCalculator> properties, bool isClient = true)
        {
            Properties = properties;
            IsClient = isClient;
        }
        
        public static void SetCalculatorConstant(PropertyCalculatorConstant constant)
        {
            CalculatorConstant = constant;
            ConfigPlayerMinProperties = CalculatorConstant.PropertyConfig.GetPlayerMinProperties();
            ConfigPlayerMaxProperties = CalculatorConstant.PropertyConfig.GetPlayerMaxProperties();
            ConfigPlayerBaseProperties = CalculatorConstant.PropertyConfig.GetPlayerBaseProperties();
        }
        
        public static Dictionary<PropertyTypeEnum, PropertyCalculator> GetPropertyCalculators()
        {
            var dictionary = new Dictionary<PropertyTypeEnum, PropertyCalculator>();
            var enumValues = (PropertyTypeEnum[])Enum.GetValues(typeof(PropertyTypeEnum));
            foreach (var propertyType in enumValues)
            {
                if (propertyType == PropertyTypeEnum.None)
                {
                    continue;
                }
                var propertyData = new PropertyCalculator.PropertyData();
                var propertyConfig = CalculatorConstant.PropertyConfig.GetPropertyConfigData(propertyType);
                propertyData.BaseValue = ConfigPlayerBaseProperties[propertyType];
                propertyData.Additive = 0;
                propertyData.Multiplier = 1;
                propertyData.Correction = 1;
                propertyData.CurrentValue = propertyData.BaseValue;
                propertyData.MaxCurrentValue = propertyData.BaseValue;
                var calculator = new PropertyCalculator(propertyType, propertyData, ConfigPlayerMaxProperties[propertyType],ConfigPlayerMinProperties[propertyType], propertyConfig.consumeType == PropertyConsumeType.Consume, propertyConfig.showInHud);
                dictionary.Add(propertyType, calculator);
            }
            return dictionary;
        }

        public static void UpdateSpeed(ref PlayerPredictablePropertyState state, bool isSprinting, bool hasInputMovement, PlayerEnvironmentState playerEnvironmentState)
        {
            var memoryPropertyCalculator = state.MemoryProperty;
            var currentSpeed = state.MemoryProperty[PropertyTypeEnum.Speed].CurrentValue;
            //Debug.Log($"[UpdateSpeed] Current speed: {currentSpeed}");
            var currentFactor = hasInputMovement ? 1f : 0f;
            //Debug.Log($"[UpdateSpeed] Current factor: {currentFactor}");
            //Debug.Log($"[UpdateSpeed] isSprinting = {isSprinting} hasInputMovement = {hasInputMovement} playerEnvironmentState = {playerEnvironmentState}");
            switch (playerEnvironmentState)
            {
                case PlayerEnvironmentState.InAir:
                case PlayerEnvironmentState.OnGround:
                    currentFactor *= isSprinting ? memoryPropertyCalculator[PropertyTypeEnum.SprintSpeedRatio].CurrentValue : 1;
                    break;
                case PlayerEnvironmentState.OnStairs:
                    currentFactor *= isSprinting ? memoryPropertyCalculator[PropertyTypeEnum.StairsSpeedRatio].CurrentValue * memoryPropertyCalculator[PropertyTypeEnum.SprintSpeedRatio].CurrentValue : memoryPropertyCalculator[PropertyTypeEnum.StairsSpeedRatio].CurrentValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(playerEnvironmentState), playerEnvironmentState, null);
            }

            //Debug.Log($"[UpdateSpeed] Current speed * currentFactor: {currentSpeed} * {currentFactor} = {currentSpeed * currentFactor}");
            currentSpeed *= currentFactor;
            state.PlayerState.CurrentMoveSpeed = currentSpeed;
        }

        public float GetProperty(PropertyTypeEnum propertyType)
        {
            return Properties[propertyType].CurrentValue;
        }
        
        public float GetMaxProperty(PropertyTypeEnum propertyType)
        {
            if (Properties[propertyType].IsResourceProperty())
            {
                return Properties[propertyType].MaxCurrentValue;
            }
            return Properties[propertyType].CurrentValue;
        }

        public static DamageResultData HandleItemAttack(uint attackerId, uint defenderId, ref PlayerPredictablePropertyState defender, DamageCalculateResultData damageCalculateResultData)
        {
            var damageResultData = new DamageResultData();
            damageResultData.HitterUid = attackerId;
            damageResultData.DefenderUid = defenderId;
            damageResultData.DamageCalculateResult = damageCalculateResultData;
            damageResultData.IsCritical = damageCalculateResultData.IsCritical;
            damageResultData.DamageRatio = damageCalculateResultData.Damage / defender.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue;
            var remainHealth = GetRemainHealth(defender.MemoryProperty[PropertyTypeEnum.Health], damageCalculateResultData.Damage);
            defender.MemoryProperty[PropertyTypeEnum.Health] = remainHealth;
            damageResultData.HpRemainRatio = remainHealth.CurrentValue / defender.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue;
            if (remainHealth.CurrentValue <= 0)
            {
                damageResultData.IsDead = true;
            }
            
            if (defender.ControlSkillType.HasAnyState(SubjectedStateType.IsInvisible))
            {
                Debug.Log($"PlayerConnectionId: {defenderId} is invisible, cannot attack.");
                damageResultData.IsDodged = true;
            }
            return damageResultData;
        }

        public static DamageResultData[] HandleAttack(uint attackerId, ref PlayerPredictablePropertyState playerPredictablePropertyState, 
            ref Dictionary<uint, PlayerPredictablePropertyState> defenders, Func<float, float, float, float, DamageCalculateResultData> getDamageFunction)
        {
            var playerState = playerPredictablePropertyState;
            var propertyState = playerState.MemoryProperty;
            var attack = propertyState[PropertyTypeEnum.Attack].CurrentValue;
            var critical = propertyState[PropertyTypeEnum.CriticalRate].CurrentValue;
            var criticalDamage = propertyState[PropertyTypeEnum.CriticalDamageRatio].CurrentValue;
            var defenderPropertyStates = defenders;
            var damageResultDatas = new List<DamageResultData>();
            foreach (var (key, defenderPropertyState) in defenders)
            {
                if (defenderPropertyState.ControlSkillType.HasAnyState(SubjectedStateType.IsInvisible))
                {
                    continue;
                }
                var resultData = new DamageResultData();
                resultData.HitterUid = attackerId;
                resultData.DefenderUid = key;
                resultData.DamageCalculateResult = new DamageCalculateResultData();
                resultData.DamageCalculateResult.Damage = 0;
                resultData.DamageCalculateResult.IsCritical = false;
                resultData.IsDodged = false;
                resultData.DamageType = DamageType.Physical;
                resultData.DamageCastType = DamageCastType.NormalAttack;
                resultData.DamageRatio = 0;
                resultData.IsDead = false;
                if (defenderPropertyState.ControlSkillType.HasAnyState(SubjectedStateType.IsInvisible))
                {
                    Debug.Log($"PlayerConnectionId: {key} is invisible, cannot attack.");
                    resultData.IsDodged = true;
                    damageResultDatas.Add(resultData);
                    continue;
                }
                var defense = defenderPropertyState.MemoryProperty[PropertyTypeEnum.Defense].CurrentValue;
                resultData.DamageCalculateResult = getDamageFunction(attack, defense, critical, criticalDamage);
                resultData.IsCritical = resultData.DamageCalculateResult.IsCritical;
                resultData.DamageRatio = resultData.DamageCalculateResult.Damage /
                                         defenderPropertyState.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue;
                var remainHealth = GetRemainHealth(defenderPropertyState.MemoryProperty[PropertyTypeEnum.Health], resultData.DamageCalculateResult.Damage);
                defenderPropertyState.MemoryProperty[PropertyTypeEnum.Health] = remainHealth;
                defenderPropertyStates[key] = defenderPropertyState;
                resultData.HpRemainRatio = remainHealth.CurrentValue / defenderPropertyState.MemoryProperty[PropertyTypeEnum.Health].MaxCurrentValue;
                if (remainHealth.CurrentValue <= 0)
                {
                    resultData.IsDead = true;
                }
                damageResultDatas.Add(resultData);
            }
            defenders = defenderPropertyStates;
            playerPredictablePropertyState = playerState;   
            return damageResultDatas.ToArray();
        }
        
        public void HandlePropertyRecover(ref PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            var propertyState = playerPredictablePropertyState;
            if (propertyState.IsDead)
            {
                return;
            }
            var state = propertyState.MemoryProperty;
            var healthRecover = state[PropertyTypeEnum.HealthRecovery];
            var strengthRecover = state[PropertyTypeEnum.StrengthRecovery];
            var health = state[PropertyTypeEnum.Health];
            var strength = state[PropertyTypeEnum.Strength];
            if (!Mathf.Approximately(health.CurrentValue, health.MaxCurrentValue))
            {
                state[PropertyTypeEnum.Health] = health.UpdateCalculator(health, new BuffIncreaseData
                {
                    increaseType = BuffIncreaseType.Current,
                    increaseValue = healthRecover.CurrentValue * 0.25f,
                });
            }

            if (!Mathf.Approximately(strength.CurrentValue, strength.MaxCurrentValue))
            {
                state[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
                {
                    increaseType = BuffIncreaseType.Current,
                    increaseValue = strengthRecover.CurrentValue * 0.25f,
                });
            }
            playerPredictablePropertyState = propertyState;
        }
        
        public void HandleAnimationCommand(ref PlayerPredictablePropertyState playerPredictablePropertyState, AnimationState command, float animationCost)
        {
            var cost = animationCost;
            if (cost <= 0)
            {
                return;
            }
            var propertyState = playerPredictablePropertyState;
            var state = propertyState.MemoryProperty;
            cost *= command == AnimationState.Sprint ? Time.fixedDeltaTime : 1f;
            //Debug.Log($"playerPropertyCalculator: execute {command} animation, current: {state[PropertyTypeEnum.Strength].CurrentValue}, cost: {cost}");
            var strength = state[PropertyTypeEnum.Strength];
            if (cost > strength.CurrentValue)
            {
                //Debug.LogError($"PlayerPropertySyncSystem: {connectionId} does not have enough strength to perform {command} animation.");
                return;
            }

            if (cost > 0)
            {
                state[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
                {
                    increaseType = BuffIncreaseType.Current,
                    increaseValue = cost,
                    operationType = OperationType.Subtract,
                });
            }
            playerPredictablePropertyState = propertyState;
        }

        public void HandleEnvironmentChange(ref PlayerPredictablePropertyState playerPredictablePropertyState, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            // var propertyState = playerPredictablePropertyState;
            // var speed = propertyState.MemoryProperty[PropertyTypeEnum.Speed];
            // var sprintRatio = propertyState.MemoryProperty[PropertyTypeEnum.SprintSpeedRatio];
            // var stairsRatio = propertyState.MemoryProperty[PropertyTypeEnum.StairsSpeedRatio];
            // if (!hasInputMovement)
            // {
            //     speed = speed.UpdateCalculator(speed, new BuffIncreaseData
            //     {
            //         increaseType = BuffIncreaseType.CorrectionFactor,
            //         increaseValue = 0,
            //     });
            // }
            // else
            // {
            //     switch (environmentType)
            //     {
            //         case PlayerEnvironmentState.InAir:
            //             break;
            //         case PlayerEnvironmentState.OnGround:
            //             speed = speed.UpdateCalculator(speed, new BuffIncreaseData
            //             {
            //                 increaseType = BuffIncreaseType.CorrectionFactor,
            //                 increaseValue = isSprinting ? sprintRatio.CurrentValue : 1,
            //                 operationType = BuffOperationType.Multiply,
            //             });
            //             break;
            //         case PlayerEnvironmentState.OnStairs:
            //             speed = speed.UpdateCalculator(speed, new BuffIncreaseData
            //             {
            //                 increaseType = BuffIncreaseType.CorrectionFactor,
            //                 increaseValue = isSprinting ? sprintRatio.CurrentValue * stairsRatio.CurrentValue : stairsRatio.CurrentValue,
            //                 operationType = BuffOperationType.Multiply,
            //             });
            //             break;
            //         case PlayerEnvironmentState.Swimming:
            //             break;
            //         default:
            //             throw new ArgumentOutOfRangeException(nameof(environmentType), environmentType, null);
            //     }
            // }
            // propertyState.MemoryProperty[PropertyTypeEnum.Speed] = speed;
            // playerPredictablePropertyState = propertyState;
        }

        private static PropertyCalculator GetRemainHealth(PropertyCalculator health, float damage)
        {
            return health.UpdateCalculator(health, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = damage,
                operationType = OperationType.Subtract,
            });
        }

        public PlayerPredictablePropertyState HandlePlayerDeath(PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            var propertyState = playerPredictablePropertyState;
            var state = propertyState.MemoryProperty;
            var health = state[PropertyTypeEnum.Health];
            var remainHealth = health.UpdateCurrentValue(0);
            state[PropertyTypeEnum.Health] = remainHealth;
            playerPredictablePropertyState = propertyState;
            return playerPredictablePropertyState;
        }

        public PlayerPredictablePropertyState HandlePlayerRespawn(PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            var propertyState = playerPredictablePropertyState;
            var state = propertyState.MemoryProperty;
            var health = state[PropertyTypeEnum.Health];
            var remainHealth = health.UpdateCurrentValue(health.MaxCurrentValue);
            var strength = state[PropertyTypeEnum.Strength];
            var remainStrength = strength.UpdateCurrentValue(strength.MaxCurrentValue);
            state[PropertyTypeEnum.Strength] = remainStrength;
            state[PropertyTypeEnum.Health] = remainHealth;
            playerPredictablePropertyState = propertyState;
            return playerPredictablePropertyState;
        }

        public void UpdateProperty(PropertyTypeEnum propertyType, PropertyCalculator property)
        {
            Properties.TryAdd(propertyType, property);
            Properties[propertyType] = property;
            OnPropertyChanged?.Invoke(propertyType, property.CurrentValue);
        }

        public void UpdateState(SubjectedStateType subjectType)
        {
            SubjectedStateType = subjectType;
        }

        public void Execute(int index)
        {
            
        }
        
        private enum SpeedState
        {
            Up,
            Low,
            Equal,
            None,
        }
    }

    public struct PropertyCalculatorConstant
    {
        public float TickRate;
        public bool IsServer;
        public PropertyConfig PropertyConfig;
        public PlayerConfigData PlayerConfig;
        public bool IsClient;
        public bool IsLocalPlayer;
    }
}