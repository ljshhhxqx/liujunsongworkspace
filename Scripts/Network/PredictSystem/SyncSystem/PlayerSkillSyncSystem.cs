using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Skill;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerSkillSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerSkillSyncState> _playerSkillSyncStates = new Dictionary<int, PlayerSkillSyncState>();
        private SkillConfig _skillConfig;
        private PlayerInGameManager _playerInGameManager;
        private readonly CancellationTokenSource _tokenSource = new CancellationTokenSource();
        private int _currentSkillId;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _skillConfig = configProvider.GetConfig<SkillConfig>();
            _playerInGameManager = Object.FindObjectOfType<PlayerInGameManager>();
            UpdateEquipmentCd(_tokenSource.Token).Forget();
        }

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
        private async UniTaskVoid UpdateEquipmentCd(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1 / GameSyncManager.TickRate), cancellationToken: token);
                foreach (var playerId in PropertyStates.Keys)
                {
                    var playerState = PropertyStates[playerId];
                    if (playerState is PlayerSkillState playerSkillState)
                    {
                        foreach (var key in playerSkillState.SkillCheckers.Keys)
                        {
                            var skillChecker = playerSkillState.SkillCheckers[key];
                            if (skillChecker.IsSkillEffect())
                            {
                                PlayerSkillCalculator.UpdateSkillFlyEffect(playerId, GameSyncManager.TickRate, skillChecker, _playerInGameManager.GetHitPlayers);
                            }

                            if (!skillChecker.IsSkillNotInCd())
                            {
                                var cooldown = skillChecker.GetCooldownHeader();
                                cooldown = cooldown.Update(GameSyncManager.TickRate);
                                skillChecker.SetCooldownHeader(cooldown);
                            }
                            playerSkillState.SkillCheckers[key] = skillChecker;
                        }
                        
                    }
                    PropertyStates[playerId] = playerState;
                }
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
                var checker = GetSkillChecker(skillCommand, playerSkillState);
                var skillCommonHeader = checker.GetCommonSkillCheckerHeader();
                //释放的技能与当前技能不一致
                if (skillCommand.SkillConfigId != skillCommonHeader.ConfigId)
                {
                    Debug.LogError($"Player {header.ConnectionId} skill checker has different config ID {skillCommonHeader.ConfigId} for player {header.ConnectionId}");
                    return PropertyStates[header.ConnectionId];
                }
                var skillData = _skillConfig.GetSkillData(skillCommand.SkillConfigId);
                var propertySync = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
                var playerProperty = propertySync.GetPropertyCalculator(header.ConnectionId, skillData.costProperty);
                if (!PlayerSkillCalculator.ExecuteSkill(playerSkillState, skillData, playerProperty, skillCommand,
                        skillCommand.KeyCode, _playerInGameManager.GetHitPlayers, out var position))
                {
                    Debug.LogError($"Player {header.ConnectionId} execute skill failed");
                    return PropertyStates[header.ConnectionId];
                }
                PropertyStates[header.ConnectionId] = playerSkillState;
                var playerSkillSyncState = _playerSkillSyncStates[header.ConnectionId];
                playerSkillSyncState.RpcSpawnSkillEffect(skillCommand.SkillConfigId, position, skillCommand.KeyCode);
                return PropertyStates[header.ConnectionId];
            }
            if (command is SkillLoadCommand skillLoadCommand)
            {
                var checker = GetSkillChecker(skillLoadCommand, playerSkillState);
                var skillCommonHeader = checker.GetCommonSkillCheckerHeader();
                var skillData = _skillConfig.GetSkillData(skillLoadCommand.SkillConfigId);
                if (!skillLoadCommand.IsLoad)
                {
                    if (skillLoadCommand.SkillConfigId != skillCommonHeader.ConfigId)
                    {
                        Debug.LogError($"Player {header.ConnectionId} skill checker has different config ID {skillCommonHeader.ConfigId} for player {header.ConnectionId}");
                        return PropertyStates[header.ConnectionId];
                    }

                    playerSkillState.SkillCheckers.Remove(skillLoadCommand.KeyCode);
                }
                else
                {
                    checker = PlayerSkillCalculator.CreateSkillChecker(skillData);
                    playerSkillState.SkillCheckers.Add(skillLoadCommand.KeyCode, checker);
                }

                PropertyStates[header.ConnectionId] = playerSkillState;
                return PropertyStates[header.ConnectionId];
            }
            return playerSkillState;
        }

        private ISkillChecker GetSkillChecker(INetworkCommand command, PlayerSkillState playerSkillState)
        {
            var header = command.GetHeader();
            if (command is SkillCommand skillCommand)
            {
                var checker = playerSkillState.SkillCheckers[skillCommand.KeyCode];
                if (checker == null)
                {
                    Debug.LogError($"Player {header.ConnectionId} has no skill checker");
                    return null;
                }
                return checker;
            } 
            if (command is SkillLoadCommand skillLoadCommand)
            {
                var checker = playerSkillState.SkillCheckers[skillLoadCommand.KeyCode];
                if (checker == null)
                {
                    Debug.LogError($"Player {header.ConnectionId} has no skill checker");
                    return null;
                }
                return checker;
            }
            return null;
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
        
        public override void Clear()
        {
            _playerSkillSyncStates.Clear();
            PropertyStates.Clear();
            _tokenSource.Cancel();
        }
    }
}