using UnityEngine;
using Mirror;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Server.Sync;
using Network.NetworkMes;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerInput : NetworkBehaviour
    {
        private PlayerControlClient _playerControlClient;
        private PlayerAnimationComponent _playerAnimationComponent;
        private FrameSyncManager _frameSyncManager;
        private JsonDataConfig _jsonDataConfig;
        private int _inputSequence = 0;
        private readonly Queue<InputData> _inputBuffer = new Queue<InputData>();
        private PlayerInputInfo _playerInputInfo;
        
        [Header("Input Settings")]
        [SerializeField] private float inputBufferTime = 0.1f;
        [SerializeField] private float moveThreshold = 0.1f;
        
        private bool _isSprintPressed;
        private bool _wasGrounded;
        
        [Inject]
        private void Init(FrameSyncManager frameSyncManager, JsonDataConfig jsonDataConfig)
        {
            _playerControlClient = GetComponent<PlayerControlClient>();
            _playerAnimationComponent = GetComponent<PlayerAnimationComponent>();
            _jsonDataConfig = jsonDataConfig;
            _frameSyncManager = frameSyncManager;
            
            if (!isLocalPlayer) enabled = false;
        }
        
        private void Update()
        {
            if (!isLocalPlayer) return;
            
            // 捕获输入并处理
            var input = CaptureInput();
            if (input.command != AnimationState.None)
            {
                ProcessInput(input);
            }
        }
        
        private InputData CaptureInput()
        {
            var playerInputInfo = new PlayerInputCommand
            {
                movement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical")),
                isJumpRequested = Input.GetButtonDown("Jump"),
                isRollRequested = Input.GetButtonDown("Roll"),
                isAttackRequested = Input.GetButtonDown("Fire1"),
                isSprinting = Input.GetButton("Running")
            };
            var requestAnimation = _playerAnimationComponent.DetermineAnimationState(playerInputInfo, _playerControlClient.PlayerEnvironmentState, _playerControlClient.GroundDistance);
            
            var input = new InputData
            {
                sequence = _inputSequence++,
                timestamp = Time.time,
                moveDirection = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical")).normalized,
                rotation = transform.rotation,
                command = requestAnimation
            };
        
            return input;
        }
        
        private void ProcessInput(InputData input)
        {
            var actionType = _jsonDataConfig.GetActionType(input.command);
            
            switch (actionType)
            {
                case ActionType.Movement:
                    // 立即在本地执行
                    //_playerControlClient.ProcessLocalInput(input);
                    // 缓存输入
                    _inputBuffer.Enqueue(input);
                    // 清理过期输入
                    while (_inputBuffer.Count > 0 && 
                           Time.time - _inputBuffer.Peek().timestamp > inputBufferTime)
                    {
                        _inputBuffer.Dequeue();
                    }
                    break;
        
                case ActionType.Interaction:
                    // 可以播放准备动画
                    //_playerControlClient.PlayPrepareAnimation(input);
                    break;
        
                case ActionType.Animation:
                    // 直接更新动画状态
                    //_playerControlClient.UpdateAnimatorParameters(input);
                    break;
            }
        
            // 发送到服务器（除了纯动画状态）
            if (actionType != ActionType.Animation)
            {
                CmdSendInput(input);
            }
        }
        
        [Command]
        private void CmdSendInput(InputData input)
        {
            // todo:
            //_frameSyncManager.BroadcastInput(connectionToClient.connectionId, input);
        }
        
        public Queue<InputData> GetInputBuffer()
        {
            return new Queue<InputData>(_inputBuffer);
        }
        
        public void ClearInputBuffer()
        {
            _inputBuffer.Clear();
        }
    }
}