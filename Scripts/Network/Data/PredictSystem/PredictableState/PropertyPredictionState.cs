using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState
{
    public class PropertyPredictionState: PredictableStateBase
    {
        protected override IPropertyState ServerState { get; set; }
        
        public override void SetServerState<T>(T state)
        {
            if (NeedsReconciliation(state))
            {
                ApplyServerState(state);
            }
        }

        public override CommandType HandledCommandType => CommandType.Property;
        
        public override void ApplyServerState<T>(T state)
        {
            
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            return false;
        }

        public override void Simulate(INetworkCommand command)
        {
            
        }
    }
}