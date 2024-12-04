using HotUpdate.Scripts.Config;
using Mirror;
using UnityEngine;
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

        private void Start()
        {
            _networkAnimator = GetComponent<NetworkAnimator>();
            _animator = GetComponent<Animator>();
            SetHp(100);
        }
        
        public AnimationState GetAnimationState()
        {
            var stateInfo = _animator.GetCurrentAnimatorStateInfo(0);
            Debug.Log(stateInfo.ToString());
            if (stateInfo.IsName(JumpMove))
            {
                return AnimationState.SprintJump;
            }
            if (stateInfo.IsName(Jump))
            {
                return AnimationState.Jump;
            }
            if (stateInfo.IsName(Roll))
            {
                return AnimationState.Roll;
            }

            if (stateInfo.IsName(Attack1) || stateInfo.IsName(Attack2) || stateInfo.IsName(Attack3))
            {
                return AnimationState.Attack;
            }

            if (stateInfo.IsName(Falling))
            {
                return AnimationState.Falling;
            }

            if (stateInfo.IsName(Landed))
            {
                return AnimationState.Landed;
            }

            if (stateInfo.IsName(Move))
            {
                return AnimationState.Move;
            }

            if (stateInfo.IsName(Idle))
            {
                return AnimationState.Idle;
            }

            if (stateInfo.IsName(Death))
            {
                return AnimationState.Death;
            }

            if (stateInfo.IsName(Hit))
            {
                return AnimationState.Hit;
            }

            if (stateInfo.IsName(Sprint))
            {
                return AnimationState.Sprint;
            }

            return default;
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
            _animator.CrossFadeInFixedTime(Roll, 0.15f);
        }

        public void SetHp(float hp)
        {
            _animator.SetFloat(Hp, hp);
        }
    }
}