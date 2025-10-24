using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.Sync;
using Network.NetworkMes;
using UnityEngine;
using VContainer;
using AnimationState = AOTScripts.Data.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerInput : NetworkAutoInjectComponent
    {
        private PlayerControlClient _playerControlClient;
        private PlayerAnimationComponent _playerAnimationComponent;
        private FrameSyncManager _frameSyncManager;
        private JsonDataConfig _jsonDataConfig;
        private int _inputSequence = 0;
        private readonly Queue<InputData> _inputBuffer = new Queue<InputData>();
        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private PlayerInputInfo _playerInputInfo;
        private AnimationConfig _animationConfig;
        
        [Header("Input Settings")]
        [SerializeField] private float inputBufferTime = 0.1f;
        [SerializeField] private float moveThreshold = 0.1f;
        
        private bool _isSprintPressed;
        private bool _wasGrounded;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _playerControlClient = GetComponent<PlayerControlClient>();
            _mirrorNetworkMessageHandler = FindObjectOfType<MirrorNetworkMessageHandler>();
            _playerAnimationComponent = GetComponent<PlayerAnimationComponent>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _frameSyncManager = FindObjectOfType<FrameSyncManager>();
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            
            //if (!isLocalPlayer) enabled = false;
        }
        
        private void Update()
        {
            if (!isLocalPlayer) return;
            
            // 捕获输入并处理
            var input = CaptureInput();
            if (input.command != AnimationState.None)
            {
                //Debug.Log("Received command: " + input.command);
                ProcessInput(input);
            }
        }
        
        private InputData CaptureInput()
        {
            var playerInputInfo = new PlayerInputCommand
            {
                playerInputMovement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical")),
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
                playerInput = playerInputInfo,
                rotation = transform.rotation,
                command = requestAnimation
            };
        
            return input;
        }
        
        private void ProcessInput(InputData input)
        {
            var actionType = _animationConfig.GetActionType(input.command);

            if (actionType == ActionType.Movement)
            {
                // 立即在本地执行
                _playerControlClient.ExecutePlayerLocalInput(input);
                // 缓存输入
                _inputBuffer.Enqueue(input);
                // 清理过期输入
                while (_inputBuffer.Count > 0 && 
                       Time.time - _inputBuffer.Peek().timestamp > inputBufferTime)
                {
                    _inputBuffer.Dequeue();
                }
            }
        
            // 发送到服务器（除了纯动画状态）
            if (actionType != ActionType.Animation)
            {
                _mirrorNetworkMessageHandler.SendToServer(new MirrorPlayerInputInfoMessage
                {
                    input = input,
                    connectionID = connectionToClient.connectionId
                });
            }
            else
            {
                _playerControlClient.ExecuteAnimationInput(input);
            }
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