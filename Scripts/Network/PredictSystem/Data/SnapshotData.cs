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
        private List<float> _configCooldowns;
        private float _windowCountdown;
        private bool _inComboWindow;
        private AnimationState _state;

        public ComboCooldown(AnimationState state, List<float> comboWindows, List<float> cooldowns)
        {
            _state = state;
            _maxStage = cooldowns.Count;
            _comboWindows = comboWindows;
            _configCooldowns = cooldowns;
            Reset();
        }

        public AnimationState AnimationState => _state;
        public int CurrentStage => _currentStage;
        public float WindowRemaining => _windowCountdown;

        public bool IsReady() => _currentCountdown <= 0 && 
            (_currentStage == 0 || _inComboWindow);

        public void Update(float deltaTime)
        {
            _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
            
            if (_inComboWindow)
            {
                _windowCountdown = Mathf.Max(0, _windowCountdown - deltaTime);
                if (_windowCountdown <= 0) EndComboWindow();
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
                _currentCountdown = _configCooldowns[_currentStage - 1];
                Reset();
            }
        }

        public bool Refresh(CooldownSnapshotData snapshot)
        {
            if (!snapshot.AnimationState.Equals(_state))
            {
                return false;
            }
            _currentStage = snapshot.CurrentAttackStage;
            _windowCountdown = snapshot.WindowCountdown;
            _inComboWindow = snapshot.IsInComboWindow;
            _currentCountdown = snapshot.Cooldown;
            return true;
        }

        public void Reset()
        {
            _currentStage = 0;
            _windowCountdown = 0;
            _inComboWindow = false;
        }
    }
    
    // 纯关键帧系统
    public class KeyframeCooldown : IAnimationCooldown
    {
        private float _currentTime;
        private float _currentCountdown;
        private AnimationState _state;
        private List<KeyframeData> _timeline;
        private float _configCooldown;
        private HashSet<string> _triggeredEvents = new HashSet<string>();
        private Subject<string> _eventStream = new Subject<string>();

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
            if (_currentCountdown > 0)
            {
                _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
                return;
            }

            _currentTime += deltaTime;

            foreach (var kf in _timeline)
            {
                if (_currentTime >= kf.triggerTime && 
                    !_triggeredEvents.Contains(kf.eventType))
                {
                    _triggeredEvents.Add(kf.eventType);
                    _eventStream.OnNext(kf.eventType);
                
                    if (kf.resetCooldown)
                    {
                        _currentCountdown = kf.customCooldown;
                    }
                }
            }
        }

        public void Use()
        {
            _currentCountdown = _configCooldown;
        }

        public bool Refresh(CooldownSnapshotData snapshotData)
        {
            if (!snapshotData.AnimationState.Equals(_state))
            {
                return false;
            }
            _currentCountdown = snapshotData.CurrentCountdown;
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
        private ComboCooldown _combo;
        private KeyframeCooldown _keyframes;
        private IDisposable _eventListener;

        public KeyframeComboCooldown(
            AnimationState state,
            float configCooldown,
            IEnumerable<KeyframeData> keyframes,
            List<float> comboWindows, 
            List<float> cooldowns)
        {
            _combo = new ComboCooldown(
                state,
                comboWindows,
                cooldowns);

            _keyframes = new KeyframeCooldown(state, configCooldown, keyframes);

            _eventListener = _keyframes.EventStream
                .Subscribe(OnKeyframeEvent);
        }

        private void OnKeyframeEvent(string eventType)
        {
            if (eventType == "ComboAdvance" && _combo.IsReady())
            {
                _combo.Use();
            }
        }

        public AnimationState AnimationState => _combo.AnimationState;

        public bool IsReady() => _combo.IsReady();

        public void Update(float deltaTime)
        {
            _combo.Update(deltaTime);
            _keyframes.Update(deltaTime);
        }

        public void Use()
        {
            
        }

        public bool Refresh(CooldownSnapshotData snapshotData)
        {
            return _combo.Refresh(snapshotData) && _keyframes.Refresh(snapshotData);
        }

        public void Reset()
        {
            
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
            //todo: 补全keyframeComboCooldown、comboCooldown、keyframeCooldown的snapshot数据
            if (other is KeyframeComboCooldown keyframeComboCooldown)
            {
                return AnimationState == keyframeComboCooldown.AnimationState;
            }
            if (other is ComboCooldown comboCooldown)
            {
                return AnimationState == comboCooldown.AnimationState;
            }
            if (other is KeyframeCooldown keyframeCooldown)
            {
                return AnimationState == keyframeCooldown.AnimationState;
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
                //todo: 补全keyframeComboCooldown、comboCooldown、keyframeCooldown的snapshot数据
                case KeyframeComboCooldown keyframeComboCooldown:
                    return new CooldownSnapshotData
                    {
                        AnimationState = keyframeComboCooldown.AnimationState,
                    };
                case ComboCooldown comboCooldown:
                    return new CooldownSnapshotData
                    {
                        AnimationState = comboCooldown.AnimationState,
                    };
                case KeyframeCooldown keyframeCooldown:
                    return new CooldownSnapshotData
                    {
                        AnimationState = keyframeCooldown.AnimationState,
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