using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using Mirror;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerSkillSystem : BaseSyncSystem
    {
        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            
        }

        public override CommandType HandledCommandType => CommandType.Skill;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            return null;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }
    }
}