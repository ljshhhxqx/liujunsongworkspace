using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AOTScripts.Data;
using HotUpdate.Scripts.Tool.ObjectPool;
using MemoryPack;
using UniRx;
using UnityEngine;
using AnimationEvent = AOTScripts.Data.AnimationEvent;
using AnimationState = AOTScripts.Data.AnimationState;

namespace HotUpdate.Scripts.Network.State
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

            _currentStage = snapshot.CurrentStage;
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
        //迭代关键帧序列
        private float _currentTime;
        //迭代冷却时间
        private float _currentCountdown;
        private AnimationState _state;
        private List<KeyframeData> _timeline;
        private float _configCooldown;
        private readonly Subject<AnimationEvent> _eventStream = new Subject<AnimationEvent>();
        private int _currentStage;

        public float CurrentTime => _currentTime;
        public int CurrentStage => _currentStage;
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

        public bool IsReady()
        {
            if (_timeline.Count == 0 && _configCooldown <= 0)
            {
                Debug.Log($"[IsReady] [Keyframe] _timeline.Count == 0 && _configCooldown <= 0  _currentCountdown-{_currentCountdown} _configCooldown-{_configCooldown}");
                return true;
            }
            //Debug.Log($"[IsReady] [Keyframe] _currentCountdown-{_currentCountdown} _configCooldown-{_configCooldown}");
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
            if (_currentCountdown <= 0)
            {
                //Reset();
                return;
            }
            _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
            if (_currentStage >= _timeline.Count)
            {
                _currentTime = 0;
                return;
            }


            _currentTime += deltaTime;

            for (int i = 0; i < _timeline.Count; i++)
            {
                var kf = _timeline[i];
                if (_currentTime >= kf.triggerTime - kf.tolerance && 
                    _currentTime <= kf.triggerTime + kf.tolerance)
                {
                    _eventStream.OnNext(kf.eventType);
                    _currentStage++;
                }
            }
            //Debug.Log($"[Update] [Animation] Animation-{_state}  _currentCountdown-{_currentCountdown} _currentTime-{_currentTime}  _currentStage-{_currentStage}");
        }

        public void Use()
        {
            if (!IsReady()) return;
            _currentCountdown = _configCooldown;
            _currentStage = 0;
            Debug.Log($"[Use] [keyframe] Animation-{_state}  _currentStage-{_currentStage} _currentCountdown-{_currentCountdown}");
        }

        public bool Refresh(CooldownSnapshotData snapshot)
        {
            if (!snapshot.AnimationState.Equals(_state)) 
                return false;

            _currentCountdown = snapshot.CurrentCountdown;
            _currentTime = snapshot.KeyframeCurrentTime;
            _currentStage = snapshot.CurrentStage;
            //Debug.Log($"[Refresh] [Keyframe] Animation-{_state}  _currentStage-{_currentStage} _currentCountdown-{_currentCountdown}");
            return true;
        }

        public void Reset()
        {
            _currentCountdown = 0;
            _currentTime = 0;
            _currentStage = 0;
        }

        public void SkillModifyCooldown(float modifier)
        {
            _configCooldown = modifier;
        }
    }
    
    /// <summary>
    /// 在关键帧连招动画里面，每一段动画都是独立的时间窗口、独立的关键帧计算，只要有一个动画时间的窗口结束，则全部进入冷却
    /// </summary>
    public class KeyframeComboCooldown : IAnimationCooldown
    {
        private float _currentCountdown;
        private int _currentStage;
        private int _maxStage;
        private float _configCooldown;
        private float _currentTime;
        private bool _isComboStart;
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
        public bool IsComboStart => _isComboStart;
        public float AttackWindow => _currentStage == 0 ? 0 : _currentStage >= _keyframe.Count ? _keyframe.Count-1 : _keyframe[_currentStage - 1].resetCooldownWindowTime;

        public KeyframeComboCooldown(AnimationState state, float cooldown, List<KeyframeData> keyframe, float animationSpeed)
        {
            _state = state;
            _maxStage = keyframe.Count;
            _configCooldown = cooldown;
            _keyframe = keyframe;
            Reset();
            AnimationSpeed = animationSpeed;
            _inComboWindow = true;
        }
        public float AnimationSpeed { get; private set; }
        public float SetAnimationSpeed(float speed)
        {
            AnimationSpeed = Mathf.Max(0, speed);
            return AnimationSpeed;
        }

        public bool IsReady()
        {
            if (_currentStage <= 0)
            {
                //Debug.Log($"[IsReady] [keyFrameCombo] _currentStage <= 0  _currentStage-{_currentStage} _inComboWindow-{_inComboWindow}");
                return _currentCountdown <= 0 && _inComboWindow;
            }
            
            //Debug.Log($"[IsReady] [keyFrameCombo] _currentStage > 0  _windowCountdown-{_windowCountdown} _currentStage-{_currentStage} _inComboWindow-{_inComboWindow}");
            return _windowCountdown > 0 && _inComboWindow && _currentStage < _keyframe.Count;
        }

        public void Update(float deltaTime)
        {
            if (_currentCountdown > 0)
            {
                _inComboWindow = false;
                _isComboStart = false;
                _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
                if (_currentCountdown <= 0)
                {
                    _inComboWindow = true;
                    Reset();
                }
                return;
            }
            
            if (!_isComboStart)
            {
                return;
            }
            
            // 连招窗口倒计时
            if (_windowCountdown > 0 && _currentStage < _keyframe.Count)
            {
                _windowCountdown = Mathf.Max(0, _windowCountdown - deltaTime);
                //Debug.Log($"[Update] [KeyframeCombo] 连招窗口倒计时 Animation-{_state}  _currentStage-{_currentStage} _windowCountdown-{_windowCountdown} _currentTime-{_currentTime}");
                if (_windowCountdown <= 0)
                {
                    EndComboWindow();
                }
                return;
            }

            // 检测当前阶段关键帧
            var currentStageConfig = _currentStage < _keyframe.Count ? 
                _keyframe[_currentStage] : default;
            if (currentStageConfig.resetCooldownWindowTime == 0)
            {
                _isComboStart = false;
                _inComboWindow = false;
                _currentCountdown = _configCooldown;
                return;
            }

            // 关键帧触发检测
            if (_currentTime >= currentStageConfig.triggerTime - currentStageConfig.tolerance && 
                _currentTime <= currentStageConfig.triggerTime + currentStageConfig.tolerance)
            {
                _currentTime = 0;
                _eventStream.OnNext(currentStageConfig.eventType);
                Debug.Log($"[Update] [KeyframeCombo] 关键帧已通过触发条件 Animation-{_state}  _currentStage-{_currentStage} _windowCountdown-{_windowCountdown} _currentTime-{_currentTime}");
                _windowCountdown = currentStageConfig.resetCooldownWindowTime;
                _inComboWindow = true;
                _currentStage = Mathf.Min(_currentStage + 1, _maxStage);

                if (_currentStage >= _keyframe.Count)
                {
                    _currentCountdown = _configCooldown;
                    _inComboWindow = false;
                    _currentStage = 0;
                    _isComboStart = false;
                    return;
                }
            }
            // 推进动画时间轴
            _currentTime += deltaTime;
            //Debug.Log($"[Update] [Animation] Animation-{_state}  _currentCountdown-{_currentCountdown} _windowCountdown-{_windowCountdown} _currentTime-{_currentTime}  _currentStage-{_currentStage}");
        }

        public void Use()
        {
            if (!IsReady()) return;
            _inComboWindow = false;
            _windowCountdown = 0;

            if (!_isComboStart && _currentStage == 0)
            {
                _isComboStart = true;
            }
        }

        private void EndComboWindow()
        {
            _inComboWindow = false;
            _windowCountdown = 0;
            _isComboStart = false;
            _currentStage = 0;
            _currentTime = 0;
            _currentCountdown = _configCooldown;
            // Debug.Log($"[EndComboWindow] [KeyframeCombo] Animation-{_state}  _currentStage-{_currentStage} _windowCountdown-{_windowCountdown} _currentTime-{_currentTime} _inComboWindow-{_inComboWindow} _isComboStart-{_isComboStart}");
        }

        public bool Refresh(CooldownSnapshotData snapshot)
        {
            if (!snapshot.AnimationState.Equals(_state))
                return false;

            _currentStage = snapshot.CurrentStage;
            _windowCountdown = snapshot.WindowCountdown;
            _inComboWindow = snapshot.IsInComboWindow;
            _currentCountdown = snapshot.CurrentCountdown;
            _currentTime = snapshot.KeyframeCurrentTime;
            // Debug.Log($"[Refresh] [KeyframeCombo] Animation-{_state}  _currentStage-{_currentStage} _currentCountdown-{_currentCountdown}" +
            //           $" _windowCountdown-{_windowCountdown} _currentTime-{_currentTime} _inComboWindow-{_inComboWindow} _isComboStart-{_isComboStart}");
            return true;
        }

        public void Reset()
        {
            _currentStage = 0;
            _windowCountdown = 0;
            _currentTime = 0;
            _currentCountdown = 0;
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
            if (_currentCountdown <= 0)
            {
                return;
            }
            _currentCountdown = Mathf.Max(0, _currentCountdown - deltaTime);
            //Debug.Log($"[Update] [Animation] Animation-{_animationState}  _currentCountdown-{_currentCountdown}");
        }
        
        public void Use()
        {
            if (!IsReady())
            {
                return;
            }
            _currentCountdown = Cooldown;
            //Debug.Log($"[Use] [Animation] Animation-{_animationState}  _currentCountdown-{_currentCountdown}");
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
    public partial struct CooldownSnapshotData : IEquatable<CooldownSnapshotData>, IPoolObject
    {
        // 通用字段
        [MemoryPackOrder(0)] public AnimationState AnimationState;
        [MemoryPackOrder(1)] public float Cooldown;
        [MemoryPackOrder(2)] public float CurrentCountdown;
        
        // 连招相关字段
        [MemoryPackOrder(3)] public int MaxAttackCount;
        [MemoryPackOrder(4)] public float AttackWindow;
        [MemoryPackOrder(5)] public int CurrentStage;
        [MemoryPackOrder(6)] public bool IsInComboWindow;
        [MemoryPackOrder(7)] public float WindowCountdown;
        
        // 关键帧相关字段
        [MemoryPackOrder(8)] public float KeyframeCurrentTime;
        [MemoryPackOrder(9)] public float ResetCooldownWindow;
        [MemoryPackOrder(10)] public float AnimationSpeed;
        [MemoryPackOrder(11)] public bool IsComboStart;

        private const float EPSILON = 0.001f;

        public override string ToString()
        {
            var sb = new StringBuilder(12);
            sb.Append($"[CooldownSnapshotData]动画 {AnimationState}当前状态");
            sb.AppendFormat("AnimationState-{0}",AnimationState);
            sb.AppendFormat("Cooldown-{0}",Cooldown);
            sb.AppendFormat("CurrentCountdown-{0}",CurrentCountdown);
            sb.AppendFormat("MaxAttackCount-{0}",MaxAttackCount);
            sb.AppendFormat("AttackWindow-{0}",AttackWindow);
            sb.AppendFormat("CurrentStage-{0}",CurrentStage);
            sb.AppendFormat("IsInComboWindow-{0}",IsInComboWindow);
            sb.AppendFormat("WindowCountdown-{0}",WindowCountdown);
            sb.AppendFormat("KeyframeCurrentTime-{0}",KeyframeCurrentTime);
            sb.AppendFormat("ResetCooldownWindow-{0}",ResetCooldownWindow);
            sb.AppendFormat("AnimationSpeed-{0}",AnimationSpeed);
            sb.AppendFormat("IsComboStart-{0}",IsComboStart);
            
            return sb.ToString();
        }

        public bool Equals(IAnimationCooldown other)
        {
            return other switch
            {
                ComboCooldown combo => CompareCombo(combo),
                KeyframeCooldown keyframe => CompareKeyframe(keyframe),
                KeyframeComboCooldown comboKeyframe => CompareComboKeyframe(comboKeyframe),
                AnimationCooldown animationCooldown => CompareAnimation(animationCooldown),
                _ => false
            };
        }

        private bool CompareAnimation(AnimationCooldown animationCooldown)
        {
            return AnimationState == animationCooldown.AnimationState && Mathf.Approximately(AnimationSpeed, animationCooldown.AnimationSpeed)
                 && Mathf.Abs(CurrentCountdown - animationCooldown.CurrentCountdown) < EPSILON;
        }

        private bool CompareCombo(ComboCooldown combo)
        {
            return AnimationState == combo.AnimationState &&
                   Mathf.Abs(CurrentCountdown - combo.CurrentCountdown) < EPSILON &&
                   CurrentStage == combo.CurrentStage &&
                   Mathf.Abs(WindowCountdown - combo.WindowRemaining) < EPSILON &&
                   IsInComboWindow == combo.IsInComboWindow &&
                   Mathf.Approximately(AnimationSpeed, combo.AnimationSpeed);
        }

        private bool CompareKeyframe(KeyframeCooldown keyframe)
        {
            return AnimationState == keyframe.AnimationState &&
                   Mathf.Abs(CurrentCountdown - keyframe.CurrentCountdown) < EPSILON &&
                   Mathf.Abs(KeyframeCurrentTime - keyframe.CurrentTime) < EPSILON &&
                   Mathf.Approximately(AnimationSpeed, keyframe.AnimationSpeed);
        }

        private bool CompareComboKeyframe(KeyframeComboCooldown comboKeyframe)
        {
            return AnimationState == comboKeyframe.AnimationState &&
                   Mathf.Abs(CurrentCountdown - comboKeyframe.CurrentCountdown) < EPSILON &&
                   CurrentStage == comboKeyframe.CurrentStage &&
                   Mathf.Abs(WindowCountdown - comboKeyframe.WindowRemaining) < EPSILON &&
                   IsInComboWindow == comboKeyframe.IsInComboWindow &&
                   Mathf.Abs(KeyframeCurrentTime - comboKeyframe.CurrentTime) < EPSILON &&
                   Mathf.Approximately(AnimationSpeed, comboKeyframe.AnimationSpeed) && 
                   IsComboStart == comboKeyframe.IsComboStart;
        }

        public static void CopyTo(IAnimationCooldown source, ref CooldownSnapshotData destination)
        {
            if (source.AnimationState != destination.AnimationState)
            {
                return;
            }
            if (source is ComboCooldown combo)
            {
                
                destination.AnimationState = combo.AnimationState;
                destination.CurrentCountdown = combo.CurrentCountdown;
                destination.MaxAttackCount = combo.MaxAttackCount;
                destination.CurrentStage = combo.CurrentStage;
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
                destination.AnimationSpeed = keyframe.AnimationSpeed;
                destination.CurrentStage = keyframe.CurrentStage;
            }
            else if (source is KeyframeComboCooldown comboKeyframe)
            {
                destination.AnimationState = comboKeyframe.AnimationState;
                destination.CurrentCountdown = comboKeyframe.CurrentCountdown;
                destination.MaxAttackCount = comboKeyframe.MaxAttackCount;
                destination.CurrentStage = comboKeyframe.CurrentStage;
                destination.AttackWindow = comboKeyframe.AttackWindow;  
                destination.IsInComboWindow = comboKeyframe.IsInComboWindow;
                destination.WindowCountdown = comboKeyframe.WindowRemaining;
                destination.KeyframeCurrentTime = comboKeyframe.CurrentTime;
                destination.AnimationSpeed = comboKeyframe.AnimationSpeed;
                destination.IsComboStart = comboKeyframe.IsComboStart;
            }
            else if (source is AnimationCooldown animationCooldown)
            {
                destination.AnimationState = animationCooldown.AnimationState;
                destination.CurrentCountdown = animationCooldown.CurrentCountdown;
                destination.AnimationSpeed = animationCooldown.AnimationSpeed;
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
                CurrentStage = combo.CurrentStage,
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
                CurrentStage = comboKeyframe.CurrentStage,
                AttackWindow = comboKeyframe.AttackWindow,
                IsInComboWindow = comboKeyframe.IsInComboWindow,
                WindowCountdown = comboKeyframe.WindowRemaining,
                KeyframeCurrentTime = comboKeyframe.CurrentTime,
                AnimationSpeed = comboKeyframe.AnimationSpeed,
                IsComboStart = comboKeyframe.IsComboStart,
            };
        }

        public bool Equals(CooldownSnapshotData other)
        {
            return AnimationState == other.AnimationState && Cooldown.Equals(other.Cooldown) && CurrentCountdown.Equals(other.CurrentCountdown) && MaxAttackCount == other.MaxAttackCount && AttackWindow.Equals(other.AttackWindow) && CurrentStage == other.CurrentStage && IsInComboWindow == other.IsInComboWindow && WindowCountdown.Equals(other.WindowCountdown) && KeyframeCurrentTime.Equals(other.KeyframeCurrentTime) && ResetCooldownWindow.Equals(other.ResetCooldownWindow) && AnimationSpeed.Equals(other.AnimationSpeed);
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
            hashCode.Add(CurrentStage);
            hashCode.Add(IsInComboWindow);
            hashCode.Add(WindowCountdown);
            hashCode.Add(KeyframeCurrentTime);
            hashCode.Add(ResetCooldownWindow);
            hashCode.Add(AnimationSpeed);
            hashCode.Add(IsComboStart);
            return hashCode.ToHashCode();
        }
        
        public CooldownSnapshotData Reset(CooldownSnapshotData snapshotData)
        {
            snapshotData.CurrentCountdown = 0;
            snapshotData.KeyframeCurrentTime = 0;
            snapshotData.CurrentStage = 0;
            snapshotData.IsInComboWindow = false;
            snapshotData.WindowCountdown = 0;
            snapshotData.ResetCooldownWindow = 0;
            snapshotData.IsComboStart = false;
            return snapshotData;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            AnimationSpeed = 0;
            CurrentCountdown = 0;
            KeyframeCurrentTime = 0;
            ResetCooldownWindow = 0;
            CurrentStage = 0;
            IsInComboWindow = false;
            WindowCountdown = 0;
            MaxAttackCount = 0;
            AttackWindow = 0;
            IsComboStart = false;
        }
    }

    public static class AnimationCooldownExtensions
    {
        
    }
}