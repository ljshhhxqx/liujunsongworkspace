using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.Server.Sync;
using Mirror;
using Network.NetworkMes;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;
using static HotUpdate.Scripts.Config.AnimationState;
using AnimationInfo = HotUpdate.Scripts.Config.AnimationInfo;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerAnimationComponent : NetworkBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private static readonly int Hp = Animator.StringToHash("Hp");
        private static readonly int GroundDistance = Animator.StringToHash("GroundDistance");
        private static readonly int InputMagnitude = Animator.StringToHash("InputMagnitude");
        private static readonly int EnvironmentState = Animator.StringToHash("EnvironmentState");
        private static readonly int SpecialAction = Animator.StringToHash("IsSpecialAction");
        private static readonly int CurrentCombo = Animator.StringToHash("CurrentCombo");
        private static readonly int CanCombo = Animator.StringToHash("CanCombo");
        private static readonly int IsSprinting = Animator.StringToHash("IsSprinting");
        private readonly SyncDictionary<AnimationState, float> _animationCooldown = new SyncDictionary<AnimationState, float>();
        private readonly SyncList<AnimationState> _animationState = new SyncList<AnimationState>();
        //private readonly Dictionary<AnimationState, float> _animationCooldown = new Dictionary<AnimationState, float>();
        private const string JumpMove = "JumpMove";
        private const string Jump = "Jump";
        private const string Roll = "Roll";
        private const string Attack1 = "Attack1";
        private const string Attack2 = "Attack2";        
        private const string Attack3 = "Attack3";
        private const string Falling = "Falling";
        private const string Landed = "Landed";
        private const string MoveBlend = "MoveBlend";
        private const string Death = "Death";
        private const string Hit = "Hit";

        private Animator _animator;
        private NetworkAnimator _networkAnimator;
        private PlayerDataConfig _playerDataConfig;
        private UIManager _uiManager;
        private PlayerNotifyManager _playerNotifyManager;
        private PlayerPropertyComponent _playerPropertyComponent;

        // 动画状态
        [SyncVar(hook = nameof(OnRequestedAnimationStateChanged))]
        private AnimationState _requestedAnimationState = Idle;
        [SyncVar(hook = nameof(OnCurrentAnimationStateChanged))]
        private AnimationState _currentAnimationState = Idle;

        private void OnRequestedAnimationStateChanged(AnimationState oldState, AnimationState newState)
        {
            if (isClient && newState != _requestedAnimationState)
            {
                if (!TryPlayAnimation(newState))
                {
                    Debug.LogWarning($"播放动画失败：{newState}");
                }
            }
        }

        private void OnCurrentAnimationStateChanged(AnimationState oldState, AnimationState newState)
        {
            _currentState.Value = newState;
            _playerPropertyComponent.CurrentAnimationState = newState;
            IsPlayingSpecialAction = IsSpecialActionState(newState);
            _animator.SetBool(SpecialAction, IsPlayingSpecialAction);
        }

        // 统一的动画播放API
        public bool TryPlayAnimation(AnimationState newState)
        {
            // 验证是否可以播放
            if (!CanPlayAnimation(newState))
                return false;

            // 根据动画状态执行相应的动画
            switch (newState)
            {
                case AnimationState.Jump:
                    _animator.CrossFadeInFixedTime(Jump, 0.15f);
                    break;
                case SprintJump:
                    _animator.CrossFadeInFixedTime(JumpMove, 0.15f);
                    break;
                case AnimationState.Roll:
                    _animator.CrossFadeInFixedTime(Roll, 0.15f);
                    break;
                case Attack:
                    RequestAttack();
                    break;
                case AnimationState.Hit:
                    _animator.CrossFade(Hit, 0.01f);
                    break;
                case Dead:
                    _animator.CrossFade(Death, 0.01f);
                    break;
            }

            if (isServer)
            {
                _currentAnimationState = newState;
            }
            else
            {
                CmdUpdateCurrentAnimationState(newState);
            }

            return true;
        }
        [Command]
        private void CmdUpdateCurrentAnimationState(AnimationState newState)
        {
            _currentAnimationState = newState;
        }

        public bool CanPlayAnimation(AnimationState newState)
        {
            // 已死亡状态不能播放任何动画
            if (_currentAnimationState == Dead)
                return false;

            if (_currentAnimationState == Attack)
            {
                return CanDoAnimation(newState) && _playerPropertyComponent.StrengthCanDoAnimation(newState);
            }

            // 检查优先级
            if ((int)newState <= (int)_currentAnimationState && _currentAnimationState != newState)
                return false;

            // 检查冷却和体力
            if (!_playerPropertyComponent.StrengthCanDoAnimation(newState) || !CanDoAnimation(newState))
                return false;

            return true;
        }

        // 帧同步相关方法
        public AnimationState ExecuteAnimationState(PlayerInputInfo input, PlayerEnvironmentState environmentState)
        {
            // 根据输入和环境状态确定应该播放的动画
            var requestedState = DetermineAnimationState(input, environmentState);
            
            if (requestedState != _requestedAnimationState)
            {
                if (isServer)
                {
                    _requestedAnimationState = requestedState;
                }
                else
                {
                    CmdUpdateRequestedAnimationState(requestedState);
                }
            }

            // 更新动画参数
            UpdateAnimationParameters(input, environmentState);
            return requestedState;
        }

        [Command]
        private void CmdUpdateRequestedAnimationState(AnimationState newState)
        {
            _requestedAnimationState = newState;
        }
        
        private PlayerEnvironmentState _lastEnvironmentState;

        private AnimationState DetermineAnimationState(PlayerInputInfo input, PlayerEnvironmentState environmentState)
        {
            _lastEnvironmentState = environmentState;
            // 优先处理特殊状态
            if (_currentAnimationState == Dead)
            {
                return Dead;
            }

            if (environmentState == PlayerEnvironmentState.InAir)
            {
                return AnimationState.Falling;
            }

            if (environmentState == PlayerEnvironmentState.OnGround)
            {
                if (input.isAttackRequested && CanPlayAnimation(Attack))
                    return Attack;

                if (input.isRollRequested && CanPlayAnimation(AnimationState.Roll))
                    return AnimationState.Roll;

                if (input.isJumpRequested)
                {
                    var jumpState = input.isSprinting ? SprintJump : AnimationState.Jump;
                    if (CanPlayAnimation(jumpState))
                        return jumpState;
                }

                return DetermineAnimationStateByInput(input);
            }

            if (environmentState == PlayerEnvironmentState.OnStairs)
            {
                if (input.isJumpRequested)
                {
                    var jumpState = input.isSprinting ? SprintJump : AnimationState.Jump;
                    if (CanPlayAnimation(jumpState))
                        return jumpState;
                }
                return DetermineAnimationStateByInput(input);
            }

            return DetermineAnimationStateByInput(input);
        }

        private AnimationState DetermineAnimationStateByInput(PlayerInputInfo input)
        {
            // 处理移动状态
            if (input.movement.magnitude > 0)
            {
                return input.isSprinting ? Sprint : Move;
            }

            return Idle;
        }

        private void UpdateAnimationParameters(PlayerInputInfo input, PlayerEnvironmentState environmentState)
        {
            _animator.SetFloat(InputMagnitude, input.movement.magnitude);
            _animator.SetBool(IsSprinting, input.isSprinting);
            _animator.SetInteger(EnvironmentState, (int)environmentState);
        }

        public bool IsSpecialActionState(AnimationState state)
        {
            return state is Dead or AnimationState.Hit or Attack or AnimationState.Roll;
        }
        
        private readonly ReactiveProperty<AnimationState> _currentState = new ReactiveProperty<AnimationState>(Idle);
        public IReadOnlyReactiveProperty<AnimationState> CurrentState => _currentState;

        [SyncVar]
        private bool _canComboSync;
        [SyncVar]
        private int _currentComboSync;
        private AnimationInfo _attackInfo;
        private CancellationTokenSource _comboWindowCts;
        
        public bool IsPlayingSpecialAction { get; private set; }

        public event Action<bool> OnBecameInvisible;
        public event Action OnGetHit;
        public event Action OnAttackHit;

        [Inject]
        private void Init(IConfigProvider configProvider, UIManager uiManager, PlayerNotifyManager playerNotifyManager)
        {
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            _playerNotifyManager = playerNotifyManager;
            _networkAnimator = GetComponent<NetworkAnimator>();
            _uiManager = uiManager;
            _attackInfo = _playerDataConfig.GetAnimationInfo(Attack);
            _playerPropertyComponent = GetComponent<PlayerPropertyComponent>();
            _animator = GetComponent<Animator>();
        }
        
        // private void OnAttack()
        // {
        //     OnAttackHit?.Invoke();
        //     
        //     // 打开连招窗口
        //     ComboWindow().Forget();
        // }
        //
        // private void OnHit()
        // {
        //     OnGetHit?.Invoke();
        // }
        //
        // private void OnInvincibleStart()
        // {
        //     OnBecameInvisible?.Invoke(true);
        //     _playerPropertyComponent.IsInvincible = true;
        // }
        //
        // private void OnInvincibleStop()
        // {
        //     OnBecameInvisible?.Invoke(false);
        //     _playerPropertyComponent.IsInvincible = false;
        // }

        public void SetIsSprinting(bool isSprinting)
        {
            _animator.SetBool(IsSprinting, isSprinting);
        }

        private void Update()
        {
            if (isServer)
            {
                for (var i = _animationState.Count - 1; i >= 0; i--)
                {
                    var state = _animationState[i];
                    _animationCooldown[state] -= Time.deltaTime;
                    if (_animationCooldown[state] <= 0f)
                    {
                        _animationCooldown.Remove(state);
                        _animationState.RemoveAt(i);
                    }
                }
            }
            if (!isLocalPlayer) return;

            if (Input.GetKeyDown(KeyCode.P))
            {
                SetHit();
            }
            if (Input.GetKeyDown(KeyCode.L))
            {
                SetDeath();
            }

            //UpdateAnimationState();
        }

        public bool IsMovingState()
        {
            return _currentState.Value is Move or Sprint or Idle or AnimationState.Landed;
        }

        public bool IsJumpingState()
        {
            return _currentState.Value is AnimationState.Jump or SprintJump or AnimationState.Falling;
        }

        private void UpdateAnimationState()
        {
            if (_animator.IsInTransition(0))
            {
                // 处于过渡中，可以选择忽略或处理
                return;
            }

            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            var newState = Idle;

            if (stateInfo.IsName(JumpMove))
            {
                newState = SprintJump;
            }
            else if (stateInfo.IsName(Jump))
            {
                newState = AnimationState.Jump;
            }
            else if (stateInfo.IsName(Roll))
            {
                newState = AnimationState.Roll;
            }
            else if (stateInfo.IsName(Attack1) || stateInfo.IsName(Attack2) || stateInfo.IsName(Attack3))
            {
                newState = Attack;
            }
            else if (stateInfo.IsName(Falling))
            {
                newState = AnimationState.Falling;
            }
            else if (stateInfo.IsName(Landed))
            {
                newState = AnimationState.Landed;
            }
            else if (stateInfo.IsName(MoveBlend))
            {
                if (_animator.GetFloat(InputMagnitude) > 0f)
                {
                    Debug.Log("Start Move");
                    newState = _animator.GetBool(IsSprinting) ? Sprint : Move;
                }
                else
                {
                    Debug.Log("start Idle");
                    newState = Idle;
                }
            }
            else if (stateInfo.IsName(Death))
            {
                newState = AnimationState.Death;
            }
            else if (stateInfo.IsName(Hit))
            {
                newState = AnimationState.Hit;
            }

            if (newState == _currentState.Value) 
                return;
            Debug.Log($"Animation State Changed from {_currentState} to {newState}");
            IsPlayingSpecialAction = newState is Dead or AnimationState.Hit or Attack or AnimationState.Roll;
            _animator.SetBool(SpecialAction, IsPlayingSpecialAction);
            if (_currentState.Value != Attack)
            {
                _playerPropertyComponent.DoAnimation(_currentState.Value);
            }
            // _animator.SetBool(CanCombo, _canComboSync);
            // _animator.SetInteger(CurrentCombo, _currentComboSync);
        }
        
        public void SetInputMagnitude(float magnitude)  
        {
            _animator.SetFloat(InputMagnitude, magnitude);
        }

        public void SetFallSpeed(float speed)
        {
            _animator.SetFloat(VerticalSpeed, speed);
        }
        
        public void SetGroundDistance(float distance)
        {
            _animator.SetFloat(GroundDistance, distance);
        }

        public void SetMoveSpeed(float speed)
        {
            _animator.SetFloat(Speed, speed);
        }

        public void SetEnvironmentState(PlayerEnvironmentState state)
        {
            _animator.SetInteger(EnvironmentState, (int) state);
        }

        public void RequestJump(bool isSprinting)
        {
            if (!_playerPropertyComponent.StrengthCanDoAnimation(isSprinting ? SprintJump : AnimationState.Jump) ||
                _currentState.Value is AnimationState.Jump or SprintJump || IsPlayingSpecialAction)
            {
                return;
            }
            
            _animator.CrossFadeInFixedTime(isSprinting? JumpMove : Jump, 0.15f);
        }
        
        public void SetRoll()
        {
            if (!_playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Roll) || _currentState.Value == AnimationState.Roll || IsPlayingSpecialAction)
            {
                return;
            }
            IsPlayingSpecialAction = true;
            _animator.CrossFadeInFixedTime(Roll, 0.15f);
        }
        
        public void SetHit()
        {
            if (_currentState.Value is AnimationState.Hit or Dead)
            {
                return;
            }
            IsPlayingSpecialAction = true;
            _animator.CrossFade(Hit, 0.01f);
        }
        
        public void SetDeath()
        {
            if (_currentState.Value is Dead)
            {
                return;
            }
            IsPlayingSpecialAction = true;
            _animator.CrossFade(Death, 0.01f);
        }


        public void SetHp(float hp)
        {
            _animator.SetFloat(Hp, hp);
        }

        public void RequestAttack()
        {
            if (_currentComboSync > 0)
            {
                if (_currentAnimationState == Attack || IsMovingState())
                {
                    Debug.Log($"Request Attack - Combo: {_currentComboSync}, CanCombo: {_canComboSync}, CurrentState: {_currentState}");
                    StartAttack();
                }
            }
            else
            {
                if (!IsMovingState()) return;
                Debug.Log($"Request Attack - Combo: {_currentComboSync}, CanCombo: {_canComboSync}, CurrentState: {_currentState}");
                StartAttack();
            }

        }
        private void StartAttack()
        {
            _comboWindowCts?.Cancel();
            _comboWindowCts = new CancellationTokenSource();
            _currentComboSync++;
            _canComboSync = false;
            _playerPropertyComponent.DoAnimation(Attack);

            // 触发对应段数的攻击动画
            switch (_currentComboSync)
            {
                case 1:
                    _animator.CrossFadeInFixedTime(Attack1, 0.15f);
                    break;
                case 2:
                    _animator.CrossFadeInFixedTime(Attack2, 0.15f);
                    break;
                case 3:
                    _animator.CrossFadeInFixedTime(Attack3, 0.15f);
                    break;
            }

            // 如果是最后一击，直接进入冷却
            if (_currentComboSync >= _playerDataConfig.PlayerConfigData.AttackComboMaxCount)
            {
                // 重置所有状态
                _comboWindowCts?.Cancel();
                StartCooldown();
            }
        }

        private void OnAttack()
        {
            CmdStartComboWindow();
        }
        
        [Command]
        private void CmdStartComboWindow()
        {
            ComboWindow().Forget();
        }

        private async UniTask ComboWindow()
        {
            OnAttackHit?.Invoke();
            try
            {
                _canComboSync = true;

                // 等待连招窗口时间，使用可取消的token
                await UniTask.Delay((int)(_attackInfo.Cooldown * 1000), 
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
            CmdDoAnimation(Attack);
        }
        
        [Command]
        private void CmdDoAnimation(AnimationState animationState)
        {
            if (isClient)
            {
                if (!DoAnimation(animationState))
                {
                    _playerNotifyManager.TargetNotifyInsufficientStamina(connectionToClient, "动画冷却中！");
                }
            }
        }
        
        [Server]
        private bool DoAnimation(AnimationState animationState)
        {
            if (!CanDoAnimation(animationState)) return false;
            var cooldown = _playerDataConfig.GetPlayerAnimationCooldown(animationState);
            if (cooldown > 0f)
            {
                _animationCooldown.Add(animationState, cooldown);
                _animationState.Add(animationState);
                return true;
            }
            return cooldown == 0f;
        }

        private bool CanDoAnimation(AnimationState animationState)
        {
            if (_animationCooldown.ContainsKey(animationState))
            {
                _playerNotifyManager.TargetNotifyInsufficientStamina(connectionToClient, "动画冷却中！");
                return false;
            }
            var currentInfo = _playerDataConfig.GetAnimationInfo(_currentState.Value);
            var targetInfo = _playerDataConfig.GetAnimationInfo(animationState);
            if (animationState == Attack && _currentComboSync > 0)
            {
                return _canComboSync;
            }

            return currentInfo.Priority < targetInfo.Priority;
        }

        private void OnHit()
        {
            OnGetHit?.Invoke();
        }
        
        private void OnInvincibleStart()
        {
            Debug.Log("服务器开始无敌状态");
            _playerPropertyComponent.IsInvincible = true;
        }

        // 翻滚结束无敌
        [Server]
        private void OnInvincibleStop()
        {
            Debug.Log("服务器结束无敌状态");
            _playerPropertyComponent.IsInvincible = false;
        }
    }
}