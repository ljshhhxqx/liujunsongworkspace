using HotUpdate.Scripts.Config;
using Mirror;
using Tool.GameEvent;
using UnityEngine;

namespace Network.Client
{
    public class PlayerAnimationClient : ClientBase
    {
        private Rigidbody rb;
        private Animator animator;
        //玩家是否在地面上
        private bool isGrounded;
        //玩家是否在地面上
        private bool isJumpTrigger;
        //是否完整初始化
        private bool isReady;
        private PlayerDataConfig playerDataConfig;
        private GameDataConfig gameDataConfig;
        private NetworkAnimator networkAnimator;
        
        protected override void InitCallback()
        {
            rb = GetComponent<Rigidbody>();
            animator = GetComponent<Animator>();
            networkAnimator = GetComponent<NetworkAnimator>();
            playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            gameEventManager.Subscribe<PlayerJumpEvent>(OnPlayerJump);
            gameEventManager.Subscribe<PlayerSpeedChangeEvent>(OnPlayerSpeedChange);
            gameEventManager.Subscribe<PlayerVerticalSpeedChangeEvent>(OnPlayerVerticalSpeedChange);
        }

        private void OnPlayerVerticalSpeedChange(PlayerVerticalSpeedChangeEvent playerVerticalSpeedChangeEvent)
        {
            animator.SetFloat("VerticalSpeed", playerVerticalSpeedChangeEvent.VerticalSpeed);
        }

        [Client]
        private void OnPlayerSpeedChange(PlayerSpeedChangeEvent playerSpeedChangeEvent)
        {
            animator.SetFloat("Speed", playerSpeedChangeEvent.Speed);
        }

        [Client]
        private void OnPlayerJump(PlayerJumpEvent playerJumpEvent)
        {
            Debug.Log($"OnPlayerSpeedChange:{playerJumpEvent}");
            networkAnimator.SetTrigger("IsJumpTriggered");
            isJumpTrigger = true;
        }

        private void OnDestroy()
        {
            gameEventManager.Unsubscribe<PlayerJumpEvent>(OnPlayerJump);
            gameEventManager.Unsubscribe<PlayerSpeedChangeEvent>(OnPlayerSpeedChange);
            gameEventManager.Unsubscribe<PlayerVerticalSpeedChangeEvent>(OnPlayerVerticalSpeedChange);
        }
    }
}