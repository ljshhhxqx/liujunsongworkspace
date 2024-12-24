using UniRx;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class WeaponIKController : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;
        [SerializeField]
        private Transform rightHandIKTarget;
        private WeaponComponent _weaponComponent;

        [Header("IK Weights")]
        [Range(0, 1)]
        [SerializeField]
        private float positionWeight = 1.0f;
        [Range(0, 1)]
        [SerializeField]
        private float rotationWeight = 1.0f;
        private const float MaxWeight = 1.0f;
        private IReadOnlyReactiveProperty<AnimationState> _animationState;

        private void Start()
        {
            var animatorController = GetComponent<PlayerAnimationComponent>();
            _weaponComponent = rightHandIKTarget.GetComponentInChildren<WeaponComponent>();
            if (animatorController)
            {
                _animationState = animatorController.CurrentState;
            }
        }

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator && _weaponComponent && _animationState != null)
            {
                // 设置右手 IK 目标的位置和旋转
                if (rightHandIKTarget)
                {
                    animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandIKTarget.position);
                    animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandIKTarget.rotation);
                    animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _animationState.Value == AnimationState.Attack ? MaxWeight : positionWeight);
                    animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _animationState.Value == AnimationState.Attack ? MaxWeight : rotationWeight);
                }
            }
        }
    }
}