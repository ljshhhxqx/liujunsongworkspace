﻿using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using MemoryPack;
using UniRx;
using UnityEngine;
using AnimationEvent = HotUpdate.Scripts.Config.ArrayConfig.AnimationEvent;
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
        float AnimationSpeed { get; }
        //动画类型
        AnimationState AnimationState { get; }
        //是否在冷却中
        bool IsReady();
        float SetAnimationSpeed(float speed);
        //更新冷却时间
        void Update(float deltaTime);
        //使用，进入冷却状态
        void Use();
        //使用快照数据来刷新冷却状态(快照是服务器传过来的，用于同步客户端)
        bool Refresh(CooldownSnapshotData snapshotData);
        //重置冷却状态
        void Reset();
        void SkillModifyCooldown(float modifier);
    }
    
    public class ComboCooldown : IAnimationCooldown
    {
        private float _currentCountdown;
        private int _currentStage;
        private int _maxStage;
        private List<float> _comboWindows;
        private float _configCooldown;
        private float _windowCountdown;
        private bool _inComboWindow;
        private AnimationState _state;
        public int MaxAttackCount => _maxStage;
        public float AttackWindow => _currentStage < _comboWindows.Count ? _comboWindows[_currentStage] : 0;

        public ComboCooldown(AnimationState state, List<float> comboWindow, float cooldown, float animationSpeed)
        {
            _state = state;
            _maxStage = comboWindow.Count;
            _comboWindows = comboWindow;
            AnimationSpeed = animationSpeed;
            _configCooldown = cooldown;
            Reset();
        }
        public AnimationState AnimationState => _state;
        public int CurrentStage => _currentStage;
        public float WindowRemaining => _windowCountdown;
        public float CurrentCountdown => _currentCountdown;
        public bool IsInComboWindow => _inComboWindow;

        public bool IsReady() => _currentCountdown <= 0 && 
                                 (_currentStage == 0 || _inComboWindow);


        public float AnimationSpeed { get; private set; }
        public float SetAnimationSpeed(float speed)
        {
            AnimationSpeed = Mathf.Max(0, speed);
            return AnimationSpeed;
        }

        public void Update(float deltaTime)
        {
            _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
            
            if (_inComboWindow)
            {
                _windowCountdown = Mathf.Max(0, _windowCountdown - deltaTime);
                if (_windowCountdown <= 0)
                    EndComboWindow();
            }
        }

        public void Use()
        {
            if (!IsReady()) return;

            if (_currentStage == 0)
            {
                StartNewCombo();
            }
            else if (_inComboWindow)
            {
                if (_currentStage == _maxStage)
                {
                    // 达到最大连击数时立即进入冷却
                    _currentCountdown = _configCooldown;
                    Reset();
                    return;
                }
                AdvanceCombo();
            }
        }

        private void StartNewCombo()
        {
            _currentStage = 1;
            _windowCountdown = _comboWindows[_currentStage - 1];
            _inComboWindow = true;
            _currentCountdown = 0;
        }

        private void AdvanceCombo()
        {
            _currentStage = Mathf.Min(_currentStage + 1, _maxStage);
            _windowCountdown = _comboWindows[_currentStage - 1];
            _inComboWindow = true;
        }

        private void EndComboWindow()
        {
            _inComboWindow = false;
            if (_currentStage > 0)
            {
                _currentCountdown = _configCooldown;
                Reset();
            }
        }

        public bool Refresh(CooldownSnapshotData snapshot)
        {
            if (!snapshot.AnimationState.Equals(_state))
                return false;

            _currentStage = snapshot.CurrentAttackStage;
            _windowCountdown = snapshot.WindowCountdown;
            _inComboWindow = snapshot.IsInComboWindow;
            _currentCountdown = snapshot.CurrentCountdown;
            return true;
        }

        public void Reset()
        {
            _currentStage = 0;
            _windowCountdown = 0;
            _inComboWindow = false;
        }

        public void SkillModifyCooldown(float modifier)
        {
            _configCooldown = modifier;
        }
    }
    
    public class KeyframeCooldown : IAnimationCooldown
    {
        private float _currentTime;
        private float _currentCountdown;
        private AnimationState _state;
        private List<KeyframeData> _timeline;
        private float _configCooldown;
        private float _windowCountdown;
        private HashSet<AnimationEvent> _triggeredEvents = new HashSet<AnimationEvent>();
        private Subject<AnimationEvent> _eventStream = new Subject<AnimationEvent>();

        public float CurrentTime => _currentTime;
        public float ResetWindow => _windowCountdown;
        public KeyframeCooldown(AnimationState state, float configCooldown, IEnumerable<KeyframeData> keyframes, float animationSpeed)
        {
            _state = state;
            _currentCountdown = 0;
            _configCooldown = configCooldown;
            AnimationSpeed = animationSpeed;
            _timeline = keyframes
                .OrderBy(k => k.triggerTime)
                .ToList();
        }
        public AnimationState AnimationState => _state;
        public IObservable<AnimationEvent> EventStream => _eventStream;
        public float CurrentCountdown => _currentCountdown;
        public float WindowRemaining => _windowCountdown;

        public bool IsReady()
        {
            if (_timeline.Count == 0 || _configCooldown == 0)
            {
                return true;
            }
            return _currentCountdown <= 0;
        }
        public float AnimationSpeed { get; private set; }
        public float SetAnimationSpeed(float speed)
        {
            AnimationSpeed = Mathf.Max(0, speed);
            return AnimationSpeed;
        }

        public void Update(float deltaTime)
        {
            if (_windowCountdown > 0)
            {
                _windowCountdown = Mathf.Max(0, _windowCountdown - deltaTime);
                if (_windowCountdown <= 0) 
                    Use();
                return;
            }

            _currentTime += deltaTime;

            foreach (var kf in _timeline)
            {
                if (_currentTime >= kf.triggerTime - kf.tolerance && 
                    _currentTime <= kf.triggerTime + kf.tolerance &&
                    !_triggeredEvents.Contains(kf.eventType))
                {
                    _triggeredEvents.Add(kf.eventType);
                    _eventStream.OnNext(kf.eventType);
                
                    if (kf.resetCooldown)
                    {
                        _windowCountdown = kf.resetCooldownWindowTime;
                    }
                }
            }
        }

        public void Use()
        {
            _currentCountdown = _configCooldown;
        }

        public bool Refresh(CooldownSnapshotData snapshot)
        {
            if (!snapshot.AnimationState.Equals(_state)) 
                return false;

            _currentCountdown = snapshot.CurrentCountdown;
            _windowCountdown = snapshot.ResetCooldownWindow;
            _currentTime = snapshot.KeyframeCurrentTime;
            return true;
        }

        public void Reset()
        {
            _currentCountdown = 0;
            _currentTime = 0;
        }

        public void SkillModifyCooldown(float modifier)
        {
            _configCooldown = modifier;
        }
    }
    
    public class KeyframeComboCooldown : IAnimationCooldown
    {
        private float _currentCountdown;
        private int _currentStage;
        private int _maxStage;
        private float _configCooldown;
        private float _currentTime;
        private float _windowCountdown;
        private bool _inComboWindow;
        private AnimationState _state;
        private Subject<AnimationEvent> _eventStream = new Subject<AnimationEvent>();
        private List<KeyframeData> _keyframe;
        public IObservable<AnimationEvent> EventStream => _eventStream;
        public int MaxAttackCount => _maxStage;

        public AnimationState AnimationState => _state;
        public int CurrentStage => _currentStage;
        public float WindowRemaining => _windowCountdown;
        public float CurrentCountdown => _currentCountdown;
        public bool IsInComboWindow => _inComboWindow;
        public float CurrentTime => _currentTime;
        public float AttackWindow => _currentStage == 0 ? 0 : _keyframe[_currentStage-1].resetCooldownWindowTime;

        public KeyframeComboCooldown(AnimationState state, float cooldown, List<KeyframeData> keyframe, float animationSpeed)
        {
            _state = state;
            _maxStage = keyframe.Count;
            _configCooldown = cooldown;
            _keyframe = keyframe;
            Reset();
            AnimationSpeed = animationSpeed;
        }
        public float AnimationSpeed { get; private set; }
        public float SetAnimationSpeed(float speed)
        {
            AnimationSpeed = Mathf.Max(0, speed);
            return AnimationSpeed;
        }

        public bool IsReady()
        {
            return _currentCountdown <= 0 && (_currentStage == 0 || _inComboWindow);
        }

        public void Update(float deltaTime)
        {
            if (_currentTime > 0)
            {
                // 全局冷却中
                _currentTime = Mathf.Max(0, _currentTime - deltaTime);
                return;
            }

            // 推进动画时间轴
            var animTime = GetAnimationTime();
            animTime += deltaTime;

            // 检测当前阶段关键帧
            var currentStageConfig = _currentStage < _keyframe.Count ? 
                _keyframe[_currentStage] : default;

            // 关键帧触发检测
            if (animTime >= currentStageConfig.triggerTime)
            {
                _eventStream.OnNext(currentStageConfig.eventType);
                _windowCountdown = currentStageConfig.resetCooldownWindowTime;
                _currentStage++;
            }

            // 连招窗口倒计时
            if (_windowCountdown > 0)
            {
                _windowCountdown = Mathf.Max(0, _windowCountdown - deltaTime);
                if (_windowCountdown <= 0)
                {
                    EndComboWindow();
                }
            }
        }

        public void Use()
        {
            if (!IsReady()) return;

            if (_currentStage == 0)
            {
                // 开始新连招
                StartNewCombo();
            }
            else if (_windowCountdown > 0)
            {
                // 在窗口期内推进连招
                AdvanceCombo();
            }
        }

        private void StartNewCombo()
        {
            _currentStage = 1;
            _windowCountdown = _keyframe[_currentStage-1].resetCooldownWindowTime;
            _inComboWindow = true;
            _currentCountdown = 0;
        }

        private void AdvanceCombo()
        {
            _currentStage = Mathf.Min(_currentStage + 1, _maxStage);
            _windowCountdown = _keyframe[_currentStage-1].resetCooldownWindowTime;
            _inComboWindow = true;
        }

        private void EndComboWindow()
        {
            _inComboWindow = false;
            if (_currentStage > 0)
            {
                _currentCountdown = _configCooldown;
                Reset();
            }
        }

        public bool Refresh(CooldownSnapshotData snapshot)
        {
            if (!snapshot.AnimationState.Equals(_state))
                return false;

            _currentStage = snapshot.CurrentAttackStage;
            _windowCountdown = snapshot.WindowCountdown;
            _inComboWindow = snapshot.IsInComboWindow;
            _currentCountdown = snapshot.CurrentCountdown;
            return true;
        }

        public void Reset()
        {
            _currentStage = 0;
            _windowCountdown = 0;
            _currentTime = 0;
            _inComboWindow = false;
        }
        
        private float GetAnimationTime()
        {
            if (_currentStage == 0) return 0;
            return _keyframe.Take(_currentStage - 1).Sum(s => s.triggerTime);
        }

        public void SkillModifyCooldown(float modifier)
        {
            _configCooldown = modifier;
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
        
        public AnimationCooldown(AnimationState animationState, float cooldown, float animationSpeed)
        {
            _animationState = animationState;
            _cooldown = cooldown;
            AnimationSpeed = animationSpeed;
        }
        
        public bool IsReady()
        {
            if (_cooldown == 0)
            {
                return true;
            }

            return _currentCountdown <= 0;
        }
        public float AnimationSpeed { get; private set; }
        public float SetAnimationSpeed(float speed)
        {
            AnimationSpeed = Mathf.Max(0, speed);
            return AnimationSpeed;
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

        public void SkillModifyCooldown(float modifier)
        {
            
        }
    }
    
    // 快照数据结构优化
    [MemoryPackable]
    public partial struct CooldownSnapshotData : IEquatable<CooldownSnapshotData>
    {
        // 通用字段
        [MemoryPackOrder(0)] public AnimationState AnimationState;
        [MemoryPackOrder(1)] public float Cooldown;
        [MemoryPackOrder(2)] public float CurrentCountdown;
        
        // 连招相关字段
        [MemoryPackOrder(3)] public int MaxAttackCount;
        [MemoryPackOrder(4)] public float AttackWindow;
        [MemoryPackOrder(5)] public int CurrentAttackStage;
        [MemoryPackOrder(6)] public bool IsInComboWindow;
        [MemoryPackOrder(7)] public float WindowCountdown;
        
        // 关键帧相关字段
        [MemoryPackOrder(8)] public float KeyframeCurrentTime;
        [MemoryPackOrder(9)] public float ResetCooldownWindow;
        [MemoryPackOrder(10)] public float AnimationSpeed;

        private const float EPSILON = 0.001f;

        public bool Equals(IAnimationCooldown other)
        {
            return other switch
            {
                ComboCooldown combo => CompareCombo(combo),
                KeyframeCooldown keyframe => CompareKeyframe(keyframe),
                KeyframeComboCooldown comboKeyframe => CompareComboKeyframe(comboKeyframe),
                _ => false
            };
        }

        private bool CompareCombo(ComboCooldown combo)
        {
            return AnimationState == combo.AnimationState &&
                   Mathf.Abs(CurrentCountdown - combo.CurrentCountdown) < EPSILON &&
                   CurrentAttackStage == combo.CurrentStage &&
                   Mathf.Abs(WindowCountdown - combo.WindowRemaining) < EPSILON &&
                   IsInComboWindow == combo.IsInComboWindow &&
                   Mathf.Approximately(AnimationSpeed, combo.AnimationSpeed);
        }

        private bool CompareKeyframe(KeyframeCooldown keyframe)
        {
            return AnimationState == keyframe.AnimationState &&
                   Mathf.Abs(CurrentCountdown - keyframe.CurrentCountdown) < EPSILON &&
                   Mathf.Abs(KeyframeCurrentTime - keyframe.CurrentTime) < EPSILON &&
                   Mathf.Abs(ResetCooldownWindow - keyframe.WindowRemaining) < EPSILON  &&
                   Mathf.Approximately(AnimationSpeed, keyframe.AnimationSpeed);
        }

        private bool CompareComboKeyframe(KeyframeComboCooldown comboKeyframe)
        {
            return AnimationState == comboKeyframe.AnimationState &&
                   Mathf.Abs(CurrentCountdown - comboKeyframe.CurrentCountdown) < EPSILON &&
                   CurrentAttackStage == comboKeyframe.CurrentStage &&
                   Mathf.Abs(WindowCountdown - comboKeyframe.WindowRemaining) < EPSILON &&
                   IsInComboWindow == comboKeyframe.IsInComboWindow &&
                   Mathf.Abs(KeyframeCurrentTime - comboKeyframe.CurrentTime) < EPSILON &&
                   Mathf.Approximately(AnimationSpeed, comboKeyframe.AnimationSpeed);
        }

        public static void CopyTo(IAnimationCooldown source, ref CooldownSnapshotData destination)
        {
            if (source is ComboCooldown combo)
            {
                
                destination.AnimationState = combo.AnimationState;
                destination.CurrentCountdown = combo.CurrentCountdown;
                destination.MaxAttackCount = combo.MaxAttackCount;
                destination.CurrentAttackStage = combo.CurrentStage;
                destination.AttackWindow = combo.AttackWindow;
                destination.IsInComboWindow = combo.IsInComboWindow;
                destination.WindowCountdown = combo.WindowRemaining;
                destination.AnimationSpeed = combo.AnimationSpeed;
            }
            else if (source is KeyframeCooldown keyframe)
            {
                destination.AnimationState = keyframe.AnimationState;
                destination.CurrentCountdown = keyframe.CurrentCountdown;
                destination.KeyframeCurrentTime = keyframe.CurrentTime;
                destination.ResetCooldownWindow = keyframe.ResetWindow;
                destination.AnimationSpeed = keyframe.AnimationSpeed;
            }
            else if (source is KeyframeComboCooldown comboKeyframe)
            {
                destination.AnimationState = comboKeyframe.AnimationState;
                destination.CurrentCountdown = comboKeyframe.CurrentCountdown;
                destination.MaxAttackCount = comboKeyframe.MaxAttackCount;
                destination.CurrentAttackStage = comboKeyframe.CurrentStage;
                destination.AttackWindow = comboKeyframe.AttackWindow;  
                destination.IsInComboWindow = comboKeyframe.IsInComboWindow;
                destination.WindowCountdown = comboKeyframe.WindowRemaining;
                destination.KeyframeCurrentTime = comboKeyframe.CurrentTime;
                destination.AnimationSpeed = comboKeyframe.AnimationSpeed;
            }
        }

        public static CooldownSnapshotData Create(IAnimationCooldown cooldown)
        {
            return cooldown switch
            {
                ComboCooldown combo => FromCombo(combo),
                KeyframeCooldown keyframe => FromKeyframe(keyframe),
                KeyframeComboCooldown comboKeyframe => FromComboKeyframe(comboKeyframe),
                AnimationCooldown animation => FromAnimation(animation),
                _ => throw new ArgumentException("Unsupported cooldown type")
            };
        }

        private static CooldownSnapshotData FromAnimation(AnimationCooldown animation)
        {
            return new CooldownSnapshotData
            {
                AnimationState = animation.AnimationState,
                CurrentCountdown = animation.CurrentCountdown,
                Cooldown = animation.Cooldown,
                AnimationSpeed = animation.AnimationSpeed,
            };
        }

        private static CooldownSnapshotData FromCombo(ComboCooldown combo)
        {
            return new CooldownSnapshotData
            {
                AnimationState = combo.AnimationState,
                CurrentCountdown = combo.CurrentCountdown,
                MaxAttackCount = combo.MaxAttackCount,
                CurrentAttackStage = combo.CurrentStage,
                AttackWindow = combo.AttackWindow,
                IsInComboWindow = combo.IsInComboWindow,
                WindowCountdown = combo.WindowRemaining,
                AnimationSpeed = combo.AnimationSpeed,
            };
        }

        private static CooldownSnapshotData FromKeyframe(KeyframeCooldown keyframe)
        {
            return new CooldownSnapshotData
            {
                AnimationState = keyframe.AnimationState,
                CurrentCountdown = keyframe.CurrentCountdown,
                KeyframeCurrentTime = keyframe.CurrentTime,
                ResetCooldownWindow = keyframe.ResetWindow,
                AnimationSpeed = keyframe.AnimationSpeed,
            };
        }

        private static CooldownSnapshotData FromComboKeyframe(KeyframeComboCooldown comboKeyframe)
        {
            return new CooldownSnapshotData
            {
                AnimationState = comboKeyframe.AnimationState,
                CurrentCountdown = comboKeyframe.CurrentCountdown,
                MaxAttackCount = comboKeyframe.MaxAttackCount,
                CurrentAttackStage = comboKeyframe.CurrentStage,
                AttackWindow = comboKeyframe.AttackWindow,
                IsInComboWindow = comboKeyframe.IsInComboWindow,
                WindowCountdown = comboKeyframe.WindowRemaining,
                KeyframeCurrentTime = comboKeyframe.CurrentTime,
                AnimationSpeed = comboKeyframe.AnimationSpeed,
            };
        }

        public bool Equals(CooldownSnapshotData other)
        {
            return AnimationState == other.AnimationState && Cooldown.Equals(other.Cooldown) && CurrentCountdown.Equals(other.CurrentCountdown) && MaxAttackCount == other.MaxAttackCount && AttackWindow.Equals(other.AttackWindow) && CurrentAttackStage == other.CurrentAttackStage && IsInComboWindow == other.IsInComboWindow && WindowCountdown.Equals(other.WindowCountdown) && KeyframeCurrentTime.Equals(other.KeyframeCurrentTime) && ResetCooldownWindow.Equals(other.ResetCooldownWindow) && AnimationSpeed.Equals(other.AnimationSpeed);
        }

        public override bool Equals(object obj)
        {
            return obj is CooldownSnapshotData other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add((int)AnimationState);
            hashCode.Add(Cooldown);
            hashCode.Add(CurrentCountdown);
            hashCode.Add(MaxAttackCount);
            hashCode.Add(AttackWindow);
            hashCode.Add(CurrentAttackStage);
            hashCode.Add(IsInComboWindow);
            hashCode.Add(WindowCountdown);
            hashCode.Add(KeyframeCurrentTime);
            hashCode.Add(ResetCooldownWindow);
            hashCode.Add(AnimationSpeed);
            return hashCode.ToHashCode();
        }
        
        public CooldownSnapshotData Reset(CooldownSnapshotData snapshotData)
        {
            snapshotData.CurrentCountdown = 0;
            snapshotData.KeyframeCurrentTime = 0;
            snapshotData.CurrentAttackStage = 0;
            snapshotData.IsInComboWindow = false;
            snapshotData.WindowCountdown = 0;
            snapshotData.ResetCooldownWindow = 0;
            return snapshotData;
        }
    }

    public static class AnimationCooldownExtensions
    {
        
    }
}