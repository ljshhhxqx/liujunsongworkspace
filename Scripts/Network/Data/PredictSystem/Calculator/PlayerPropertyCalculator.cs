using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.Calculator
{
    public class PlayerPropertyCalculator : IPlayerStateCalculator
    {
        private static PropertyCalculatorConstant _calculatorConstant;
        public bool IsClient { get; private set; }
        public event Action<PropertyTypeEnum, float> OnPropertyChanged;
        public Dictionary<PropertyTypeEnum, PropertyCalculator> Properties { get; private set; }

        public PlayerPropertyCalculator(Dictionary<PropertyTypeEnum, PropertyCalculator> properties, bool isClient)
        {
            Properties = properties;
            IsClient = isClient;
        }
        
        public static void SetCalculatorConstant(PropertyCalculatorConstant constant)
        {
            _calculatorConstant = constant;
        }

        public Dictionary<PropertyTypeEnum, PropertyCalculator> Clone()
        {
            var properties = new Dictionary<PropertyTypeEnum, PropertyCalculator>();
            foreach (var property in Properties)
            {
                properties.Add(property.Key, new PropertyCalculator(property.Key, property.Value.PropertyDataValue, property.Value.MaxValue, property.Value.MinValue));
            }

            return properties;
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

        public void HandleAttack(PlayerPropertyState[] defenders, Func<float, float, float, float, float> getDamageFunction, out List<int> deadIndexes)
        {
            var propertyState = Properties;
            var attack = propertyState[PropertyTypeEnum.Attack].CurrentValue;
            var critical = propertyState[PropertyTypeEnum.CriticalRate].CurrentValue;
            var criticalDamage = propertyState[PropertyTypeEnum.CriticalDamageRatio].CurrentValue;
            var defenderPropertyStates = defenders;
            deadIndexes = new List<int>();
            for (int i = 0; i < defenderPropertyStates.Length; i++)
            {
                var defenderPropertyState = defenderPropertyStates[i];
                var defense = defenderPropertyState.Properties[PropertyTypeEnum.Defense].CurrentValue;
                var damage = getDamageFunction(attack, defense, critical, criticalDamage);
                if (damage <= 0)
                {
                    continue;
                }
                var remainHealth = GetRemainHealth(defenderPropertyState.Properties[PropertyTypeEnum.Health], damage);
                defenderPropertyState.Properties[PropertyTypeEnum.Health] = remainHealth;
                defenders[i] = defenderPropertyState;
                if (remainHealth.CurrentValue <= 0)
                {
                    deadIndexes.Add(i);
                }
            }
            Properties = propertyState;   
        }
        
        public void HandlePropertyRecover()
        {
            var state = Properties;
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
            Properties = state;
        }

        public void HandleAnimationCommand(AnimationState command, float animationCost)
        {
            var cost = animationCost;
            if (cost <= 0)
            {
                return;
            }
            var state = Properties;
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
            Properties = state;
        }

        public void HandleEnvironmentChange(bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            var playerState = Properties;
            var speed = playerState[PropertyTypeEnum.Speed];
            var sprintRatio = playerState[PropertyTypeEnum.SprintSpeedRatio];
            var stairsRatio = playerState[PropertyTypeEnum.StairsSpeedRatio];
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
            playerState[PropertyTypeEnum.Speed] = speed;
            Properties = playerState;
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
    }

    public struct PropertyCalculatorConstant
    {
        public float TickRate;
        
        public PropertyCalculatorConstant(float tickRate)
        {
            TickRate = tickRate;
        }
    }
}