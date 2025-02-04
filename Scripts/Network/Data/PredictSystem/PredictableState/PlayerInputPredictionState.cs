using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState
{
    public class PlayerInputPredictionState : PredictableStateBase
    {
        protected override IPropertyState CurrentState { get; set; }
        public PlayerInputState InputState => (PlayerInputState) CurrentState;
        private KeyAnimationConfig _keyAnimationConfig;
        protected override CommandType CommandType => CommandType.Input;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _keyAnimationConfig = configProvider.GetConfig<KeyAnimationConfig>();
        }
        public override CommandType HandledCommandType => CommandType.Input;
        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is null || state is not PlayerInputState propertyState)
                return false;
            return !InputState.IsEqual(propertyState);
        }

        public List<AnimationState> GetAnimationStates()
        {
            return _keyAnimationConfig.GetAllActiveActions();
        }
        
        public override void Simulate(INetworkCommand command)
        {
            
        }
    }
}