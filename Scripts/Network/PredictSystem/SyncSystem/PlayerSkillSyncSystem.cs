using System.Collections.Generic;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using Mirror;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerSkillSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerSkillSyncState> _playerSkillSyncStates = new Dictionary<int, PlayerSkillSyncState>();

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerSkillState>>(state);
            foreach (var playerState in playerStates)
            {
                if (!PropertyStates.ContainsKey(playerState.Key))
                {
                    continue;
                }
                PropertyStates[playerState.Key] = playerState.Value;
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerSkillSyncState>();
            var inputState = new PlayerSkillState();
            PropertyStates.Add(connectionId, inputState);
            _playerSkillSyncStates.Add(connectionId, playerPredictableState);
        }

        public override CommandType HandledCommandType => CommandType.Skill;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            var playerState = PropertyStates[header.ConnectionId];
            if (!header.CommandType.HasAnyState(CommandType.Skill) || playerState is not PlayerSkillState playerSkillState)
                return null;
            if (command is SkillCommand skillCommand)
            {
                
            }
            return playerSkillState;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _playerSkillSyncStates[connectionId];
            playerPredictableState.ApplyState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return true;
        }
    }
}