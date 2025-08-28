﻿using System;
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
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
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
        protected override CommandType CommandType => CommandType.Skill;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _skillConfig = configProvider.GetConfig<SkillConfig>();
            _playerInGameManager = PlayerInGameManager.Instance;
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

        

        protected override void OnClientProcessStateUpdate(int connectionId, byte[] state, CommandType commandType)
        {
            if (commandType != CommandType.Skill)
            {
                return;
            }
            var playerStates = NetworkCommandExtensions.DeserializePlayerState(state);
            
            // if (playerStates is not PlayerSkillState playerSkillState)
            // {
            //     Debug.LogError($"Player {playerStates.GetStateType().ToString()} skill state is not PlayerSkillState.");
            //     return;
            // }

            if (PropertyStates.ContainsKey(connectionId))
            {
                PropertyStates[connectionId] = playerStates;
            }
        }
        
        public override byte[] GetPlayerSerializedState(int connectionId)
        {
            if (PropertyStates.TryGetValue(connectionId, out var playerState))
            {
                if (playerState is PlayerSkillState playerSkillState)
                {
                    //playerSkillState.SetSkillCheckerDatas();
                    return NetworkCommandExtensions.SerializePlayerState(playerSkillState).Item1;
                }

                Debug.LogError($"Player {connectionId} equipment state is not PlayerPredictablePropertyState.");
                return null;
            }
            Debug.LogError($"Player {connectionId} equipment state not found.");
            return null;
        }
        
        private async UniTaskVoid UpdateEquipmentCd(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(1 / GameSyncManager.TickSeconds), cancellationToken: token);
                foreach (var playerId in PropertyStates.Keys)
                {
                    var playerState = PropertyStates[playerId];
                    var playerConnection = GameSyncManager.GetPlayerConnection(playerId);
                    var skillDic = playerConnection.SkillCheckerDict;
                    if (playerState is PlayerSkillState playerSkillState)
                    {
                        if (skillDic == null || skillDic.Count == 0)
                        {
                            return;
                        }
                        foreach (var key in skillDic.Keys)
                        {
                            var skillChecker = skillDic[key];
                            if (skillChecker.IsSkillEffect())
                            {
                                PlayerSkillCalculator.UpdateSkillFlyEffect(playerId, GameSyncManager.TickSeconds, skillChecker, _playerInGameManager.GetHitPlayers);
                            }

                            if (!skillChecker.IsSkillNotInCd())
                            {
                                var cooldown = skillChecker.GetCooldownHeader();
                                cooldown = cooldown.Update(GameSyncManager.TickSeconds);
                                skillChecker.SetCooldownHeader(cooldown);
                            }
                            skillDic[key] = skillChecker;
                        }
                        
                    }
                    PropertyStates[playerId] = playerState;
                }
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerSkillSyncState>();
            var skillState = new PlayerSkillState();
            skillState.SkillCheckerDatas = new MemoryList<SkillCheckerData>();
            PropertyStates.TryAdd(connectionId, skillState);
            _playerSkillSyncStates.TryAdd(connectionId, playerPredictableState);
            RpcSetPlayerSkillState(connectionId, NetworkCommandExtensions.SerializePlayerState(skillState).Item1);
        }


        [ClientRpc]
        private void RpcSetPlayerSkillState(int connectionId, byte[] playerSkillState)
        {
            var syncState = NetworkServer.connections[connectionId].identity.GetComponent<PlayerSkillSyncState>();
            var playerState = NetworkCommandExtensions.DeserializePlayerState(playerSkillState);
            syncState.InitializeState(playerState);
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
                var checker = GetSkillChecker(skillCommand.KeyCode, header.ConnectionId);
                var skillCommonHeader = checker.GetCommonSkillCheckerHeader();
                //释放的技能与当前技能不一致
                if (skillCommand.SkillConfigId != skillCommonHeader.ConfigId)
                {
                    Debug.LogError($"Player {header.ConnectionId} skill checker has different config ID {skillCommonHeader.ConfigId} for player {header.ConnectionId}");
                    return PropertyStates[header.ConnectionId];
                }
                var playerConnection = GameSyncManager.GetPlayerConnection(skillCommand.Header.ConnectionId);
                var skillCheckers = playerConnection.GetNowAnimationCooldownsDict();
                var skillData = _skillConfig.GetSkillData(skillCommand.SkillConfigId);
                var propertySync = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
                var playerProperty = propertySync.GetPropertyCalculator(header.ConnectionId, skillData.costProperty);
                var playerComponent = GameSyncManager.GetPlayerConnection(header.ConnectionId);
                if (!PlayerSkillCalculator.ExecuteSkill(playerComponent, skillData, playerProperty, skillCommand,
                        skillCommand.KeyCode, _playerInGameManager.GetHitPlayers, out var position))
                {
                    Debug.LogError($"Player {header.ConnectionId} execute skill failed");
                    return PropertyStates[header.ConnectionId];
                }
                PropertyStates[header.ConnectionId] = playerSkillState;
                var playerSkillSyncState = _playerSkillSyncStates[header.ConnectionId];
                playerSkillSyncState.SpawnSkillEffect(skillCommand.SkillConfigId, position, skillCommand.KeyCode);
                var skillHeader = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property,
                    CommandAuthority.Server, CommandExecuteType.Immediate);
                GameSyncManager.EnqueueServerCommand(new PropertyUseSkillCommand
                {
                    Header = skillHeader,
                    SkillConfigId = skillCommand.SkillConfigId,
                });
                return PropertyStates[header.ConnectionId];
            }
            if (command is SkillLoadCommand skillLoadCommand)
            {
                Debug.Log($"[SkillLoadCommand] Player {header.ConnectionId} skill {skillLoadCommand.SkillConfigId} start load");
                
                ISkillChecker checker;
                var playerConnection = GameSyncManager.GetPlayerConnection(skillLoadCommand.Header.ConnectionId);
                var skillCheckers = playerConnection.SkillCheckerDict;
                if (skillCheckers.ContainsKey(skillLoadCommand.KeyCode))
                {
                    return PropertyStates[header.ConnectionId];
                }
                var skillData = _skillConfig.GetSkillData(skillLoadCommand.SkillConfigId);
                if (!skillLoadCommand.IsLoad)
                {
                    Debug.Log($"[SkillLoadCommand] Player {header.ConnectionId} skill {skillLoadCommand.SkillConfigId}-{skillLoadCommand.KeyCode} unload");
                    checker = skillCheckers[skillLoadCommand.KeyCode];
                    var skillCommonHeader = checker.GetCommonSkillCheckerHeader();
                    if (skillLoadCommand.SkillConfigId != skillCommonHeader.ConfigId)
                    {
                        Debug.LogError($"Player {header.ConnectionId} skill checker has different config ID {skillCommonHeader.ConfigId} for player {header.ConnectionId}");
                        return PropertyStates[header.ConnectionId];
                    }
                    skillCheckers.Remove(skillLoadCommand.KeyCode);
                }
                else
                {
                    if (skillCheckers.ContainsKey(skillLoadCommand.KeyCode))
                    {
                        skillCheckers.Remove(skillLoadCommand.KeyCode);
                    }
                    Debug.Log($"[SkillLoadCommand] Player {header.ConnectionId} skill {skillLoadCommand.SkillConfigId}-{skillLoadCommand.KeyCode} load");
                    checker = PlayerSkillCalculator.CreateSkillChecker(skillData, skillLoadCommand.KeyCode);
                    skillCheckers.Add(skillLoadCommand.KeyCode, checker);
                }
                var skillLoadOverrideAnimation = new SkillLoadOverloadAnimationCommand
                {
                    Header = new NetworkCommandHeader
                    {
                        ConnectionId = header.ConnectionId,
                        CommandType = CommandType.Input,
                        ExecuteType = CommandExecuteType.Immediate,
                        Authority = CommandAuthority.Server,
                    },
                    KeyCode = skillLoadCommand.KeyCode,
                    Cooldowntime = skillData.cooldown,
                    IsLoad = skillLoadCommand.IsLoad,
                    Cost = skillData.cost,
                };
                GameSyncManager.EnqueueServerCommand(skillLoadOverrideAnimation);

                PropertyStates[header.ConnectionId] = playerSkillState;
                return playerSkillState;
            }
            return playerSkillState;
        }

        private ISkillChecker GetSkillChecker(AnimationState keyCode, int connectionId)
        {
            var playerConnection = GameSyncManager.GetPlayerConnection(connectionId);

            var skillCheckers = playerConnection.SkillCheckerDict;
            var checker = skillCheckers[keyCode];
            if (checker == null)
            {
                Debug.LogError($"Player {keyCode} has no skill checker");
                return null;
            }
            return checker;
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

        public SkillConfigData GetSkillConfigData(AnimationState animationState, int headerConnectionId)
        {
            var playerConnection = GameSyncManager.GetPlayerConnection(headerConnectionId);
            var skillCheckers = playerConnection.SkillCheckerDict;
            if (skillCheckers == null || skillCheckers.Count == 0)
            {
                return default;
            }

            if (skillCheckers.TryGetValue(animationState, out var checker))
            {
                var skillConfigData = _skillConfig.GetSkillData(checker.GetCommonSkillCheckerHeader().ConfigId);
                return skillConfigData;
            }
            return default;
        }
    }
}