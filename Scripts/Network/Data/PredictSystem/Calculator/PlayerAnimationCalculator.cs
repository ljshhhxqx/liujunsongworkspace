using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using UnityEngine;
using AnimationInfo = HotUpdate.Scripts.Config.ArrayConfig.AnimationInfo;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.Calculator
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
        
        private bool IsMovingState()
        {
            return CurrentAnimationState is AnimationState.Move or AnimationState.Sprint or AnimationState.Idle;
        }
        
        private string GetAnimationName(AnimationState state, int index = 0) => _animationConstant.AnimationConfig.GetAnimationName(state, index);

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
        
        public bool HandleAnimation(AnimationState newState, Func<AnimationState, bool> canPlayAnimationByStrengthAndCd)
        {
            // 验证是否可以播放
            if (!CanPlayAnimation(newState, canPlayAnimationByStrengthAndCd))
            {
                return false;
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
                    RequestAttack();
                    break;
                case AnimationState.Hit:
                    IsPlayingSpecialAction = true;
                    _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.Hit), 0.01f);
                    break;
                case AnimationState.Dead:
                    IsPlayingSpecialAction = true;
                    _animationComponent.Animator.CrossFade(GetAnimationName(AnimationState.Dead), 0.01f);
                    break;
            }
            return true;
        }

        private bool CanPlayAnimation(AnimationState newState, Func<AnimationState, bool> canPlayAnimationByStrengthAndCd)
        {
            // 已死亡状态不能播放任何动画
            if (CurrentAnimationState == AnimationState.Dead)
                return false;
            
            var currentInfo = _animationConstant.AnimationConfig.GetAnimationInfo(CurrentAnimationState);
            var newInfo = _animationConstant.AnimationConfig.GetAnimationInfo(newState);

            // 检查冷却和体力
            if (!canPlayAnimationByStrengthAndCd(newState))
                return false;

            switch (currentInfo.animationType)
            {
                case AnimationType.Continuous:
                    //Debug.Log($"CanPlayAnimation - Continuous: {currentInfo.State.ToString()}-{currentInfo.CanBeInterrupted}, {newInfo.State.ToString()}-{newInfo.CanBeInterrupted}");
                    return currentInfo.canBeInterrupted;
                case AnimationType.Single:
                    // 一次性动画只有在可被打断时才能切换到优先级更高的动画
                    return currentInfo.canBeInterrupted && newInfo.priority > currentInfo.priority;
                case AnimationType.Combo:
                    // 连击中的动画只能继续连击或被高优先级动画打断（如受击）
                    if (newState == AnimationState.Attack)
                    {
                        return true;
                        //return _canComboSync;
                    }
                    return currentInfo.canBeInterrupted && newInfo.priority > currentInfo.priority;
                default:
                    return false;
            }
        }
        public AnimationState DetermineAnimationState(DetermineAnimationStateParams parameters, Func<AnimationState, bool> canPlayAnimationByStrengthAndCd)
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
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Attack) && canPlayAnimationByStrengthAndCd(AnimationState.Attack))
                    return AnimationState.Attack;

                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Roll) && canPlayAnimationByStrengthAndCd(AnimationState.Roll))
                    return AnimationState.Roll;

                if (HasAnimation(parameters.InputAnimationStates, AnimationState.SprintJump) && canPlayAnimationByStrengthAndCd(AnimationState.SprintJump))
                    return AnimationState.SprintJump;
                
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Jump) && canPlayAnimationByStrengthAndCd(AnimationState.Jump))
                    return AnimationState.Jump;

                return DetermineAnimationStateByInput(parameters.InputMovement, parameters.InputAnimationStates);
            }

            if (_currentEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.SprintJump) && canPlayAnimationByStrengthAndCd(AnimationState.SprintJump))
                    return AnimationState.SprintJump;
                
                if (HasAnimation(parameters.InputAnimationStates, AnimationState.Jump) && canPlayAnimationByStrengthAndCd(AnimationState.Jump))
                    return AnimationState.Jump;
                return DetermineAnimationStateByInput(parameters.InputMovement, parameters.InputAnimationStates);
            }

            return DetermineAnimationStateByInput(parameters.InputMovement, parameters.InputAnimationStates);
        }
        
        private AnimationState DetermineAnimationStateByInput(Vector3 inputMovement, List<AnimationState> inputStates)
        {
            // 处理移动状态
            if (inputMovement.magnitude >= _animationConstant.InputThreshold)
            {
                return inputStates.Any(state => state == AnimationState.Sprint) ? AnimationState.Sprint : AnimationState.Move;
            }

            return AnimationState.Idle;
        }
        
        public bool IsSpecialActionState(AnimationState state)
        {
            return state is AnimationState.Dead or AnimationState.Hit or AnimationState.Attack or AnimationState.Roll;
        }
        
        private bool HasAnimation(List<AnimationState> animationStates, AnimationState animationState)
        {
            return animationStates.Any(state => animationState == state);
        }
        
        public void SetGroundDistance(float distance)
        {
            _animationComponent.Animator.SetFloat(GroundDistance, distance);
        }

        public void SetAnimatorParams(float magnitude, float verticalSpeed, float speed)
        {
            _animationComponent.Animator.SetFloat(InputMagnitude, magnitude);
            _animationComponent.Animator.SetFloat(Speed, speed);
            _animationComponent.Animator.SetFloat(VerticalSpeed, speed);
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
            _animationComponent.Animator.CrossFadeInFixedTime(GetAnimationName(AnimationState.Attack, _currentComboSync), 0.15f);

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
        public List<AnimationState> InputAnimationStates;
        
        public DetermineAnimationStateParams(Vector3 inputMovement, PlayerEnvironmentState environmentState, float groundDistance, List<AnimationState> inputAnimationStates)
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
        
        public AnimationConstant(float maxGroundDistance, float inputThreshold, int attackComboMaxCount, AnimationConfig animationConfig)
        {
            MaxGroundDistance = maxGroundDistance;
            InputThreshold = inputThreshold;
            AttackComboMaxCount = attackComboMaxCount;
            AnimationConfig = animationConfig;
        }
    }
}