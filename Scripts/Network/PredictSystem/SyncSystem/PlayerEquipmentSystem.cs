using System;
using System.Collections.Generic;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UnityEngine;
using VContainer;
using EquipmentData = AOTScripts.Data.EquipmentData;
using ISyncPropertyState = AOTScripts.Data.ISyncPropertyState;
using PlayerEquipmentState = AOTScripts.Data.PlayerEquipmentState;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerEquipmentSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerEquipmentSyncState> _playerEquipmentSyncStates = new Dictionary<int, PlayerEquipmentSyncState>();
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private IConfigProvider _configProvider;
        private PlayerInGameManager _playerInGameManager;
        protected override CommandType CommandType => CommandType.Equipment;

        [Inject]
        private void Init(IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
            _playerInGameManager = playerInGameManager;
            _configProvider = configProvider;
        }

        protected override void OnGameStart(bool isGameStarted)
        {
            if (!isGameStarted)
            {
                return;
            }
            //游戏开始才能开始倒计时
            UpdateEquipmentCd(_tokenSource.Token).Forget();
        }

        public bool TryGetPlayerConditionChecker(int connectionId, TriggerType triggerType, out List<IConditionChecker> conditionCheckers)
        {
            var state = GetState<PlayerEquipmentState>(connectionId);
            return PlayerEquipmentCalculator.TryGetEquipmentTrigger(state, triggerType, out conditionCheckers);
        }

        private async UniTaskVoid UpdateEquipmentCd(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1 / GameSyncManager.TickSeconds),ignoreTimeScale: true, cancellationToken: token);
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

        protected override void RegisterState(int connectionId, uint netId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerEquipmentSyncState>();
            var playerEquipmentState = new PlayerEquipmentState();
            playerEquipmentState.EquipmentDatas = new MemoryList<EquipmentData>();
            PropertyStates.AddOrUpdate(connectionId, playerEquipmentState);
            _playerEquipmentSyncStates.AddOrUpdate(connectionId, playerPredictableState);
            RpcSetPlayerEquipmentState(netId, NetworkCommandExtensions.SerializePlayerState(playerEquipmentState).Buffer);
        }

        [ClientRpc]
        private void RpcSetPlayerEquipmentState(uint netId, byte[] playerEquipmentState)
        {
            var player = GameSyncManager.GetPlayerConnection(netId);
            var syncState = player.GetComponent<PlayerEquipmentSyncState>();
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
                PlayerEquipmentCalculator.CommandEquipment(equipmentCommand, ref playerEquipmentState);
                PropertyStates[header.ConnectionId] = playerEquipmentState;
                return PropertyStates[header.ConnectionId];
            }
            if (command is TriggerCommand triggerCommand && triggerCommand.TriggerType!= TriggerType.None)
            {
                //todo: 根据触发类型查阅是否有触发效果，没有则忽略，有则获取相关装备数据，并根据配置计算触发效果
                Debug.Log($"TriggerCommand {triggerCommand.TriggerType} received, {triggerCommand.TriggerData}.");
                var data = PlayerEquipmentCalculator.GetDataByTriggerType(playerEquipmentState, triggerCommand.TriggerType);
                var battleConfigData = PlayerItemCalculator.GetBattleEffectConditionConfigData(data.Item2, data.Item3);
                if (battleConfigData.id == 0)
                    return PropertyStates[header.ConnectionId];
                var targetIds = _playerInGameManager.GetPlayerIdsByTargetType(header.ConnectionId,
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
                    return NetworkCommandExtensions.SerializePlayerState(playerEquipmentState).Buffer;
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