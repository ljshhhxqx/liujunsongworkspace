using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using MemoryPack;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using CooldownSnapshotData = HotUpdate.Scripts.Network.PredictSystem.Data.CooldownSnapshotData;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PlayerInputState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerInputState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem
{
    public class PlayerInputSyncSystem : BaseSyncSystem
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly Dictionary<int, PlayerInputPredictionState> _inputPredictionStates = new Dictionary<int, PlayerInputPredictionState>();
        private readonly Dictionary<int, List<IAnimationCooldown>> _animationCooldowns = new Dictionary<int, List<IAnimationCooldown>>();
        private AnimationConfig _animationConfig;
        private JsonDataConfig _jsonDataConfig;
        private List<IAnimationCooldown> _animationCooldownConfig;

        [Inject]
        private void InitContainers(IConfigProvider configProvider)
        {
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            Observable.EveryUpdate().ThrottleFirst(TimeSpan.FromMilliseconds(GameSyncManager.TickRate))
                .Subscribe(_ => Update(GameSyncManager.TickRate))
                .AddTo(_disposables);
        }
        
        private void Update(float deltaTime)
        {
            UpdatePlayerAnimationCooldowns(deltaTime);
        }

        private void UpdatePlayerAnimationCooldowns(float deltaTime)
        {
            foreach (var playerId in _animationCooldowns.Keys)
            {
                var animationCooldowns = _animationCooldowns[playerId];
                for (var i = animationCooldowns.Count - 1; i >= 0; i--)
                {
                    var animationCooldown = animationCooldowns[i];
                    animationCooldown.Update(deltaTime);
                    if (animationCooldown.IsReady())
                    {
                        animationCooldowns.Remove(animationCooldown);
                    }
                }
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
            _animationCooldowns.Add(connectionId, GetAnimationCooldowns());
            BindAniEvents(connectionId, player.GetComponent<IAttackAnimationEvent>());
        }

        [Server]
        private void BindAniEvents(int connectionId, IAttackAnimationEvent animationEvent)
        {
            var animationCooldowns = _animationCooldowns[connectionId];
            if (animationCooldowns.Find(x => x.AnimationState == AnimationState.Attack) is not AttackCooldown attackCooldown)
            {
                Debug.LogError("AttackCooldown not found in animation cooldowns.");
                return;
            }
            attackCooldown.BindAnimationEvents(animationEvent);
            attackCooldown.AttackPointReached
                .Subscribe(_ => HandlePlayerAttack(connectionId))
                .AddTo(_disposables);
        }

        private void HandlePlayerAttack(int connectionId)
        {
            
        }

        private List<IAnimationCooldown> GetAnimationCooldowns()
        {
            var list = new List<IAnimationCooldown>();
            var animationStates = Enum.GetValues(typeof(AnimationState)).Cast<AnimationState>();
            foreach (var animationState in animationStates)
            {
                var info = _animationConfig.GetAnimationInfo(animationState);
                if (info.state == AnimationState.Attack)
                {
                    list.Add(new AttackCooldown(animationState, info.cooldown, _jsonDataConfig.PlayerConfig.AttackComboMaxCount, _jsonDataConfig.PlayerConfig.AttackComboWindow));
                    continue;
                }

                if (info.cooldown > 0)
                {
                    list.Add(new AnimationCooldown(animationState, info.cooldown, 0));
                }
            }
            return list;
        }

        public override CommandType HandledCommandType => CommandType.Input;
        public override IPropertyState ProcessCommand(INetworkCommand command)
        {
            if (command is InputCommand inputCommand)
            {
                var header = inputCommand.GetHeader();
                var playerSyncSystem = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
                var playerController = GameSyncManager.GetPlayerConnection(header.ConnectionId);
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
                inputCommand.CommandAnimationState = commandAnimation;
                var actionType = _animationConfig.GetActionType(inputCommand.CommandAnimationState);
                if (actionType is not ActionType.Movement and ActionType.Interaction)
                {
                    Debug.LogWarning($"Player {header.ConnectionId} input animation {inputCommand.CommandAnimationState} is not supported.");
                    return null;
                }
                
                if (!_animationCooldowns.TryGetValue(header.ConnectionId, out var animationCooldowns))
                {
                    return null;
                }
                var info = _animationConfig.GetAnimationInfo(commandAnimation);
                
                //验证冷却时间是否已到
                var cooldownInfo = animationCooldowns.Find(x => x.AnimationState == commandAnimation);
                if (info.cooldown != 0 || info.cost > 0)
                {
                    if (cooldownInfo == null || !cooldownInfo.IsReady())
                    {
                        Debug.LogWarning($"Player {header.ConnectionId} input animation {commandAnimation} is not ready.");
                        return null;
                    }
                
                    //验证是否耐力值足够
                    if (playerProperty[PropertyTypeEnum.Strength].CurrentValue < info.cost)
                    {
                        Debug.LogWarning($"Player {header.ConnectionId} input animation {commandAnimation} cost {info.cost} strength, but strength is {playerProperty[PropertyTypeEnum.Strength].CurrentValue}.");
                        return null;
                    }
                    
                    // 扣除耐力值
                    GameSyncManager.EnqueueServerCommand(new PropertyServerAnimationCommand
                    {
                        Header = NetworkCommandHeader.Create(0, CommandType.Property, GameSyncManager.CurrentTick, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(), CommandAuthority.Server),
                        AnimationState = commandAnimation,
                    });

                    cooldownInfo.Use();
                }
                
                var playerGameStateData = playerController.HandleServerMoveAndAnimation(inputStateData);
                PropertyStates[header.ConnectionId] = new PlayerInputState(playerGameStateData, new PlayerAnimationCooldownState(GetCooldownSnapshotData(header.ConnectionId)));
                return PropertyStates[header.ConnectionId];
            }

            return null;
        }
        
        private List<CooldownSnapshotData> GetCooldownSnapshotData(int connectionId)
        {
            var animationCooldowns = _animationCooldowns[connectionId];
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

        public override bool HasStateChanged(IPropertyState oldState, IPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            _disposables.Dispose();
        }
    }
}