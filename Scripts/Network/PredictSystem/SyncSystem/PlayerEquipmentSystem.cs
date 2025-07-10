using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.Server.InGame;
using MemoryPack;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerEquipmentSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerEquipmentSyncState> _playerEquipmentSyncStates = new Dictionary<int, PlayerEquipmentSyncState>();
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        protected override CommandType CommandType => CommandType.Equipment;
        
        protected override void OnGameStart(bool isGameStarted)
        {
            if (!isGameStarted)
            {
                return;
            }
            //游戏开始才能开始倒计时
            UpdateEquipmentCd(_tokenSource.Token).Forget();
        }

        private async UniTaskVoid UpdateEquipmentCd(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1 / GameSyncManager.TickSeconds), cancellationToken: token);
                foreach (var playerId in PropertyStates.Keys)
                {
                    var playerState = PropertyStates[playerId];
                    if (playerState is PlayerEquipmentState playerEquipmentSyncState)
                    {
                        PlayerEquipmentState.UpdateCheckerCd(ref playerEquipmentSyncState, GameSyncManager.TickSeconds);
                    }
                }
            }
        }

        protected override void OnClientProcessStateUpdate(int connectionId, byte[] state, CommandType commandType)
        {
            if (commandType!= CommandType.Equipment)
            {
                return;
            }
            var playerStates = NetworkCommandExtensions.DeserializePlayerState(state);
            // if (playerStates is not PlayerEquipmentState equipmentState)
            // {
            //     Debug.LogError($"Player {playerStates.GetStateType().ToString()} equipment state is not PlayerEquipmentState.");
            //     return;
            // }
            if (PropertyStates.ContainsKey(connectionId))
            {
                PropertyStates[connectionId] = playerStates;
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerEquipmentSyncState>();
            var playerEquipmentState = new PlayerEquipmentState();
            playerEquipmentState.EquipmentDatas = new MemoryList<EquipmentData>();//<EquipmentData>();
            PropertyStates.TryAdd(connectionId, playerEquipmentState);
            _playerEquipmentSyncStates.TryAdd(connectionId, playerPredictableState);
            RpcSetPlayerEquipmentState(connectionId, NetworkCommandExtensions.SerializePlayerState(playerEquipmentState));
        }

        [ClientRpc]
        private void RpcSetPlayerEquipmentState(int connectionId, byte[] playerEquipmentState)
        {
            var syncState = NetworkServer.connections[connectionId].identity.GetComponent<PlayerEquipmentSyncState>();
            var playerState = NetworkCommandExtensions.DeserializePlayerState(playerEquipmentState);
            syncState.ApplyState(playerState);
        }

        public override CommandType HandledCommandType => CommandType.Equipment;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            var playerState = PropertyStates[header.ConnectionId];
            if (!header.CommandType.HasAnyState(CommandType.Equipment) || playerState is not PlayerEquipmentState playerEquipmentState)
                return null;
            if (command is EquipmentCommand equipmentCommand)
            {
                var equipmentData = PlayerEquipmentCalculator.CommandEquipment(equipmentCommand, ref playerEquipmentState);
                PropertyStates[header.ConnectionId] = playerEquipmentState;
                return PropertyStates[header.ConnectionId];
            }
            if (command is TriggerCommand triggerCommand)
            {
                //todo: 根据触发类型查阅是否有触发效果，没有则忽略，有则获取相关装备数据，并根据配置计算触发效果
                var data = PlayerEquipmentCalculator.GetDataByTriggerType(playerEquipmentState, triggerCommand.TriggerType);
                var battleConfigData = PlayerItemCalculator.GetBattleEffectConditionConfigData(data.Item2, data.Item3);
                if (battleConfigData.id == 0)
                    return PropertyStates[header.ConnectionId];
                var targetIds = PlayerInGameManager.Instance.GetPlayerIdsByTargetType(header.ConnectionId,
                    battleConfigData.targetCount, battleConfigData.targetType);
                PlayerEquipmentCalculator.CommandTrigger(triggerCommand, ref playerEquipmentState, targetIds, data.Item3, data.Item2, data.Item1);
                PropertyStates[header.ConnectionId] = playerEquipmentState;
            }

            return PropertyStates[header.ConnectionId];
        }

        public override byte[] GetPlayerSerializedState(int connectionId)
        {
            if (PropertyStates.TryGetValue(connectionId, out var playerState))
            {
                if (playerState is PlayerEquipmentState playerEquipmentState)
                {
                    return NetworkCommandExtensions.SerializePlayerState(playerEquipmentState);
                }

                Debug.LogError($"Player {connectionId} equipment state is not PlayerEquipmentState.");
                return null;
            }
            Debug.LogError($"Player {connectionId} equipment state not found.");
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
            _tokenSource?.Cancel();
            _tokenSource?.Dispose();
        }
    }
}