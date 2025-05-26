using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using Mirror;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class SkillSyncSystem : BaseSyncSystem
    {
        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            throw new System.NotImplementedException();
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            throw new System.NotImplementedException();
        }

        public override CommandType HandledCommandType => CommandType.Skill;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            throw new System.NotImplementedException();
        }

        public override void SetState<T>(int connectionId, T state)
        {
            throw new System.NotImplementedException();
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            throw new System.NotImplementedException();
        }
    }
}