using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.State;
using Unity.Jobs;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using PropertyCalculator = HotUpdate.Scripts.Network.PredictSystem.State.PropertyCalculator;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerPropertyCalculator : IPlayerStateCalculator, IJobParallelFor
    {
        private static PropertyCalculatorConstant _calculatorConstant;
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
            _calculatorConstant = constant;
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

        public void HandleAttack(ref PlayerPredictablePropertyState playerPredictablePropertyState, ref Dictionary<int, PlayerPredictablePropertyState> defenders, Func<float, float, float, float, float> getDamageFunction)
        {
            var playerState = playerPredictablePropertyState;
            var propertyState = playerState.Properties;
            var attack = propertyState[PropertyTypeEnum.Attack].CurrentValue;
            var critical = propertyState[PropertyTypeEnum.CriticalRate].CurrentValue;
            var criticalDamage = propertyState[PropertyTypeEnum.CriticalDamageRatio].CurrentValue;
            var defenderPropertyStates = defenders;
            foreach (var (key, defenderPropertyState) in defenders)
            {
                if (defenderPropertyState.IsInvisible)
                {
                    Debug.Log($"PlayerConnectionId: {key} is invisible, cannot attack.");
                    continue;
                }
                var defense = defenderPropertyState.Properties[PropertyTypeEnum.Defense].CurrentValue;
                var damage = getDamageFunction(attack, defense, critical, criticalDamage);
                if (damage <= 0)
                {
                    continue;
                }
                var remainHealth = GetRemainHealth(defenderPropertyState.Properties[PropertyTypeEnum.Health], damage);
                defenderPropertyState.Properties[PropertyTypeEnum.Health] = remainHealth;
                defenderPropertyStates[key] = defenderPropertyState;
            }
            defenders = defenderPropertyStates;
            playerPredictablePropertyState = playerState;   
        }
        
        public void HandlePropertyRecover(ref PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            var propertyState = playerPredictablePropertyState;
            var state = propertyState.Properties;
            var healthRecover = state[PropertyTypeEnum.HealthRecovery];
            var strengthRecover = state[PropertyTypeEnum.StrengthRecovery];
            var health = state[PropertyTypeEnum.Health];
            var strength = state[PropertyTypeEnum.Strength];
            state[PropertyTypeEnum.Health] = health.UpdateCalculator(health, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = healthRecover.CurrentValue * _calculatorConstant.TickRate,
            });
            state[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = strengthRecover.CurrentValue * _calculatorConstant.TickRate,
            });
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
            var state = propertyState.Properties;
            cost *= command == AnimationState.Sprint ? _calculatorConstant.TickRate : 1f;
            var strength = state[PropertyTypeEnum.Strength];
            if (cost > strength.CurrentValue)
            {
                //Debug.LogError($"PlayerPropertySyncSystem: {connectionId} does not have enough strength to perform {command} animation.");
                return;
            }
            state[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = cost,
                operationType = BuffOperationType.Subtract,
            });
            playerPredictablePropertyState = propertyState;
        }

        public void HandleEnvironmentChange(ref PlayerPredictablePropertyState playerPredictablePropertyState, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            var propertyState = playerPredictablePropertyState;
            var speed = propertyState.Properties[PropertyTypeEnum.Speed];
            var sprintRatio = propertyState.Properties[PropertyTypeEnum.SprintSpeedRatio];
            var stairsRatio = propertyState.Properties[PropertyTypeEnum.StairsSpeedRatio];
            if (!hasInputMovement)
            {
                speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                {
                    increaseType = BuffIncreaseType.CorrectionFactor,
                    increaseValue = 0,
                });
            }
            else
            {
                switch (environmentType)
                {
                    case PlayerEnvironmentState.InAir:
                        break;
                    case PlayerEnvironmentState.OnGround:
                        speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                        {
                            increaseType = BuffIncreaseType.CorrectionFactor,
                            increaseValue = isSprinting ? sprintRatio.CurrentValue : 1,
                            operationType = BuffOperationType.Multiply,
                        });
                        break;
                    case PlayerEnvironmentState.OnStairs:
                        speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                        {
                            increaseType = BuffIncreaseType.CorrectionFactor,
                            increaseValue = isSprinting ? sprintRatio.CurrentValue * stairsRatio.CurrentValue : stairsRatio.CurrentValue,
                            operationType = BuffOperationType.Multiply,
                        });
                        break;
                    case PlayerEnvironmentState.Swimming:
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(environmentType), environmentType, null);
                }
            }
            propertyState.Properties[PropertyTypeEnum.Speed] = speed;
            playerPredictablePropertyState = propertyState;
        }

        private PropertyCalculator GetRemainHealth(PropertyCalculator health, float damage)
        {
            return health.UpdateCalculator(health, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = damage,
                operationType = BuffOperationType.Subtract,
            });
        }

        public void UpdateProperty(PropertyTypeEnum propertyType, PropertyCalculator property)
        {
            if (!Properties.ContainsKey(propertyType))
            {
                Properties.Add(propertyType, property);
            }
            Properties[propertyType] = property;
            OnPropertyChanged?.Invoke(propertyType, property.CurrentValue);
        }

        public void Execute(int index)
        {
            
        }
    }

    public struct PropertyCalculatorConstant
    {
        public float TickRate;
        public bool IsServer;

        public PropertyCalculatorConstant(float tickRate, bool isServer)
        {
            TickRate = tickRate;
            IsServer = isServer;
        }
    }
}