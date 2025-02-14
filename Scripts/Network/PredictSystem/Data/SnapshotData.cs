using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
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
        //动画类型
        AnimationState AnimationState { get; }
        //是否在冷却中
        bool IsReady();
        //更新冷却时间
        void Update(float deltaTime);
        //使用，进入冷却状态
        void Use();
        //使用快照数据来刷新冷却状态(快照是服务器传过来的，用于同步客户端)
        bool Refresh(CooldownSnapshotData snapshotData);
        //重置冷却状态
        void Reset();
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

        public ComboCooldown(AnimationState state, List<float> comboWindow, float cooldown)
        {
            _state = state;
            _maxStage = comboWindow.Count;
            _comboWindows = comboWindow;
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
    }
    
    public class KeyframeCooldown : IAnimationCooldown
    {
        private float _currentTime;
        private float _currentCountdown;
        private AnimationState _state;
        private List<KeyframeData> _timeline;
        private float _configCooldown;
        private float _windowCountdown;
        private HashSet<string> _triggeredEvents = new HashSet<string>();
        private Subject<string> _eventStream = new Subject<string>();

        public float CurrentTime => _currentTime;
        public float ResetWindow => _windowCountdown;
        public KeyframeCooldown(AnimationState state, float configCooldown, IEnumerable<KeyframeData> keyframes)
        {
            _state = state;
            _currentCountdown = 0;
            _configCooldown = configCooldown;
            _timeline = keyframes
                .OrderBy(k => k.triggerTime)
                .ToList();
        }

        public AnimationState AnimationState => _state;
        public IObservable<string> EventStream => _eventStream;
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
        private Subject<string> _eventStream = new Subject<string>();
        private List<KeyframeData> _keyframe;
        public IObservable<string> EventStream => _eventStream;
        public int MaxAttackCount => _maxStage;

        public AnimationState AnimationState => _state;
        public int CurrentStage => _currentStage;
        public float WindowRemaining => _windowCountdown;
        public float CurrentCountdown => _currentCountdown;
        public bool IsInComboWindow => _inComboWindow;
        public float CurrentTime => _currentTime;
        public float AttackWindow => _keyframe[_currentStage-1].resetCooldownWindowTime;

        public KeyframeComboCooldown(AnimationState state, float cooldown, List<KeyframeData> keyframe)
        {
            _state = state;
            _maxStage = keyframe.Count;
            _configCooldown = cooldown;
            _keyframe = keyframe;
            Reset();
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
    
    // 快照数据结构优化
    [MemoryPackable]
    public partial struct CooldownSnapshotData
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
                   Math.Abs(CurrentCountdown - combo.CurrentCountdown) < EPSILON &&
                   CurrentAttackStage == combo.CurrentStage &&
                   Math.Abs(WindowCountdown - combo.WindowRemaining) < EPSILON &&
                   IsInComboWindow == combo.IsInComboWindow;
        }

        private bool CompareKeyframe(KeyframeCooldown keyframe)
        {
            return AnimationState == keyframe.AnimationState &&
                   Math.Abs(CurrentCountdown - keyframe.CurrentCountdown) < EPSILON &&
                   Math.Abs(KeyframeCurrentTime - keyframe.CurrentTime) < EPSILON &&
                   Math.Abs(ResetCooldownWindow - keyframe.WindowRemaining) < EPSILON;
        }

        private bool CompareComboKeyframe(KeyframeComboCooldown comboKeyframe)
        {
            return AnimationState == comboKeyframe.AnimationState &&
                   Math.Abs(CurrentCountdown - comboKeyframe.CurrentCountdown) < EPSILON &&
                   CurrentAttackStage == comboKeyframe.CurrentStage &&
                   Math.Abs(WindowCountdown - comboKeyframe.WindowRemaining) < EPSILON &&
                   IsInComboWindow == comboKeyframe.IsInComboWindow &&
                   Math.Abs(KeyframeCurrentTime - comboKeyframe.CurrentTime) < EPSILON;
        }

        public static CooldownSnapshotData Create(IAnimationCooldown cooldown)
        {
            return cooldown switch
            {
                ComboCooldown combo => FromCombo(combo),
                KeyframeCooldown keyframe => FromKeyframe(keyframe),
                KeyframeComboCooldown comboKeyframe => FromComboKeyframe(comboKeyframe),
                _ => throw new ArgumentException("Unsupported cooldown type")
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
                WindowCountdown = combo.WindowRemaining
            };
        }

        private static CooldownSnapshotData FromKeyframe(KeyframeCooldown keyframe)
        {
            return new CooldownSnapshotData
            {
                AnimationState = keyframe.AnimationState,
                CurrentCountdown = keyframe.CurrentCountdown,
                KeyframeCurrentTime = keyframe.CurrentTime,
                ResetCooldownWindow = keyframe.ResetWindow
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
            };
        }
    }
}