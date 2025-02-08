using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using Mirror;
using Newtonsoft.Json;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

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
                for (int i = animationCooldowns.Count - 1; i >= 0; i--)
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
            _animationCooldowns.Add(connectionId, GetAnimationCooldowns());
            BindAniEvents(connectionId, player.GetComponent<IAttackAnimationEvent>());
        }

        private void BindAniEvents(int connectionId, IAttackAnimationEvent animationEvent)
        {
            var animationCooldowns = _animationCooldowns[connectionId];
            if (animationCooldowns.Find(x => x.AnimationState == AnimationState.Attack) is not AttackCooldown attackCooldown)
            {
                Debug.LogError("AttackCooldown not found in animation cooldowns.");
                return;
            }
            attackCooldown.BindAnimationEvents(animationEvent);
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
                    list.Add(new AttackCooldown(info.cooldown, _jsonDataConfig.PlayerConfig.AttackComboMaxCount, _jsonDataConfig.PlayerConfig.AttackComboWindow));
                    continue;
                }

                if (info.cooldown > 0)
                {
                    list.Add(new AnimationCooldown(animationState, info.cooldown));
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
                var playerController = GameSyncManager.GetPlayerConnection(header.connectionId);
                var playerProperty = playerSyncSystem.GetPlayerProperty(header.connectionId);
                //验证玩家是否存在或者是否已死亡
                if (playerProperty == null || playerProperty[PropertyTypeEnum.Health].CurrentValue <= 0)
                {
                    return null;
                }

                var inputStateData = new PlayerInputStateData
                {
                    inputMovement = inputCommand.inputMovement,
                    inputAnimations = inputCommand.inputAnimationStates.ToList()
                };
                
                //获取可以执行的动画
                var commandAnimation = playerController.GetCurrentAnimationState(inputStateData);
                if (!_animationCooldowns.TryGetValue(header.connectionId, out var animationCooldowns))
                {
                    return null;
                }
                var info = _animationConfig.GetAnimationInfo(commandAnimation);
                
                //验证冷却时间是否已到
                var cooldownInfo = animationCooldowns.Find(x => x.AnimationState == commandAnimation);
                if (info.cooldown != 0)
                {
                    if (cooldownInfo == null || !cooldownInfo.IsReady())
                    {
                        Debug.LogWarning($"Player {header.connectionId} input animation {commandAnimation} is not ready.");
                        return null;
                    }
                }
                
                //验证是否耐力值足够
                if (playerProperty[PropertyTypeEnum.Strength].CurrentValue < info.cost)
                {
                    Debug.LogWarning($"Player {header.connectionId} input animation {commandAnimation} cost {info.cost} strength, but strength is {playerProperty[PropertyTypeEnum.Strength].CurrentValue}.");
                    return null;
                }
                

                cooldownInfo.Use();

                playerController.HandleMoveAndAnimation(inputStateData);
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

        public override void Clear()
        {
            _disposables.Dispose();
        }
    }
    
    public interface IAttackAnimationEvent
    {
        // 当动画到达判定点时触发
        IObservable<int> AttackPointReached { get; } 
        // 当动画判定结束时触发
        IObservable<int> AttackEnded { get; }
    }
    
    public interface IAnimationCooldown
    {
        AnimationState AnimationState { get; }
        float CurrentCountdown { get; }
        float Cooldown { get; }
        bool IsReady();
        void Update(float deltaTime);
        void Use();
    }

    public class AttackCooldown : IAnimationCooldown
    {
        // 基础属性
        public AnimationState AnimationState => AnimationState.Attack;
        public float CurrentCountdown { get; private set; }
        public float Cooldown { get; }
        public int MaxAttackCount { get; }
        public float AttackWindow { get; }

        // 连击状态
        private int _currentAttackStage;
        private float _windowCountdown;
        private bool _isInComboWindow;
        
        // 动画事件监听
        private IDisposable _attackPointListener;
        private IDisposable _attackEndListener;
        private IDisposable _comboResetListener;

        public AttackCooldown(float cooldown, int maxAttackCount, float attackWindow)
        {
            ValidateParameters(maxAttackCount, attackWindow);
            
            Cooldown = cooldown;
            MaxAttackCount = maxAttackCount;
            AttackWindow = attackWindow;
            ResetState();
        }

        private void ValidateParameters(int maxAttackCount, float attackWindow)
        {
            if (maxAttackCount < 1)
                throw new ArgumentException("MaxAttackCount must be at least 1");
            
            if (attackWindow <= 0.3f)
                throw new ArgumentException("AttackWindow must be greater than 0.3");
        }

        public void BindAnimationEvents(IAttackAnimationEvent animationEvent)
        {
            UnbindEvents();
            
            _attackPointListener = animationEvent.AttackPointReached
                .Where(stage => stage == _currentAttackStage)
                .Subscribe(OnAttackPointReached);

            _attackEndListener = animationEvent.AttackEnded
                .Subscribe(OnAttackEnded);

            _comboResetListener = animationEvent.AttackPointReached
                .Where(stage => stage == 0)
                .Subscribe(_ => ResetState());
        }

        private void OnAttackPointReached(int stage)
        {
            if (_currentAttackStage == 0) return;

            // 获取当前阶段的有效窗口时间
            _windowCountdown = AttackWindow;
            _isInComboWindow = true;
        }

        private void OnAttackEnded(int stage)
        {
            if (stage == _currentAttackStage)
            {
                // 结束当前阶段时未触发连击则重置
                if (!_isInComboWindow)
                {
                    ResetState();
                }
            }
        }

        public bool IsReady()
        {
            return CurrentCountdown <= 0 && 
                   (_currentAttackStage == 0 || _isInComboWindow);
        }

        public void Update(float deltaTime)
        {
            CurrentCountdown = Mathf.Max(0, CurrentCountdown - deltaTime);
            
            if (_isInComboWindow)
            {
                _windowCountdown = Mathf.Max(0, _windowCountdown - deltaTime);
                if (_windowCountdown <= 0)
                {
                    _isInComboWindow = false;
                    if (_currentAttackStage > 0)
                    {
                        CurrentCountdown = Cooldown;
                        ResetState();
                    }
                }
            }
        }

        public void Use()
        {
            if (!IsReady()) return;

            if (_currentAttackStage == 0)
            {
                // 开始新连击
                _currentAttackStage = 1;
                CurrentCountdown = 0;
                return;
            }

            if (_isInComboWindow)
            {
                _currentAttackStage++;
                if (_currentAttackStage > MaxAttackCount)
                {
                    CurrentCountdown = Cooldown;
                    ResetState();
                }
                else
                {
                    // 重置窗口状态等待动画事件
                    _isInComboWindow = false;
                    _windowCountdown = 0;
                }
            }
        }

        private void ResetState()
        {
            _currentAttackStage = 0;
            _windowCountdown = 0;
            _isInComboWindow = false;
        }

        public void UnbindEvents()
        {
            _attackPointListener?.Dispose();
            _attackEndListener?.Dispose();
            _comboResetListener?.Dispose();
        }

        // 调试信息
        public string GetDebugInfo()
        {
            return $"Stage: {_currentAttackStage} | Window: {_windowCountdown:F2} | Cooldown: {CurrentCountdown:F2}";
        }
    }

    public class AnimationCooldown : IAnimationCooldown
    {
        public AnimationState AnimationState { get; private set; }
        public float CurrentCountdown { get; private set; }
        public float Cooldown { get; private set; }

        public AnimationCooldown(AnimationState animationState, float cooldown)
        {
            AnimationState = animationState;
            CurrentCountdown = 0;
            Cooldown = cooldown;
        }
        
        public bool IsReady()
        {
            if (Cooldown == 0)
            {
                return true;
            }

            return CurrentCountdown <= 0;
        }
        
        public void Update(float deltaTime)
        {
            CurrentCountdown = Math.Max(0, CurrentCountdown - deltaTime);
        }
        
        public void Use()
        {
            if (!IsReady())
            {
                return;
            }
            CurrentCountdown = Cooldown;
        }
    }
}