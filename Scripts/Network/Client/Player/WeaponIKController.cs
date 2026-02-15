using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using UniRx;
using UnityEngine;
using AnimationState = AOTScripts.Data.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class WeaponIKController : MonoBehaviour
    {
        [SerializeField]
        private Animator animator;
        [SerializeField]
        private Transform rightHandIKTarget;
        private PlayerAnimationCalculator _playerAnimationCalculator;

        [Header("IK Weights")]
        [Range(0, 1)]
        [SerializeField]
        private float positionWeight = 1.0f;
        [Range(0, 1)]
        [SerializeField]
        private float rotationWeight = 1.0f;
        private const float MaxWeight = 1.0f;
        private GameObject _weapon;

        private void OnAnimatorIK(int layerIndex)
        {
            if (animator && rightHandIKTarget && _playerAnimationCalculator != null)
            {
                animator.SetIKPosition(AvatarIKGoal.RightHand, rightHandIKTarget.position);
                animator.SetIKRotation(AvatarIKGoal.RightHand, rightHandIKTarget.rotation);
                animator.SetIKPositionWeight(AvatarIKGoal.RightHand, _playerAnimationCalculator.CurrentAnimationState == AnimationState.Attack ? MaxWeight : positionWeight);
                animator.SetIKRotationWeight(AvatarIKGoal.RightHand, _playerAnimationCalculator.CurrentAnimationState == AnimationState.Attack ? MaxWeight : rotationWeight);
            }
        }
        
        public void SetPlayerAnimationCalculator(PlayerAnimationCalculator playerAnimationCalculator)
        {
            _playerAnimationCalculator = playerAnimationCalculator;
        }

        public void SetWeapon(GameObject weapon)
        {
            if (!weapon)
            {
                Destroy(_weapon);
                _weapon = null;
            }
            _weapon = weapon;
            _weapon.transform.SetParent(rightHandIKTarget);
        }
    }
}