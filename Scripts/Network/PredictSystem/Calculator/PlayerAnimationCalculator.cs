using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using UnityEngine;
using AnimationInfo = HotUpdate.Scripts.Config.ArrayConfig.AnimationInfo;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerAnimationCalculator : IPlayerStateCalculator
    {
        private AnimationComponent _animationComponent;
        private static AnimationConstant _animationConstant;
        
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private static readonly int Hp = Animator.StringToHash("Hp");
        private static readonly int GroundDistance = Animator.StringToHash("GroundDistance");
        private static readonly int InputMagnitude = Animator.StringToHash("InputMagnitude");
        private static readonly int EnvironmentState = Animator.StringToHash("EnvironmentState");
        private static readonly int SpecialAction = Animator.StringToHash("IsSpecialAction");
        private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
        private Dictionary<string, float> _originalSpeeds = new Dictionary<string, float>();
        public AnimationState CurrentAnimationState { get; private set; }
        private List<AnimationInfo> _animationInfos;
        public bool IsSpecialAction { get; private set; }
        public bool IsPlayingSpecialAction { get; private set; }
        private PlayerEnvironmentState _currentEnvironmentState;
        private bool _canComboSync;
        private int _currentComboSync;
        private CancellationTokenSource _comboWindowCts;
        public event Action<AnimationState> OnAnimationStateChanged;
        public event Action StartAttackCooldown; 

        public PlayerAnimationCalculator(AnimationComponent animationComponent, bool isClient = true)
        {
            _animationComponent = animationComponent;
            CurrentAnimationState = AnimationState.Idle;
            _animationInfos = _animationConstant.AnimationConfig.AnimationInfos;
            IsClient = isClient;
            foreach(var clip in _animationComponent.Animator.runtimeAnimatorController.animationClips) 
            {
                _originalSpeeds[clip.name] = clip.length;
            }
        }
        
        public void SetClipSpeed(AnimationState state, float speedFactor) 
        {
            if (state == AnimationState.Attack)
            {
                var clipNames = _animationConstant.AnimationConfig.GetAnimationNames(state);

                foreach (var clipName in clipNames)
                {
                    SetAnimationSpeed(clipName, speedFactor);
                }
            }
            else
            {
                var clipName = GetAnimationName(state);
                SetAnimationSpeed(clipName, speedFactor);
            }
        }

        private void SetAnimationSpeed(string clipName, float speedFactor)
        {
            var clip = _animationComponent.Animator.runtimeAnimatorController.animationClips
                .FirstOrDefault(c => c.name == clipName);
        
            clip?.SampleAnimation(_animationComponent.Animator.gameObject, Mathf.Clamp01(Time.time % clip.length) * speedFactor);
        }

        public static void SetAnimationConstant(AnimationConstant animationConstant)
        {
            _animationConstant = animationConstant;
        }

        private string GetAnimationName(AnimationInfo animationInfo, int index = 0)
        {
            if (animationInfo.animationNames == null || animationInfo.animationNames.Length <= index)
            {
                Debug.LogError("AnimationNames not found for state: " + animationInfo.state + " index: " + index);
                return null;
            }
            return animationInfo.animationType == AnimationType.Combo ? animationInfo.animationNames[0] : animationInfo.animationNames[index];
        }
        
        public bool IsMovingState()
        {
            return CurrentAnimationState is AnimationState.Move or AnimationState.Sprint or AnimationState.Idle;
        }
        
        public string GetAnimationName(AnimationState state, int index = 0) => _animationConstant.AnimationConfig.GetAnimationName(state, index);

        public AnimationState UpdateAnimationState()
        {
            if (_animationComponent.Animator.IsInTransition(0))
            {
                // 处于过渡中，可以选择忽略或处理
                return CurrentAnimationState;
            }

            var stateInfo = _animationComponent.Animator.GetCurrentAnimatorStateInfo(0);
            var newState = AnimationState.Idle;

            foreach (var info in _animationInfos)
            {
                var animationName = GetAnimationName(info);
                if (animationName == null) continue;
                if (stateInfo.IsName(animationName))
                {
                    if (info.state is AnimationState.Idle or AnimationState.Move or AnimationState.Sprint)
                    {
                        if (_animationComponent.Animator.GetFloat(InputMagnitude) > 0f)
                        {
                            newState = _animationComponent.Animator.GetBool(IsSprinting) ? AnimationState.Sprint : AnimationState.Move;
                        }
                        else
                        {
                            newState = AnimationState.Idle;
                        }
                    }
                    else
                    {
                        newState = info.state;
                    }
                    break;
                }
            }

            if (newState == CurrentAnimationState) 
                return CurrentAnimationState;
            Debug.Log($"Animation State Changed from {CurrentAnimationState} to {newState}");
            CurrentAnimationState = newState;
            IsPlayingSpecialAction = IsSpecialActionState(newState);
            _animationComponent.Animator.SetBool(SpecialAction, IsPlayingSpecialAction);
            if (CurrentAnimationState != AnimationState.Attack)
            {
                OnAnimationStateChanged?.Invoke(CurrentAnimationState);
            }
            return CurrentAnimationState;
        }

        private bool _currentAnimationCanPlay;
        
        public static bool IsClearVelocity(AnimationState state)
        {
            var animationInfo = _animationConstant.AnimationConfig.GetAnimationInfo(state);
            return animationInfo.isClearVelocity;
        }
        
        public void HandleAnimation(AnimationState newState, int index = 0)
        {
            // 验证是否可以播放
            if (!_currentAnimationCanPlay)
            {
                return;
            }
            
            // 根据动画状态执行相应的动画
            switch (newState)
            {
                case AnimationState.Jump:
                    _animationComponent.Animator.CrossFadeInFixedTime(GetAnimationName(AnimationState.Jump), 0.1f);
                    break;
                case AnimationState.SprintJump:
                    _animationComponent.Animator.CrossFadeInFixedTime(GetAnimationName(AnimationState.SprintJump), 0.1f);
                    break;
                case AnimationState.Roll:
                    IsPlayingSpecialAction = true;
                    Debug.Log("Roll");
                    _animationComponent.Animator.CrossFadeInFixedTime(GetAnimationName(AnimationState.Roll), 0.1f);
                    break;
                case AnimationState.Attack:
                    IsPlayingSpecialAction = true;
                    Debug.Log("Attack");            
                    _animationComponent.Animator.CrossFadeInFixedTime(GetAnimationName(AnimationState.Attack, index), 0.15f);
                    break;
                case AnimationState.Hit:
                    IsPlayingSpecialAction = true;
                    _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.Hit), 0.01f);
                    break;
                case AnimationState.Dead:
                    IsPlayingSpecialAction = true;
                    _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.Dead), 0.01f);
                    break;
                case AnimationState.SkillQ:
                    IsPlayingSpecialAction = true;
                    _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.SkillQ), 0.01f);
                    break;
                case AnimationState.SkillE:
                    IsPlayingSpecialAction = true;
                    _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.SkillE), 0.01f);
                    break;
            }
        }

        public bool CanPlayAnimation(AnimationState newState)
        {
            _currentAnimationCanPlay = false;
            // 已死亡状态不能播放任何动画
            if (CurrentAnimationState == AnimationState.Dead)
                return _currentAnimationCanPlay;
            
            var currentInfo = _animationConstant.AnimationConfig.GetAnimationInfo(CurrentAnimationState);
            var newInfo = _animationConstant.AnimationConfig.GetAnimationInfo(newState);

            switch (currentInfo.animationType)
            {
                case AnimationType.Continuous:
                    _currentAnimationCanPlay = currentInfo.canBeInterrupted;
                    break;
                case AnimationType.Single:
                case AnimationType.Combo:
                    _currentAnimationCanPlay = currentInfo.canBeInterrupted && newInfo.priority > currentInfo.priority;
                    break;
            }
            return _currentAnimationCanPlay;
        }
        public AnimationState DetermineAnimationState(DetermineAnimationStateParams parameters)
        {
            _currentEnvironmentState = parameters.EnvironmentState;
            // 优先处理特殊状态
            if (CurrentAnimationState == AnimationState.Dead)
            {
                return AnimationState.Dead;
            }

            if (_currentEnvironmentState == PlayerEnvironmentState.InAir && parameters.GroundDistance >= _animationConstant.MaxGroundDistance)
            {
                return AnimationState.Falling;
            }

            if (_currentEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Attack))
                    return AnimationState.Attack;

                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Roll))
                    return AnimationState.Roll;

                if (HasAnimation(parameters.InputAnimationStates, AnimationState.SprintJump))
                    return AnimationState.SprintJump;
                
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Jump))
                    return AnimationState.Jump;
                
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.SkillE))
                    return AnimationState.SkillE;
                
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.SkillQ))
                    return AnimationState.SkillQ;

                return DetermineAnimationStateByInput(parameters.InputMovement, parameters.InputAnimationStates);
            }

            if (_currentEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.SprintJump))
                    return AnimationState.SprintJump;
                
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Jump))
                    return AnimationState.Jump;
                return DetermineAnimationStateByInput(parameters.InputMovement, parameters.InputAnimationStates);
            }

            return DetermineAnimationStateByInput(parameters.InputMovement, parameters.InputAnimationStates);
        }
        
        private AnimationState DetermineAnimationStateByInput(Vector3 inputMovement, AnimationState inputStates)
        {
            // 处理移动状态
            if (inputMovement.magnitude >= _animationConstant.InputThreshold)
            {
                return inputStates.HasAnyState(AnimationState.Sprint) ? AnimationState.Sprint : AnimationState.Move;
            }

            return AnimationState.Idle;
        }
        
        public bool IsSpecialActionState(AnimationState state)
        {
            return state is AnimationState.Dead or AnimationState.Hit or AnimationState.Attack or AnimationState.Roll or AnimationState.SkillQ or AnimationState.SkillE;
        }
        
        private bool HasAnimation(AnimationState animationStates, AnimationState animationState)
        {
            return animationStates.HasAnyState(animationState);
        }
        
        public void SetGroundDistance(float distance)
        {
            _animationComponent.Animator.SetFloat(GroundDistance, distance);
        }

        public void SetAnimatorParams(float magnitude, float verticalSpeed, float speed)
        {
            _animationComponent.Animator.SetFloat(InputMagnitude, magnitude);
            _animationComponent.Animator.SetFloat(Speed, speed);
            _animationComponent.Animator.SetFloat(VerticalSpeed, verticalSpeed);
        }

        public void SetEnvironmentState(PlayerEnvironmentState state)
        {
            _animationComponent.Animator.SetInteger(EnvironmentState, (int) state);
        }
        
        public void SetHit()
        {
            if (CurrentAnimationState is AnimationState.Hit or AnimationState.Dead)
            {
                return;
            }
            IsPlayingSpecialAction = true;
            _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.Hit), 0.01f);
        }
        
        public void SetDeath()
        {
            if (CurrentAnimationState is AnimationState.Dead)
            {
                return;
            }
            IsPlayingSpecialAction = true;
            _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.Dead), 0.01f);
        }
        public void RequestAttack()
        {
            if (_currentComboSync > 0)
            {
                if (CurrentAnimationState == AnimationState.Attack || IsMovingState())
                {
                    //Debug.Log($"Request Attack - Combo: {_currentComboSync}, CanCombo: {_canComboSync}, CurrentState: {_currentState}");
                    StartAttack();
                }
            }
            else
            {
                if (!IsMovingState()) return;
                //Debug.Log($"Request Attack - Combo: {_currentComboSync}, CanCombo: {_canComboSync}, CurrentState: {_currentState}");
                StartAttack();
            }
        }
        
        private void StartAttack()
        {
            _comboWindowCts?.Cancel();
            _comboWindowCts = new CancellationTokenSource();
            _currentComboSync++;
            _canComboSync = false;
            OnAnimationStateChanged?.Invoke(AnimationState.Attack);

            // 触发对应段数的攻击动画

            // 如果是最后一击，直接进入冷却
            if (_currentComboSync >= _animationConstant.AttackComboMaxCount)
            {
                // 重置所有状态
                _comboWindowCts?.Cancel();
                StartCooldown();
            }
        }

        // private void OnAttack()
        // {
        //     OnAttackHit?.Invoke();
        //     ComboWindow().Forget();
        // }

        public async UniTask AttackComboWindow(float cooldown)
        {
            try
            {
                _canComboSync = true;

                // 等待连招窗口时间，使用可取消的token
                await UniTask.Delay((int)(cooldown * 1000), 
                    cancellationToken: _comboWindowCts.Token);

                // 如果没有接上下一段，进入冷却
                if (_canComboSync)
                {
                    // 重置所有状态
                    _comboWindowCts?.Cancel();
                    StartCooldown();
                }
            }
            catch (OperationCanceledException)
            {
                // 连招窗口被取消，不做处理
                Debug.Log("Combo window cancelled");
            }
        }

        private void StartCooldown()
        {
            _canComboSync = false;
            _currentComboSync = 0;
            StartAttackCooldown?.Invoke();  
        }

        public bool IsClient { get; private set; }
    }

    public class AnimationComponent
    {
        public Animator Animator;
    }

    public struct DetermineAnimationStateParams
    {
        public Vector3 InputMovement;
        public PlayerEnvironmentState EnvironmentState;
        public float GroundDistance;
        public AnimationState InputAnimationStates;
        
        public DetermineAnimationStateParams(Vector3 inputMovement, PlayerEnvironmentState environmentState, float groundDistance, AnimationState inputAnimationStates)
        {
            InputMovement = inputMovement;
            EnvironmentState = environmentState;
            GroundDistance = groundDistance;
            InputAnimationStates = inputAnimationStates;
        }
    }
        
    public struct AnimationConstant
    {
        public float MaxGroundDistance;
        public float InputThreshold;
        public int AttackComboMaxCount;
        public AnimationConfig AnimationConfig;
        public bool IsServer;

        public AnimationConstant(float maxGroundDistance, float inputThreshold, int attackComboMaxCount, AnimationConfig animationConfig, bool isServer)
        {
            MaxGroundDistance = maxGroundDistance;
            InputThreshold = inputThreshold;
            AttackComboMaxCount = attackComboMaxCount;
            AnimationConfig = animationConfig;
            IsServer = isServer;
        }
    }
}