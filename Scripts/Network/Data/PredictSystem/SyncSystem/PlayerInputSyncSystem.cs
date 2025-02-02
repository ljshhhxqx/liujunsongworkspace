using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem
{
    public class PlayerInputSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerInputPredictionState> _inputPredictionStates = new Dictionary<int, PlayerInputPredictionState>();
        
        protected override void OnClientProcessStateUpdate(string stateJson)
        {
            var playerStates = JsonConvert.DeserializeObject<Dictionary<int, PlayerInputState>>(stateJson);
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
            var playerPredictableState = player.GetComponent<PlayerInputPredictionState>();
            var playerInputState = new PlayerInputState(new PlayerGameStateData(), new PlayerInputStateData());
            PropertyStates.Add(connectionId, playerInputState);
            _inputPredictionStates.Add(connectionId, playerPredictableState);
        }

        public override CommandType HandledCommandType => CommandType.Input;
        public override IPropertyState ProcessCommand(INetworkCommand command)
        {
            if (command is InputCommand inputCommand)
            {
                var header = inputCommand.GetHeader();
            }

            return null;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _inputPredictionStates[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(IPropertyState oldState, IPropertyState newState)
        {
            return false;
        }
    }
}