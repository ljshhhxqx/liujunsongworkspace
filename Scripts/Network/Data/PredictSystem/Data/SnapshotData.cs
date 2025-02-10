using System;
using MemoryPack;
using UniRx;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.Data
{
    public interface IAttackAnimationEvent
    {
        // 当动画到达判定点时触发
        IObservable<int> AttackPointReached { get; } 
        // 当动画判定结束时触发
        IObservable<int> AttackEnded { get; }
    }
    
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(AttackCooldown))]
    [MemoryPackUnion(1, typeof(AnimationCooldown))]
    public partial interface IAnimationCooldown
    {
        AnimationState AnimationState { get; }
        bool IsReady();
        void Update(float deltaTime);
        void Use();
        bool IsEqual(IAnimationCooldown other);
    }

    [MemoryPackable]
    public partial class AttackCooldown : IAnimationCooldown
    {
        [MemoryPackOrder(0)]
        private float _currentCountdown;
        [MemoryPackOrder(1)]
        private int _currentAttackStage;
        [MemoryPackOrder(2)]
        private int _maxAttackCount;
        [MemoryPackOrder(3)]
        private float _attackWindow;
        [MemoryPackOrder(4)]
        private float _cooldown;
        [MemoryPackOrder(5)]
        private float _windowCountdown;
        [MemoryPackOrder(6)]
        private bool _isInComboWindow;
        [MemoryPackOrder(7)]
        private AnimationState _animationState;
        public int CurrentAttackStage => _currentAttackStage;
        public float CurrentCountdown => _currentCountdown;
        public float Cooldown => _cooldown;
        public int MaxAttackCount => _maxAttackCount; 
        public float AttackWindow => _attackWindow;
        public AnimationState AnimationState => _animationState;
        public bool IsInComboWindow => _isInComboWindow;
        public float WindowCountdown => _windowCountdown;
        private IDisposable _attackPointListener;
        private IDisposable _attackEndListener; 
        private IDisposable _comboResetListener;
        
        public AttackCooldown(
            AnimationState animationState,
            float cooldown,
            int maxAttackCount,
            float attackWindow,
            int currentAttackStage = 0,
            float windowCountdown = 0,
            bool isInComboWindow = false)
        {
            if (maxAttackCount < 1)
                throw new ArgumentException("MaxAttackCount must be at least 1");
            
            if (attackWindow <= 0.3f)
                throw new ArgumentException("AttackWindow must be greater than 0.3");
            _cooldown = cooldown;
            _animationState = animationState;
            _maxAttackCount = maxAttackCount;
            _attackWindow = attackWindow;
            _currentAttackStage = currentAttackStage;
            _windowCountdown = windowCountdown;
            _isInComboWindow = isInComboWindow;
            _currentCountdown = 0;
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

            // 获取当前阶段的有效窗口时间
            _windowCountdown = AttackWindow;
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
                        _currentCountdown = Cooldown;
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
                    _currentCountdown = _cooldown;
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

        public bool IsEqual(IAnimationCooldown other)
        {
            if (other is AttackCooldown attackCooldown)
            {
                return AnimationState == attackCooldown.AnimationState &&
                       Mathf.Approximately(CurrentCountdown, attackCooldown.CurrentCountdown) &&
                       MaxAttackCount == attackCooldown.MaxAttackCount &&
                       Mathf.Approximately(AttackWindow, attackCooldown.AttackWindow) &&
                       CurrentAttackStage == attackCooldown.CurrentAttackStage &&
                       IsInComboWindow == attackCooldown.IsInComboWindow &&
                       Mathf.Approximately(WindowCountdown, attackCooldown.WindowCountdown);
            }
            return false;
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

    [MemoryPackable]
    public partial class AnimationCooldown : IAnimationCooldown
    {
        [MemoryPackOrder(0)]
        private float _cooldown;
        [MemoryPackOrder(1)]
        private AnimationState _animationState;
        [MemoryPackOrder(2)]
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

        public bool IsEqual(IAnimationCooldown other)
        {
            if (other is AnimationCooldown animationCooldown)
            {
                return AnimationState == animationCooldown.AnimationState &&    
                       Mathf.Approximately(Cooldown, animationCooldown.Cooldown) &&
                       Mathf.Approximately(CurrentCountdown, animationCooldown.CurrentCountdown);
            }
            return false;
        }
    }
    
    // [MemoryPackable]
    // public partial struct CooldownSnapshotData
    // {
    //     [MemoryPackOrder(0)]
    //     public AnimationState AnimationState;
    //     [MemoryPackOrder(1)]
    //     public float Cooldown;
    //     [MemoryPackOrder(2)]
    //     public float CurrentCountdown;
    //     //for attack animation
    //     [MemoryPackOrder(3)]
    //     public float AttackWindow;
    //     [MemoryPackOrder(4)]
    //     public int MaxAttackCount;
    //     [MemoryPackOrder(5)]
    //     public bool IsInComboWindow;
    //     [MemoryPackOrder(6)]
    //     public float WindowCountdown;
    //     [MemoryPackOrder(7)]
    //     public int CurrentAttackStage;
    //     
    //     public bool Equals(IAnimationCooldown other)
    //     {
    //         if (other is AttackCooldown attackCooldown)
    //         {
    //             return AnimationState == attackCooldown.AnimationState &&
    //                    Mathf.Approximately(Cooldown, attackCooldown.Cooldown) &&
    //                    Mathf.Approximately(CurrentCountdown, attackCooldown.CurrentCountdown) &&
    //                    Mathf.Approximately(AttackWindow, attackCooldown.AttackWindow) &&
    //                    MaxAttackCount == attackCooldown.MaxAttackCount &&                   
    //                    IsInComboWindow == attackCooldown.IsInComboWindow &&
    //                    Mathf.Approximately(WindowCountdown, attackCooldown.WindowCountdown) &&
    //                    CurrentAttackStage == attackCooldown.CurrentAttackStage;
    //         }
    //         if (other is AnimationCooldown animationCooldown)
    //         {
    //             return AnimationState == animationCooldown.AnimationState &&
    //                    Mathf.Approximately(Cooldown, animationCooldown.Cooldown) &&
    //                    Mathf.Approximately(CurrentCountdown, animationCooldown.CurrentCountdown);
    //         }
    //         throw new ArgumentException("Invalid cooldown type");
    //     }
    //
    //     public static CooldownSnapshotData Create(IAnimationCooldown cooldown)
    //     {
    //         switch (cooldown)
    //         {
    //             case AttackCooldown attackCooldown:
    //                 return new CooldownSnapshotData
    //                 {
    //                     AnimationState = attackCooldown.AnimationState,
    //                     Cooldown = attackCooldown.Cooldown,
    //                     CurrentCountdown = attackCooldown.CurrentCountdown,
    //                     AttackWindow = attackCooldown.AttackWindow,
    //                     MaxAttackCount = attackCooldown.MaxAttackCount,
    //                     IsInComboWindow = attackCooldown.IsInComboWindow,
    //                     WindowCountdown = attackCooldown.WindowCountdown,
    //                     CurrentAttackStage = attackCooldown.CurrentAttackStage
    //                 };
    //             case AnimationCooldown animationCooldown:
    //                 return new CooldownSnapshotData
    //                 {
    //                     AnimationState = animationCooldown.AnimationState,
    //                     Cooldown = animationCooldown.Cooldown,
    //                     CurrentCountdown = animationCooldown.CurrentCountdown
    //                 };
    //             default:
    //                 throw new ArgumentException("Invalid cooldown type");
    //         }
    //     }
    //
    //     public IAnimationCooldown CreateCooldown()
    //     {
    //         switch (AnimationState)
    //         {
    //             case AnimationState.Attack:
    //                 return new AttackCooldown(
    //                     AnimationState,
    //                     Cooldown,
    //                     MaxAttackCount,
    //                     AttackWindow,
    //                     CurrentAttackStage,
    //                     WindowCountdown,
    //                     IsInComboWindow);
    //             default:
    //                 return new AnimationCooldown(AnimationState, Cooldown, CurrentCountdown);
    //         }
    //     }
    // }
}