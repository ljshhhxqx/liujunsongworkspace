using System;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
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
            if (CurrentState is not PlayerPredictablePropertyState playerState || header.CommandType.HasAnyState(CommandType.Property))
                return;
            if (command is PropertyAutoRecoverCommand)
            {
                HandlePropertyRecover(ref playerState);
            }
            else if (command is PropertyClientAnimationCommand clientAnimationCommand)
            {
                HandleAnimationCommand(ref playerState, clientAnimationCommand.AnimationState);
            }
            else
            {
                return;
            }
            var propertyState = PlayerPredictablePropertyState;
            PropertyChanged(propertyState);
            
        }
        
        private void HandlePropertyRecover(ref PlayerPredictablePropertyState propertyState)
        {
            PlayerComponentController.HandlePropertyRecover(ref propertyState);
            PropertyChanged(propertyState);
        }

        private void HandleAnimationCommand(ref PlayerPredictablePropertyState propertyState, AnimationState command)
        {
            var cost = _animationConfig.GetPlayerAnimationCost(command);
            if (cost <= 0)
            {
                return;
            }
            PlayerComponentController.HandleAnimationCost(ref propertyState, command, cost);
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
                    continue;
                }
                OnPropertyChanged?.Invoke(property.Key, property.Value);
                if (property.Key == PropertyTypeEnum.AttackSpeed)
                {
                    PlayerComponentController.SetAnimatorSpeed(AnimationState.Attack, property.Value.CurrentValue);
                }
            }
        }
    }
}