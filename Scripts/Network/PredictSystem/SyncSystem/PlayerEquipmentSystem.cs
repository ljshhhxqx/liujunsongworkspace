using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using Mirror;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerEquipmentSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerEquipmentSyncState> _playerEquipmentSyncStates = new Dictionary<int, PlayerEquipmentSyncState>();
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();

        [Inject]
        public override void Initialize(GameSyncManager gameSyncManager)
        {
            base.Initialize(gameSyncManager);
            UpdateEquipmentCd(_tokenSource.Token).Forget();
        }

        private async UniTaskVoid UpdateEquipmentCd(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1 / GameSyncManager.TickRate), cancellationToken: token);
                foreach (var playerId in PropertyStates.Keys)
                {
                    var playerState = PropertyStates[playerId];
                    if (playerState is PlayerEquipmentState playerEquipmentSyncState)
                    {
                        PlayerEquipmentState.UpdateCheckerCd(ref playerEquipmentSyncState, GameSyncManager.TickRate);
                        PropertyStates[playerId] = playerEquipmentSyncState;
                    }
                }
            }
        }

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerEquipmentState>>(state);
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
            var playerPredictableState = player.GetComponent<PlayerEquipmentSyncState>();
            var playerInputState = new PlayerEquipmentState();
            PropertyStates.Add(connectionId, playerInputState);
            _playerEquipmentSyncStates.Add(connectionId, playerPredictableState);
        }

        public override CommandType HandledCommandType => CommandType.Equipment;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            if (command is EquipmentCommand equipmentCommand)
            {
                var header = equipmentCommand.Header;
                if (header.CommandType.HasAnyState(CommandType.Equipment))
                {
                    var playerState = PropertyStates[header.ConnectionId];
                    if (playerState is PlayerEquipmentState playerEquipmentState)
                    {
                        //PlayerEquipmentState.EquipPlayerOrNot();
                        return playerEquipmentState;
                    }
                }
            }
            else if (command is TriggerCommand triggerCommand)
            {
                var header = triggerCommand.Header;
                if (header.CommandType.HasAnyState(CommandType.Equipment))
                {
                    var playerState = PropertyStates[header.ConnectionId];
                    if (playerState is PlayerEquipmentState playerEquipmentState)
                    {
                        var data = triggerCommand.TriggerData;
                        var checkParams = MemoryPackSerializer.Deserialize<IConditionCheckerParameters>(data);
                        var isCheckPassed = PlayerEquipmentState.CheckConditions(ref playerEquipmentState, checkParams);
                        PropertyStates[header.ConnectionId] = playerEquipmentState;
                        if (isCheckPassed)
                        {
                            return playerEquipmentState;
                        }
                    }
                }
            }

            return null;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _playerEquipmentSyncStates[connectionId];
            playerPredictableState.ApplyState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _playerEquipmentSyncStates.Clear();
            _tokenSource?.Dispose();
            _tokenSource?.Cancel();
        }
    }
}