using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using Mirror;
using UniRx;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PlayerInputState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerInputState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState
{
    public class PlayerInputPredictionState : PredictableStateBase
    {
        protected override IPropertyState CurrentState { get; set; }
        public PlayerInputState InputState => (PlayerInputState) CurrentState;
        private PropertyPredictionState _propertyPredictionState;
        private KeyAnimationConfig _keyAnimationConfig;
        private AnimationConfig _animationConfig;
        private JsonDataConfig _jsonDataConfig;
        private List<IAnimationCooldown> _animationCooldowns = new List<IAnimationCooldown>();
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        protected override CommandType CommandType => CommandType.Input;
        
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
            _animationCooldowns = GetAnimationCooldowns();
            
            Observable.EveryUpdate()
                .Throttle(TimeSpan.FromMilliseconds(gameSyncManager.TickRate))
                .Where(_ => !_isApplyingState)
                .Subscribe(_ => UpdateAnimationCooldowns(gameSyncManager.TickRate))
                .AddTo(_disposables);
        }
        
        public override CommandType HandledCommandType => CommandType.Input;
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
            for (var i = _animationCooldowns.Count - 1; i >= 0; i--)
            {
                if (i == snapshot.Count - 1)
                {
                    _animationCooldowns[i].Reset();
                    _animationCooldowns.Remove(_animationCooldowns[i]);
                    break;
                }
                var animationCooldown = _animationCooldowns[i];
                var snapshotCoolDown = snapshot[i];
                animationCooldown.Refresh(snapshotCoolDown);
            }
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
            if (header.CommandType == HandledCommandType && command is InputCommand inputCommand && IsInSpecialState?.Invoke() == false)
            {
                var info = _animationConfig.GetAnimationInfo(inputCommand.CommandAnimationState);
                var actionType = _animationConfig.GetActionType(inputCommand.CommandAnimationState);
                switch (actionType)
                {
                    case ActionType.Movement:
                        var cooldownInfo = _animationCooldowns.Find(x => x.AnimationState == inputCommand.CommandAnimationState);
                        if (cooldownInfo == null || !cooldownInfo.IsReady())
                        {
                            Debug.LogWarning($"Animation {inputCommand.CommandAnimationState} is on cooldown.");
                            return;
                        }
                        var currentStrength = _propertyPredictionState.GetProperty(PropertyTypeEnum.Strength);
                        if (currentStrength < info.cost)
                        {
                            Debug.LogWarning($"Not enough strength to perform {inputCommand.CommandAnimationState}.");
                            return;
                        }
                        _propertyPredictionState.AddPredictedCommand(new PropertyClientAnimationCommand
                        {
                            AnimationState = inputCommand.CommandAnimationState,
                            Header = header,
                        });
                        OnPlayerInputStateChanged?.Invoke(new PlayerInputStateData
                        {
                            InputAnimations = inputCommand.InputAnimationStates.ToList(),
                            Command = inputCommand.CommandAnimationState,
                            InputMovement = inputCommand.InputMovement
                        });
                        break;
                }
            }
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

        private void UpdateAnimationCooldowns(float deltaTime)
        {
            for (int i = _animationCooldowns.Count - 1; i >= 0; i--)
            {
                var animationCooldown = _animationCooldowns[i];
                animationCooldown.Update(deltaTime);
                if (animationCooldown.IsReady())
                {
                    _animationCooldowns.Remove(animationCooldown);
                }
            }
        }

        private void OnDestroy()
        {
            _disposables.Dispose();
        }
    }
}