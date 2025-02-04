using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState
{
    public class PropertyPredictionState: PredictableStateBase
    {
        protected override IPropertyState CurrentState { get; set; }
        private AnimationConfig _animationConfig;

        public PlayerPropertyState PlayerPropertyState => (PlayerPropertyState)CurrentState;

        protected override CommandType CommandType => CommandType.Property;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
        }
        
        public float GetProperty(PropertyTypeEnum propertyType)
        {
            return PlayerPropertyState.Properties[propertyType].CurrentValue;
        }
        
        public float GetMaxProperty(PropertyTypeEnum propertyType)
        {
            if (PlayerPropertyState.Properties[propertyType].IsResourceProperty())
            {
                return PlayerPropertyState.Properties[propertyType].MaxCurrentValue;
            }
            return PlayerPropertyState.Properties[propertyType].CurrentValue;
        }

        public event Action<PropertyTypeEnum, PropertyCalculator> OnPropertyChanged;
        
        public override CommandType HandledCommandType => CommandType.Property;
        
        public override void ApplyServerState<T>(T state)
        {
            if (state is PlayerPropertyState propertyState)
            {
                base.ApplyServerState(propertyState);
                PropertyChanged(propertyState);
            }
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is null || state is not PlayerPropertyState propertyState)
                return false;
            return !PlayerPropertyState.IsEqual(propertyState);
        }

        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header is { isClientCommand: true, commandType: CommandType.Property } && command is PropertyCommand propertyCommand)
            {
                var propertyState = PlayerPropertyState;
                switch (propertyCommand.Operation)
                {
                    case PropertyCommandAutoRecover:
                        var healthRecover = propertyState.Properties[PropertyTypeEnum.HealthRecovery];
                        var strengthRecover = propertyState.Properties[PropertyTypeEnum.StrengthRecovery];
                        var health = propertyState.Properties[PropertyTypeEnum.Health];
                        var strength = propertyState.Properties[PropertyTypeEnum.Strength];
                        propertyState.Properties[PropertyTypeEnum.Health] = health.UpdateCalculator(health, new BuffIncreaseData
                        {
                            increaseType = BuffIncreaseType.Current,
                            increaseValue = healthRecover.CurrentValue * Time.deltaTime,
                        });
                        propertyState.Properties[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
                        {
                            increaseType = BuffIncreaseType.Current,
                            increaseValue = strengthRecover.CurrentValue * Time.deltaTime,
                        });
                        break;
                    case PropertyCommandEnvironmentChange environmentChange:
                        var speed = propertyState.Properties[PropertyTypeEnum.Speed];
                        var sprintRatio = propertyState.Properties[PropertyTypeEnum.SprintSpeedRatio];
                        var stairsRatio = propertyState.Properties[PropertyTypeEnum.StairsSpeedRatio];
                        if (!environmentChange.hasInputMovement)
                        {
                            speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                            {
                                increaseType = BuffIncreaseType.CorrectionFactor,
                                increaseValue = 0,
                            });
                        }
                        else
                        {
                            switch (environmentChange.environmentType)
                            {
                                case PlayerEnvironmentState.InAir:
                                    break;
                                case PlayerEnvironmentState.OnGround:
                                    speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                                    {
                                        increaseType = BuffIncreaseType.CorrectionFactor,
                                        increaseValue = environmentChange.isSprinting ? sprintRatio.CurrentValue : 1,
                                        operationType = BuffOperationType.Multiply,
                                    });
                                    break;
                                case PlayerEnvironmentState.OnStairs:
                                    speed = speed.UpdateCalculator(speed, new BuffIncreaseData
                                    {
                                        increaseType = BuffIncreaseType.CorrectionFactor,
                                        increaseValue = environmentChange.isSprinting ? sprintRatio.CurrentValue * stairsRatio.CurrentValue : stairsRatio.CurrentValue,
                                        operationType = BuffOperationType.Multiply,
                                    });
                                    break;
                                case PlayerEnvironmentState.Swimming:
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(nameof(environmentChange.environmentType), environmentChange.environmentType, null);
                            }
                        }
                        propertyState.Properties[PropertyTypeEnum.Speed] = speed;
                        break;
                    case PropertyAnimationCommand animationCommand:
                        var animationType = _animationConfig.GetActionType(animationCommand.animationState);
                        if (animationType is ActionType.Interaction or ActionType.None)
                        {
                            break;
                        }
                        var cost = _animationConfig.GetPlayerAnimationCost(animationCommand.animationState);
                        cost *= animationCommand.animationState == AnimationState.Sprint ? Time.deltaTime : 1f;
                        strength = propertyState.Properties[PropertyTypeEnum.Strength];
                        if (cost <= 0 || cost > strength.CurrentValue)
                        {
                            return;
                        }
                        propertyState.Properties[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
                        {
                            increaseType = BuffIncreaseType.Current,
                            increaseValue = cost,
                            operationType = BuffOperationType.Subtract,
                        });
                        break;
                    default:
                        Debug.LogError($"PlayerPropertySyncSystem: server command {propertyCommand.Operation.GetType().Name} cannot be handled by client.");
                        break;
                }
                PropertyChanged(propertyState);
            }
        }

        public void RegisterProperties(PlayerPropertyState propertyState)
        {
            PropertyChanged(propertyState);
        }

        private void PropertyChanged(PlayerPropertyState propertyState)
        {
            foreach (var property in propertyState.Properties)
            {
                if (property.Value.IsResourceProperty())
                {
                    OnPropertyChanged?.Invoke(property.Key, property.Value);
                }
                OnPropertyChanged?.Invoke(property.Key, property.Value);
            }
        }
    }
}