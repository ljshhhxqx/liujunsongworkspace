using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationEvent = HotUpdate.Scripts.Config.ArrayConfig.AnimationEvent;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using CooldownSnapshotData = HotUpdate.Scripts.Network.PredictSystem.Data.CooldownSnapshotData;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using InputCommand = HotUpdate.Scripts.Network.PredictSystem.Data.InputCommand;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PlayerInputState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerInputState;
using PropertyAttackCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyAttackCommand;
using PropertyServerAnimationCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyServerAnimationCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerInputSyncSystem : BaseSyncSystem
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly Dictionary<int, PlayerInputPredictionState> _inputPredictionStates = new Dictionary<int, PlayerInputPredictionState>();
        private AnimationConfig _animationConfig;
        private JsonDataConfig _jsonDataConfig;
        private List<IAnimationCooldown> _animationCooldownConfig;
        private CancellationTokenSource _cts;

        [Inject]
        private void InitContainers(IConfigProvider configProvider)
        {
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            UpdatePlayerAnimationAsync(_cts.Token, GameSyncManager.TickRate).Forget();
        }
        
        private async UniTaskVoid UpdatePlayerAnimationAsync(CancellationToken token, float deltaTime)
        {
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(deltaTime), cancellationToken: token);
                UpdatePlayerAnimationCooldowns(deltaTime);
            }
        }

        private void UpdatePlayerAnimationCooldowns(float deltaTime)
        {
            foreach (var connectionsKey in NetworkServer.connections.Keys)
            {
                var playerController = GameSyncManager.GetPlayerConnection(connectionsKey);
                playerController.UpdateAnimation(deltaTime);
            }
        }

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerInputState>>(state);
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
            var playerInputState = new PlayerInputState(new PlayerGameStateData(), new PlayerAnimationCooldownState());
            PropertyStates.Add(connectionId, playerInputState);
            _inputPredictionStates.Add(connectionId, playerPredictableState);
            BindAniEvents(connectionId);
        }

        [Server]
        private void BindAniEvents(int connectionId)
        {
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            var animationCooldowns = playerController.GetNowAnimationCooldowns();
            var attackCooldown = animationCooldowns.Find(x => x.AnimationState == AnimationState.Attack);
            if (attackCooldown is KeyframeComboCooldown attackComboCooldown)
            {
                attackComboCooldown.EventStream
                    .Where(x => x == AnimationEvent.OnAttack)
                    .Subscribe(x => HandlePlayerAttack(connectionId))
                    .AddTo(_disposables);
            }
            var rollCooldown = animationCooldowns.Find(x => x.AnimationState == AnimationState.Roll);
            if (rollCooldown is KeyframeComboCooldown rollComboCooldown)
            {
                rollComboCooldown.EventStream
                    .Where(x => x == AnimationEvent.OnRollStart)
                    .Subscribe(x => HandlePlayerRoll(connectionId, true))
                    .AddTo(_disposables);
                rollComboCooldown.EventStream
                    .Where(x => x == AnimationEvent.OnRollEnd)
                    .Subscribe(x => HandlePlayerRoll(connectionId, false))
                    .AddTo(_disposables);
            }
        }

        private void HandlePlayerRoll(int connectionId, bool isRollStart)
        {
            GameSyncManager.EnqueueServerCommand(new PropertyInvincibleChangedCommand()
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Property),
                IsInvincible = isRollStart,
            });
        }

        private void HandlePlayerAttack(int connectionId)
        {
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            var propertySyncSystem = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            var playerProperty = propertySyncSystem.GetPlayerProperty(connectionId);
            var attackConfigData = new AttackConfigData(playerProperty[PropertyTypeEnum.AttackRadius].CurrentValue, playerProperty[PropertyTypeEnum.AttackAngle].CurrentValue, playerProperty[PropertyTypeEnum.AttackHeight].CurrentValue);
            var defenders = playerController.HandleAttack(new AttackParams(playerController.transform.position,
                playerController.transform.forward, connectionId, playerController.netId, attackConfigData));
            GameSyncManager.EnqueueServerCommand(new PropertyAttackCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Property),
                AttackerId = connectionId,
                TargetIds = defenders,
            });
        }

        public override CommandType HandledCommandType => CommandType.Input;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            if (command is InputCommand inputCommand)
            {
                var header = inputCommand.GetHeader();
                var playerSyncSystem = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
                var playerController = GameSyncManager.GetPlayerConnection(header.ConnectionId);
                if (playerController.IsInSpecialState())
                {
                    return null;
                }
                var playerProperty = playerSyncSystem.GetPlayerProperty(header.ConnectionId);
                //验证玩家是否存在或者是否已死亡
                if (playerProperty == null || playerProperty[PropertyTypeEnum.Health].CurrentValue <= 0)
                {
                    return null;
                }

                var inputStateData = new PlayerInputStateData
                {
                    InputMovement = inputCommand.InputMovement,
                    InputAnimations = inputCommand.InputAnimationStates.ToList(),
                };
                
                //获取可以执行的动画
                var commandAnimation = playerController.GetCurrentAnimationState(inputStateData);
                inputStateData.Command = commandAnimation;

                inputCommand.CommandAnimationState = commandAnimation;
                var actionType = _animationConfig.GetActionType(inputCommand.CommandAnimationState);
                if (actionType is not ActionType.Movement and ActionType.Interaction)
                {
                    Debug.LogWarning($"Player {header.ConnectionId} input animation {inputCommand.CommandAnimationState} is not supported.");
                    return null;
                }
                
                var playerAnimationCooldowns = playerController.GetNowAnimationCooldowns();
                if (playerAnimationCooldowns.Count == 0)
                {
                    return null;
                }
                var info = _animationConfig.GetAnimationInfo(commandAnimation);
                
                //验证冷却时间是否已到
                var cooldownInfo = playerAnimationCooldowns.Find(x => x.AnimationState == commandAnimation);

                if (info.cooldown != 0)
                {
                    if (cooldownInfo == null || !cooldownInfo.IsReady())
                    {
                        Debug.LogWarning($"Player {header.ConnectionId} input animation {commandAnimation} is not ready.");
                        return null;
                    }
                }

                if (info.cost > 0)
                {
                    //验证是否耐力值足够
                    if (!_animationConfig.IsStrengthEnough(inputCommand.CommandAnimationState, playerProperty[PropertyTypeEnum.Strength].CurrentValue, GameSyncManager.TickRate))
                    {
                        Debug.LogWarning($"Player {header.ConnectionId} input animation {commandAnimation} cost {info.cost} strength, but strength is {playerProperty[PropertyTypeEnum.Strength].CurrentValue}.");
                        return null;
                    }
                    
                    // 扣除耐力值
                    GameSyncManager.EnqueueServerCommand(new PropertyServerAnimationCommand
                    {
                        Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property),
                        AnimationState = commandAnimation,
                    });

                }
                cooldownInfo?.Use();
                
                var playerGameStateData = playerController.HandleServerMoveAndAnimation(inputStateData);
                PropertyStates[header.ConnectionId] = new PlayerInputState(playerGameStateData, new PlayerAnimationCooldownState(GetCooldownSnapshotData(header.ConnectionId)));
                return PropertyStates[header.ConnectionId];
            }

            return null;
        }
        
        private List<CooldownSnapshotData> GetCooldownSnapshotData(int connectionId)
        {
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            var animationCooldowns = playerController.GetNowAnimationCooldowns();
            var snapshotData = new List<CooldownSnapshotData>();
            foreach (var animationCooldown in animationCooldowns)
            {
                snapshotData.Add(CooldownSnapshotData.Create(animationCooldown));
            }
            return snapshotData;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _inputPredictionStates[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _disposables.Dispose();
            _disposables.Clear();
            _cts?.Cancel();
            _cts?.Dispose();
            _inputPredictionStates.Clear();
        }
    }
}