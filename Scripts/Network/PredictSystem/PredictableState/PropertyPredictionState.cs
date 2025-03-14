﻿using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using PropertyCalculator = HotUpdate.Scripts.Network.PredictSystem.State.PropertyCalculator;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PropertyPredictionState: PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        private AnimationConfig _animationConfig;

        public PlayerPredictablePropertyState PlayerPredictablePropertyState => (PlayerPredictablePropertyState)CurrentState;

        protected override CommandType CommandType => CommandType.Property;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
        }
        
        public float GetProperty(PropertyTypeEnum propertyType)
        {
            return PlayerPredictablePropertyState.Properties[propertyType].CurrentValue;
        }
        
        public float GetMaxProperty(PropertyTypeEnum propertyType)
        {
            if (PlayerPredictablePropertyState.Properties[propertyType].IsResourceProperty())
            {
                return PlayerPredictablePropertyState.Properties[propertyType].MaxCurrentValue;
            }
            return PlayerPredictablePropertyState.Properties[propertyType].CurrentValue;
        }

        public event Action<PropertyTypeEnum, PropertyCalculator> OnPropertyChanged;
        
        public override void ApplyServerState<T>(T state)
        {
            if (state is PlayerPredictablePropertyState propertyState)
            {
                base.ApplyServerState(propertyState);
                PropertyChanged(propertyState);
            }
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is null || state is not PlayerPredictablePropertyState propertyState)
                return false;
            return !PlayerPredictablePropertyState.IsEqual(propertyState);
        }

        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            // if (header is { isClientCommand: true, commandType: CommandType.Property } && command is PropertyCommand propertyCommand)
            // {
            //     var propertyState = PlayerPropertyState;
            //     switch (propertyCommand.Operation)
            //     {
            //         case PropertyCommandAutoRecover:
            //             var healthRecover = propertyState.Properties[PropertyTypeEnum.HealthRecovery];
            //             var strengthRecover = propertyState.Properties[PropertyTypeEnum.StrengthRecovery];
            //             var health = propertyState.Properties[PropertyTypeEnum.Health];
            //             var strength = propertyState.Properties[PropertyTypeEnum.Strength];
            //             propertyState.Properties[PropertyTypeEnum.Health] = health.UpdateCalculator(health, new BuffIncreaseData
            //             {
            //                 increaseType = BuffIncreaseType.Current,
            //                 increaseValue = healthRecover.CurrentValue * Time.deltaTime,
            //             });
            //             propertyState.Properties[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
            //             {
            //                 increaseType = BuffIncreaseType.Current,
            //                 increaseValue = strengthRecover.CurrentValue * Time.deltaTime,
            //             });
            //             break;
            //         case PropertyCommandEnvironmentChange environmentChange:
            //             var speed = propertyState.Properties[PropertyTypeEnum.Speed];
            //             var sprintRatio = propertyState.Properties[PropertyTypeEnum.SprintSpeedRatio];
            //             var stairsRatio = propertyState.Properties[PropertyTypeEnum.StairsSpeedRatio];
            //             if (!environmentChange.hasInputMovement)
            //             {
            //                 speed = speed.UpdateCalculator(speed, new BuffIncreaseData
            //                 {
            //                     increaseType = BuffIncreaseType.CorrectionFactor,
            //                     increaseValue = 0,
            //                 });
            //             }
            //             else
            //             {
            //                 switch (environmentChange.environmentType)
            //                 {
            //                     case PlayerEnvironmentState.InAir:
            //                         break;
            //                     case PlayerEnvironmentState.OnGround:
            //                         speed = speed.UpdateCalculator(speed, new BuffIncreaseData
            //                         {
            //                             increaseType = BuffIncreaseType.CorrectionFactor,
            //                             increaseValue = environmentChange.isSprinting ? sprintRatio.CurrentValue : 1,
            //                             operationType = BuffOperationType.Multiply,
            //                         });
            //                         break;
            //                     case PlayerEnvironmentState.OnStairs:
            //                         speed = speed.UpdateCalculator(speed, new BuffIncreaseData
            //                         {
            //                             increaseType = BuffIncreaseType.CorrectionFactor,
            //                             increaseValue = environmentChange.isSprinting ? sprintRatio.CurrentValue * stairsRatio.CurrentValue : stairsRatio.CurrentValue,
            //                             operationType = BuffOperationType.Multiply,
            //                         });
            //                         break;
            //                     case PlayerEnvironmentState.Swimming:
            //                         break;
            //                     default:
            //                         throw new ArgumentOutOfRangeException(nameof(environmentChange.environmentType), environmentChange.environmentType, null);
            //                 }
            //             }
            //             propertyState.Properties[PropertyTypeEnum.Speed] = speed;
            //             break;
            //         case PropertyAnimationCommand animationCommand:
            //             var animationType = _animationConfig.GetActionType(animationCommand.animationState);
            //             if (animationType is ActionType.Interaction or ActionType.None)
            //             {
            //                 break;
            //             }
            //             var cost = _animationConfig.GetPlayerAnimationCost(animationCommand.animationState);
            //             cost *= animationCommand.animationState == AnimationState.Sprint ? Time.deltaTime : 1f;
            //             strength = propertyState.Properties[PropertyTypeEnum.Strength];
            //             if (cost <= 0 || cost > strength.CurrentValue)
            //             {
            //                 return;
            //             }
            //             propertyState.Properties[PropertyTypeEnum.Strength] = strength.UpdateCalculator(strength, new BuffIncreaseData
            //             {
            //                 increaseType = BuffIncreaseType.Current,
            //                 increaseValue = cost,
            //                 operationType = BuffOperationType.Subtract,
            //             });
            //             break;
            //         default:
            //             Debug.LogError($"PlayerPropertySyncSystem: server command {propertyCommand.Operation.GetType().Name} cannot be handled by client.");
            //             break;
            //     }
            //todo:上述代码需要更换为直接调用PlayerComponentController,不再使用
            var propertyState = PlayerPredictablePropertyState;
            PropertyChanged(propertyState);
            
        }

        public void RegisterProperties(PlayerPredictablePropertyState predictablePropertyState)
        {
            PropertyChanged(predictablePropertyState);
        }

        private void PropertyChanged(PlayerPredictablePropertyState predictablePropertyState)
        {
            foreach (var property in predictablePropertyState.Properties)
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