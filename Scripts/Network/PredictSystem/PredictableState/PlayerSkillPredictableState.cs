using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerSkillPredictableState : SyncStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Skill;
        protected override void SetState<T>(T state)
        {
            CurrentState = state;
        }
        
        public void ApplyState<T>(T state) where T : ISyncPropertyState
        {
            SetState(state);
        }

        protected override void ProcessCommand(INetworkCommand networkCommand)
        {
            throw new System.NotImplementedException();
        }
    }
}