using System;
using MemoryPack;
using UniRx;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.Data
{
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
        bool IsReady();
        void Update(float deltaTime);
        void Use();
        bool Refresh(CooldownSnapshotData snapshotData);
        void Reset();
    }

    public class AttackCooldown : IAnimationCooldown
    {
        private float _currentCountdown;
        private int _currentAttackStage;
        private int _maxAttackCount;
        private float _attackMaxWindow;
        private float _configCooldown;
        private float _windowCountdown;
        private bool _isInComboWindow;
        private AnimationState _animationState;
        public int CurrentAttackStage => _currentAttackStage;
        public float CurrentCountdown => _currentCountdown;
        public float ConfigCooldown => _configCooldown;
        public int MaxAttackCount => _maxAttackCount; 
        public float AttackMaxWindow => _attackMaxWindow;
        public AnimationState AnimationState => _animationState;
        public bool IsInComboWindow => _isInComboWindow;
        public float WindowCountdown => _windowCountdown;
        private IDisposable _attackPointListener;
        private IDisposable _attackEndListener; 
        private IDisposable _comboResetListener;
        public Subject<int> AttackPointReached { get; }
        
        public AttackCooldown(
            AnimationState animationState,
            float configCooldown,
            int maxAttackCount,
            float attackMaxWindow,
            int currentAttackStage = 0,
            float windowCountdown = 0,
            bool isInComboWindow = false)
        {
            if (maxAttackCount < 1)
                throw new ArgumentException("MaxAttackCount must be at least 1");
            
            if (attackMaxWindow <= 0.3f)
                throw new ArgumentException("AttackWindow must be greater than 0.3");
            _configCooldown = configCooldown;
            _animationState = animationState;
            _maxAttackCount = maxAttackCount;
            _attackMaxWindow = attackMaxWindow;
            _currentAttackStage = currentAttackStage;
            _windowCountdown = windowCountdown;
            _isInComboWindow = isInComboWindow;
            _currentCountdown = 0;
            AttackPointReached = new Subject<int>();
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
                .Subscribe(OnResetState);
        }
        
        private void OnResetState(int stage)
        {
            ResetState();
        }

        private void OnAttackPointReached(int stage)
        {
            if (_currentAttackStage == 0 || _currentAttackStage != stage) return;
            AttackPointReached.OnNext(stage);
            // 获取当前阶段的有效窗口时间
            _windowCountdown = _attackMaxWindow;
            _isInComboWindow = true;
        }

        private void OnAttackEnded(int stage)
        {
            if (stage == CurrentAttackStage)
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
            return _currentCountdown <= 0 && 
                   (_currentAttackStage == 0 || _isInComboWindow);
        }

        public void Update(float deltaTime)
        {
            _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
            
            if (_isInComboWindow)
            {
                _windowCountdown = Mathf.Max(0, _windowCountdown - deltaTime);
                if (_windowCountdown <= 0)
                {
                    _isInComboWindow = false;
                    if (CurrentAttackStage > 0)
                    {
                        _currentCountdown = ConfigCooldown;
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
                _currentCountdown = 0;
                return;
            }

            if (_isInComboWindow)
            {
                _currentAttackStage++;
                if (_currentCountdown > _maxAttackCount)
                {
                    _currentCountdown = _configCooldown;
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

        public bool Refresh(CooldownSnapshotData snapshotData)
        {
            if (!snapshotData.Equals(this))
            {
                return false;
            }
            _currentCountdown = snapshotData.CurrentCountdown;
            _currentAttackStage = snapshotData.CurrentAttackStage;
            _windowCountdown = snapshotData.WindowCountdown;
            _isInComboWindow = snapshotData.IsInComboWindow;
            return true;
        }

        public void Reset()
        {
            ResetState();
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
            return $"Stage: {CurrentAttackStage} | Window: {_windowCountdown:F2} | Cooldown: {CurrentCountdown:F2}";
        }
    }

    public class AnimationCooldown : IAnimationCooldown
    {
        private float _cooldown;
        private AnimationState _animationState;
        private float _currentCountdown;

        public AnimationState AnimationState => _animationState;
        public float CurrentCountdown => _currentCountdown;
        public float Cooldown => _cooldown;
        
        public AnimationCooldown(AnimationState animationState, float cooldown, float currentCountdown = 0)
        {
            _animationState = animationState;
            _cooldown = cooldown;
            _currentCountdown = currentCountdown;
        }
        
        public bool IsReady()
        {
            if (_cooldown == 0)
            {
                return true;
            }

            return _currentCountdown <= 0;
        }
        
        public void Update(float deltaTime)
        {
            _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
        }
        
        public void Use()
        {
            if (!IsReady())
            {
                return;
            }
            _currentCountdown = Cooldown;
        }

        public bool Refresh(CooldownSnapshotData snapshotData)
        {
            if (!snapshotData.Equals(this))
            {
                return false;
            }
            _currentCountdown = snapshotData.CurrentCountdown;
            _cooldown = snapshotData.Cooldown;
            return true;
        }

        public void Reset()
        {
            _currentCountdown = 0;
        }
    }
    
    [MemoryPackable]
    public partial struct CooldownSnapshotData
    {
        [MemoryPackOrder(0)]
        public AnimationState AnimationState;
        [MemoryPackOrder(1)]
        public float Cooldown;
        [MemoryPackOrder(2)]
        public float CurrentCountdown;
        //for attack animation
        [MemoryPackOrder(3)]
        public float AttackWindow;
        [MemoryPackOrder(4)]
        public int MaxAttackCount;
        [MemoryPackOrder(5)]
        public bool IsInComboWindow;
        [MemoryPackOrder(6)]
        public float WindowCountdown;
        [MemoryPackOrder(7)]
        public int CurrentAttackStage;
        
        public bool Equals(IAnimationCooldown other)
        {
            if (other is AttackCooldown attackCooldown)
            {
                return AnimationState == attackCooldown.AnimationState &&
                       Mathf.Approximately(Cooldown, attackCooldown.ConfigCooldown) &&
                       Mathf.Approximately(CurrentCountdown, attackCooldown.CurrentCountdown) &&
                       Mathf.Approximately(AttackWindow, attackCooldown.AttackMaxWindow) &&
                       MaxAttackCount == attackCooldown.MaxAttackCount &&                   
                       IsInComboWindow == attackCooldown.IsInComboWindow &&
                       Mathf.Approximately(WindowCountdown, attackCooldown.WindowCountdown) &&
                       CurrentAttackStage == attackCooldown.CurrentAttackStage;
            }
            if (other is AnimationCooldown animationCooldown)
            {
                return AnimationState == animationCooldown.AnimationState &&
                       Mathf.Approximately(Cooldown, animationCooldown.Cooldown) &&
                       Mathf.Approximately(CurrentCountdown, animationCooldown.CurrentCountdown);
            }
            throw new ArgumentException("Invalid cooldown type");
        }
        
        public bool Equals(CooldownSnapshotData other)
        {
            return AnimationState == other.AnimationState &&
                   Mathf.Approximately(Cooldown, other.Cooldown) &&
                   Mathf.Approximately(CurrentCountdown, other.CurrentCountdown) &&
                   Mathf.Approximately(AttackWindow, other.AttackWindow) &&
                   MaxAttackCount == other.MaxAttackCount &&
                   IsInComboWindow == other.IsInComboWindow &&
                   Mathf.Approximately(WindowCountdown, other.WindowCountdown) &&
                   CurrentAttackStage == other.CurrentAttackStage;            
        }
    
        public static CooldownSnapshotData Create(IAnimationCooldown cooldown)
        {
            switch (cooldown)
            {
                case AttackCooldown attackCooldown:
                    return new CooldownSnapshotData
                    {
                        AnimationState = attackCooldown.AnimationState,
                        Cooldown = attackCooldown.ConfigCooldown,
                        CurrentCountdown = attackCooldown.CurrentCountdown,
                        AttackWindow = attackCooldown.AttackMaxWindow,
                        MaxAttackCount = attackCooldown.MaxAttackCount,
                        IsInComboWindow = attackCooldown.IsInComboWindow,
                        WindowCountdown = attackCooldown.WindowCountdown,
                        CurrentAttackStage = attackCooldown.CurrentAttackStage
                    };
                case AnimationCooldown animationCooldown:
                    return new CooldownSnapshotData
                    {
                        AnimationState = animationCooldown.AnimationState,
                        Cooldown = animationCooldown.Cooldown,
                        CurrentCountdown = animationCooldown.CurrentCountdown
                    };
                default:
                    throw new ArgumentException("Invalid cooldown type");
            }
        }
    }
}