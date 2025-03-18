using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using Mirror;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using InputCommand = HotUpdate.Scripts.Network.PredictSystem.Data.InputCommand;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PlayerInputState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerInputState;
using PropertyClientAnimationCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyClientAnimationCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerInputPredictionState : PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        public PlayerInputState InputState => (PlayerInputState) CurrentState;
        private PropertyPredictionState _propertyPredictionState;
        private KeyAnimationConfig _keyAnimationConfig;
        private AnimationConfig _animationConfig;
        private JsonDataConfig _jsonDataConfig;
        
        protected override CommandType CommandType => CommandType.Input;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isApplyingState;
        
        public event Action<PlayerGameStateData> OnPlayerStateChanged; 
        public event Action<PlayerAnimationCooldownState> OnPlayerAnimationCooldownChanged;
        public event Action<PlayerInputStateData> OnPlayerInputStateChanged;
        public event Func<bool> IsInSpecialState;

        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _keyAnimationConfig = configProvider.GetConfig<KeyAnimationConfig>();

            UpdateAnimationCooldowns(_cancellationTokenSource.Token, gameSyncManager.TickRate).Forget();
        }
        
        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is null || state is not PlayerInputState propertyState)
                return false;
            return !InputState.IsEqual(propertyState);
        }

        public override void ApplyServerState<T>(T state) 
        {
            if (state is not PlayerInputState propertyState)
                return;
            _isApplyingState = true;
            base.ApplyServerState(propertyState);
            var snapshot = propertyState.PlayerAnimationCooldownState.AnimationCooldowns;
            PlayerComponentController.RefreshSnapData(snapshot);
            OnPlayerStateChanged?.Invoke(propertyState.PlayerGameStateData);
            OnPlayerAnimationCooldownChanged?.Invoke(propertyState.PlayerAnimationCooldownState);
            _isApplyingState = false;
        }

        public List<AnimationState> GetAnimationStates()
        {
            return _keyAnimationConfig.GetAllActiveActions();
        }
        
        /// <summary>
        /// 计算玩家控制逻辑、动画状态
        /// </summary>
        /// <param name="command"></param>
        [Client]
        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType.HasAnyState(CommandType) && command is InputCommand inputCommand && IsInSpecialState?.Invoke() == false)
            {
                var info = _animationConfig.GetAnimationInfo(inputCommand.CommandAnimationState);
                var actionType = _animationConfig.GetActionType(inputCommand.CommandAnimationState);
                var health = _propertyPredictionState.GetProperty(PropertyTypeEnum.Health);
                if (health == 0 || actionType is not ActionType.Movement and ActionType.Interaction)
                {
                    return;
                }

                var animationCooldowns = PlayerComponentController.GetNowAnimationCooldowns();
                var cooldownInfo = animationCooldowns.Find(x => x.AnimationState == inputCommand.CommandAnimationState);
                if (info.cooldown > 0)
                {
                    if (cooldownInfo == null || !cooldownInfo.IsReady())
                    {
                        Debug.LogWarning($"Animation {inputCommand.CommandAnimationState} is on cooldown.");
                        return;
                    }
                }

                if (info.cost > 0)
                {
                    var currentStrength = _propertyPredictionState.GetProperty(PropertyTypeEnum.Strength);
                    if (!_animationConfig.IsStrengthEnough(inputCommand.CommandAnimationState, currentStrength, GameSyncManager.TickRate))
                    {
                        Debug.LogWarning($"Not enough strength to perform {inputCommand.CommandAnimationState}.");
                        return;
                    }
                    _propertyPredictionState.AddPredictedCommand(new PropertyClientAnimationCommand
                    {
                        AnimationState = inputCommand.CommandAnimationState,
                        Header = header,
                    });
                }
                cooldownInfo?.Use(); 

                OnPlayerInputStateChanged?.Invoke(new PlayerInputStateData
                {
                    InputAnimations = inputCommand.InputAnimationStates.ToList(),
                    Command = inputCommand.CommandAnimationState,
                    InputMovement = inputCommand.InputMovement
                });
            }
        }

        private async UniTaskVoid UpdateAnimationCooldowns(CancellationToken token, float deltaTime)
        {
            while (token.IsCancellationRequested == false && !_isApplyingState)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(deltaTime), cancellationToken: token);
                PlayerComponentController.UpdateAnimation(deltaTime);
            }
        }
    }
}