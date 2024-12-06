using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config;
using Mirror;
using UnityEngine;
using VContainer;
using AnimationInfo = HotUpdate.Scripts.Config.AnimationInfo;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerAnimationComponent : NetworkBehaviour
    {
        private static readonly int Speed = Animator.StringToHash("Speed");
        private static readonly int VerticalSpeed = Animator.StringToHash("VerticalSpeed");
        private static readonly int IsAttack1 = Animator.StringToHash("Attack1");
        private static readonly int IsAttack2 = Animator.StringToHash("Attack2");
        private static readonly int IsAttack3 = Animator.StringToHash("Attack3");
        private static readonly int Hp = Animator.StringToHash("Hp");
        private static readonly int GroundDistance = Animator.StringToHash("GroundDistance");
        private static readonly int InputMagnitude = Animator.StringToHash("InputMagnitude");
        private static readonly int EnvironmentState = Animator.StringToHash("EnvironmentState");
        //private readonly SyncDictionary<AnimationState, float> _animationCooldown = new SyncDictionary<AnimationState, float>();
        private readonly Dictionary<AnimationState, float> _animationCooldown = new Dictionary<AnimationState, float>();
        private const string JumpMove = "JumpMove";
        private const string Jump = "Jump";
        private const string Roll = "Roll";
        private const string Attack1 = "Attack1";
        private const string Attack2 = "Attack2";        
        private const string Attack3 = "Attack3";
        private const string Falling = "Falling";
        private const string Landed = "Landed";
        private const string Move = "Move";
        private const string Idle = "Idle";
        private const string Death = "Death";
        private const string Hit = "Hit";
        private const string Sprint = "Sprint";

        private Animator _animator;
        private NetworkAnimator _networkAnimator;
        private PlayerDataConfig _playerDataConfig;
        [SerializeField]
        private AnimationState currentState = AnimationState.Idle;
        public AnimationState CurrentState => currentState;
        private bool _canCombo;           // 是否可以接下一段连招
        private int _currentCombo;            // 当前连招数
        private AnimationInfo _attackInfo;
        
        public event Action<bool> OnBecameInvisible;
        public event Action OnGetHit;
        public event Action OnAttackHit;
        

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            _networkAnimator = GetComponent<NetworkAnimator>();
            _attackInfo = _playerDataConfig.GetAnimationInfo(AnimationState.Attack);
            _animator = GetComponent<Animator>();
        }
        
        private void OnAttack()
        {
            OnAttackHit?.Invoke();
            
            // 打开连招窗口
            ComboWindow().Forget();
        }
        
        private void OnAttackEnd()
        {
            // 关闭连招窗口
            _animator.CrossFadeInFixedTime("MoveBlend", 0.1f);
        }

        private void OnHit()
        {
            OnGetHit?.Invoke();
        }

        private void OnInvincibleStart()
        {
            OnBecameInvisible?.Invoke(true);
        }
        
        private void OnInvincibleStop()
        {
            OnBecameInvisible?.Invoke(false);
        }


        private void Update()
        {
            
            var keys = new List<AnimationState>(_animationCooldown.Keys);
            foreach (var key in keys)
            {
                _animationCooldown[key] -= Time.deltaTime;
                if (_animationCooldown[key] <= 0f)
                {
                    _animationCooldown.Remove(key);
                }
            }
            if (!isLocalPlayer) return;

            UpdateAnimationState();
        }

        public bool IsMovingState()
        {
            return currentState is AnimationState.Move or AnimationState.Sprint or AnimationState.Idle;
        }

        public bool IsJumpingState()
        {
            return currentState is AnimationState.Jump or AnimationState.SprintJump or AnimationState.Falling;
        }

        private void UpdateAnimationState()
        {
            if (_animator.IsInTransition(0))
            {
                // 处于过渡中，可以选择忽略或处理
                return;
            }

            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            var newState = AnimationState.Idle;

            if (stateInfo.IsName(JumpMove))
            {
                newState = AnimationState.SprintJump;
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
                newState = AnimationState.Attack;
            }
            else if (stateInfo.IsName(Falling))
            {
                newState = AnimationState.Falling;
            }
            else if (stateInfo.IsName(Landed))
            {
                newState = AnimationState.Landed;
            }
            else if (stateInfo.IsName(Move))
            {
                newState = AnimationState.Move;
            }
            else if (stateInfo.IsName(Idle))
            {
                newState = AnimationState.Idle;
            }
            else if (stateInfo.IsName(Death))
            {
                newState = AnimationState.Death;
            }
            else if (stateInfo.IsName(Hit))
            {
                newState = AnimationState.Hit;
            }
            else if (stateInfo.IsName(Sprint))
            {
                newState = AnimationState.Sprint;
            }

            if (newState != currentState)
            {
                Debug.Log($"Animation State Changed from {currentState} to {newState}");
                currentState = newState;
            }
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

        public void SetJump(bool isSprinting)
        {
            _animator.CrossFadeInFixedTime(isSprinting? JumpMove : Jump, 0.15f);
        }

        public void SetAttack(int attackIndex)
        {
            switch (attackIndex)
            {
                case 1:
                    _networkAnimator.SetTrigger(IsAttack1);
                    break;
                case 2:
                    _networkAnimator.SetTrigger(IsAttack2);
                    break;
                case 3:
                    _networkAnimator.SetTrigger(IsAttack3);
                    break;
            }
        }
        
        public void SetRoll()
        {
            // if (!CanDoAnimation(AnimationState.Roll)) return;
            // RequestDoAnimation(AnimationState.Roll);
            
            _animator.CrossFadeInFixedTime(Roll, 0.15f);
        }
        
        public void SetLanded()
        {
            _animator.CrossFade(Landed, 0.01f);
        }


        public void SetHp(float hp)
        {
            _animator.SetFloat(Hp, hp);
        }

        public void RequestAttack()
        {
            if (!CanDoAnimation(AnimationState.Attack) || !IsMovingState()) return;
            Debug.Log("Request Attack");

            // 开始新的连招序列或继续当前连招
            if (_currentCombo == 0 || _canCombo)
            {
                Debug.Log("Can Combo");
                StartAttack();
            }
        }
        private void StartAttack()
        {
            _currentCombo++;
            _canCombo = false;

            // 触发对应段数的攻击动画
            if (_currentCombo == 1)
            {
                _animator.CrossFadeInFixedTime(Attack1, 0.15f);
            }
            else if (_currentCombo == 2)
            {
                _animator.CrossFadeInFixedTime(Attack2, 0.15f);
            }
            else if (_currentCombo == 3)
            {
                _animator.CrossFadeInFixedTime(Attack3, 0.15f);
            }


            // 如果是最后一击，直接进入冷却
            if (_currentCombo >= _playerDataConfig.PlayerConfigData.AttackComboMaxCount)
            {
                StartCooldown();
            }
        }
        
        private async UniTask ComboWindow()
        {
            _canCombo = true;

            // 等待连招窗口时间
            await UniTask.Delay((int)(_attackInfo.Cooldown * 1000));

            // 如果没有接上下一段，进入冷却
            if (_canCombo)
            {
                StartCooldown();
            }
        }

        private void StartCooldown()
        {
            // 重置所有状态
            _canCombo = false;
            _currentCombo = 0;
            _animationCooldown[AnimationState.Attack] = _attackInfo.Cooldown;
        }
        
        private bool DoAnimation(AnimationState animationState)
        {
            if (!CanDoAnimation(animationState)) return false;
            var cooldown = _playerDataConfig.GetPlayerAnimationCooldown(animationState);
            if (cooldown > 0f)
            {
                _animationCooldown.Add(animationState, cooldown);
                return true;
            }
            return cooldown == 0f;
        }

        private bool CanDoAnimation(AnimationState animationState)
        {
            if (_animationCooldown.ContainsKey(animationState)) return false;
            var currentInfo = _playerDataConfig.GetAnimationInfo(currentState);
            var targetInfo = _playerDataConfig.GetAnimationInfo(animationState);
            return currentInfo.Priority < targetInfo.Priority;
        }

        // private void RequestDoAnimation(AnimationState animationState)
        // {
        //     if (!isLocalPlayer) return;
        //     CmdDoAnimation(animationState, connectionToServer);
        // }
        //
        // [Command]
        // private void CmdDoAnimation(AnimationState animationState, NetworkConnection connection)
        // {
        //     if (CanDoAnimation(animationState))
        //     {
        //         var cooldown = _playerDataConfig.GetPlayerAnimationCooldown(animationState);
        //         if (cooldown > 0f)
        //         {
        //             _animationCooldown.Add(animationState, cooldown);
        //             TargetDoAnimation(connection, animationState);
        //         }
        //     }
        // }
        //
        // [TargetRpc]
        // private void TargetDoAnimation(NetworkConnection target, AnimationState animationState)
        // {
        //     if (target != connectionToClient) return;
        //     switch (animationState)
        //     {
        //         case AnimationState.Jump:
        //             _animator.CrossFadeInFixedTime(Jump, 0.15f);
        //             break;
        //         case AnimationState.Falling:
        //             _animator.CrossFadeInFixedTime(Falling, 0.15f);
        //             break;
        //         case AnimationState.Landed:
        //             _animator.CrossFadeInFixedTime(Landed, 0.15f);
        //             break;
        //         case AnimationState.Move:
        //             _animator.CrossFadeInFixedTime(Move, 0.15f);
        //             break;
        //         case AnimationState.Idle:
        //             _animator.CrossFadeInFixedTime(Idle, 0.15f);
        //             break;
        //         case AnimationState.Death:
        //             _animator.CrossFadeInFixedTime(Death, 0.15f);
        //             break;
        //         case AnimationState.Hit:
        //             _animator.CrossFadeInFixedTime(Hit, 0.15f);
        //             break;
        //         case AnimationState.Sprint:
        //             _animator.CrossFadeInFixedTime(Sprint, 0.15f);
        //             break;
        //         case AnimationState.SprintJump:
        //             _animator.CrossFadeInFixedTime(JumpMove, 0.15f);
        //             break;
        //         case AnimationState.Roll:
        //             _animator.CrossFadeInFixedTime(Roll, 0.15f);
        //             break;
        //         case AnimationState.Attack:
        //             break;
        //     }
        // }
    }
}