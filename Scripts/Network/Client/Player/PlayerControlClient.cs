using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.Sync;
using Mirror;
using Network.NetworkMes;
using Tool.GameEvent;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerControlClient : NetworkBehaviour
    {
        private PlayerPropertyComponent _playerPropertyComponent;
        private PlayerAnimationComponent _playerAnimationComponent;
        private MirrorNetworkMessageHandler _messageHandler;
        private FrameSyncManager _frameSyncManager;
        private UIManager _uiManager;
        private PlayerInputInfo _playerInput;
        private readonly Queue<InputData> _pendingInputs = new Queue<InputData>();
        
        private float _speedSmoothTime = 0.1f; // 速度平滑时间
        //决定摄像机的旋转中心
        private Transform _rotateCenter;
        private Transform _checkStairsTransform;
        private Rigidbody _rigidbody;
        private JsonDataConfig _jsonDataConfig;
        //玩家的摄像机
        private Camera _camera;
        private float _speedSmoothVelocity;
        private float _targetSpeed;
        private float _verticalSpeed;
        private float _currentSpeed;
        private Vector3 _stairsNormal;
        private PlayerEnvironmentState _playerEnvironmentState;
        private Vector3 _stairsHitNormal;
        private CapsuleCollider _capsuleCollider;

        private ReactiveProperty<PropertyType> _speed;
        private bool _isOnSlope;
        private Vector3 _slopeNormal;
        private float _slopeAngle;
        private readonly float _maxSlopeAngle = 45f; // 可行走的最大斜坡角度
        
        public bool IsGrounded => _playerEnvironmentState == PlayerEnvironmentState.OnGround;
        public PlayerEnvironmentState PlayerEnvironmentState => _playerEnvironmentState;
        public float GroundDistance => _groundDistance;
        
        [Header("Sync Settings")]
        [SerializeField] private float reconcileThreshold = 2f;
        [SerializeField] private float interpolationTime = 0.1f;

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, MirrorNetworkMessageHandler networkMessageHandler,
            FrameSyncManager frameSyncManager, UIManager uiManager)
        {
            _playerAnimationComponent = GetComponent<PlayerAnimationComponent>();
            _playerPropertyComponent = GetComponentInChildren<PlayerPropertyComponent>();
            _frameSyncManager = frameSyncManager;
            _messageHandler = networkMessageHandler;
            _uiManager = uiManager;
            _rotateCenter = transform.Find("RotateCenter");
            _checkStairsTransform = transform.Find("CheckStairs");
            _camera = Camera.main;
            _rigidbody = GetComponent<Rigidbody>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _speed = _playerPropertyComponent.GetProperty(PropertyTypeEnum.Speed);
            _speed.Subscribe(x => _targetSpeed = x.Value).AddTo(this);
            if (!isLocalPlayer)
                gameEventManager.Publish(new PlayerSpawnedEvent(_rotateCenter));
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            CheckGroundDistance();
            CheckPlayerState();
        }

        public void ExecuteInput(PlayerInputInfo input)
        {
            //_playerInput = input;
            //HandleAllInput();
        }
        
        private Vector3 _inputMovement;

        public void ExecutePlayerLocalInput(InputData input)
        {
            _playerPropertyComponent.HasMovementInput = input.playerInput.movement.magnitude > 0;
            _inputMovement = input.playerInput.movement;
            switch (input.command)
            {
                case AnimationState.Move:
                case AnimationState.Sprint:
                    HandleMove(_inputMovement);
                    break;
                case AnimationState.Jump:
                case AnimationState.SprintJump:
                    HandleJump();
                    break;
            }
            HandlePlayerRotation(input);
            HandleAnimation(input);
            SetAnimationParams(input);
            _pendingInputs.Enqueue(input);
        }

        public void ExecuteAnimationInput(InputData input)
        {
            HandleAnimation(input);
            SetAnimationParams(input);
        }

        public void ExecuteServerAction(InputData input)
        {
            if (_playerEnvironmentState != PlayerEnvironmentState.OnGround)
            {
                return;
            }
            _rigidbody.velocity = Vector3.zero;
            switch (input.command)
            {
                case AnimationState.Attack:
                    HandleAnimation(input);
                    break;
                case AnimationState.Roll:
                    HandleAnimation(input);
                    _rigidbody.AddForce(transform.forward.normalized * _jsonDataConfig.PlayerConfig.RollForce, ForceMode.Impulse);
                    break;
            }
        }
        
        private AnimationState _currentRequestAnimationState;
        public AnimationState CurrentRequestAnimationState => _currentRequestAnimationState;

        private void HandleAnimation(InputData inputData)
        {
            _currentRequestAnimationState = _playerAnimationComponent.ExecuteAnimationState(new PlayerInputCommand
            {
                isJumpRequested = inputData.playerInput.isJumpRequested,
                isRollRequested = inputData.playerInput.isRollRequested,
                isAttackRequested = inputData.playerInput.isAttackRequested,
                isSprinting = inputData.playerInput.isSprinting,
                movement = inputData.playerInput.movement,
            }, _playerEnvironmentState, _groundDistance);
        }

        public void HandlePlayerRotation(InputData inputData)
        {
            var canRotate = _playerEnvironmentState is not PlayerEnvironmentState.InAir;
            var isMoving = _playerAnimationComponent.IsMovingState();
            if (inputData.playerInput.movement.magnitude > 0.1f && canRotate && isMoving)
            {
                //前进方向转化为摄像机面对的方向
                var movementDirection = inputData.playerInput.movement.normalized;
                var targetRotation = Quaternion.LookRotation(movementDirection);
                targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _jsonDataConfig.PlayerConfig.RotateSpeed);
            }
        }

        private void CheckPlayerState()
        {
            PlayerEnvironmentState newEnvironmentState; // 默认保持当前状态

            // 检查楼梯状态
            if (CheckStairs(out _stairsNormal, out _stairsHitNormal))
            {
                newEnvironmentState = PlayerEnvironmentState.OnStairs;
            }
            // 如果不在楼梯上，检查是否在地面
            else if (_groundDistance <= groundMinDistance)
            {
                newEnvironmentState = PlayerEnvironmentState.OnGround;
            }
            // 既不在楼梯也不在地面，则在空中
            else
            {
                newEnvironmentState = PlayerEnvironmentState.InAir;
            }

            // 只有状态发生改变时才更新
            if (newEnvironmentState != _playerEnvironmentState)
            {
                _playerEnvironmentState = newEnvironmentState;
                _playerPropertyComponent.PlayerEnvironmentState = _playerEnvironmentState;

                // 如果新状态是楼梯状态，更新朝向
                if (newEnvironmentState == PlayerEnvironmentState.OnStairs)
                {
                    // 计算垂直于楼梯的方向（使用楼梯的法线）
                    var desiredForward = -_stairsHitNormal;
                    // 保持y轴垂直，只在水平面上旋转
                    desiredForward.y = 0;
                    desiredForward.Normalize();
    
                    // 立即更新玩家朝向
                    transform.rotation = Quaternion.LookRotation(desiredForward);
                }
            }
            _playerAnimationComponent.SetEnvironmentState(_playerEnvironmentState);
        }
        
        public void ReconcileState(ServerState state)
        {
            if (state.actionType != ActionType.Movement) return;
        
            // 检查位置差异
            if (Vector3.Distance(transform.position, state.position) > reconcileThreshold)
            {
                // 位置差异过大，进行回滚
                transform.position = state.position;
                _rigidbody.velocity = state.velocity;
            
                // 清除已确认的输入
                while (_pendingInputs.Count > 0 && 
                       _pendingInputs.Peek().sequence <= state.lastProcessedInput)
                {
                    _pendingInputs.Dequeue();
                }
            
                // 重新应用未确认的输入
                foreach (var input in _pendingInputs)
                {
                    HandleMovementAndJump(input);
                }
            }
        }

        public void HandleMove(Vector3 movement)
        {
            if (_playerAnimationComponent.IsPlayingSpecialAction)
            {
                return;
            }
            var hasMovementInput = movement.magnitude > 0f;
            if (_playerEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                _rigidbody.useGravity = false;
                if (hasMovementInput)
                {
                    var moveDirection = movement.normalized;
                    var targetVelocity = moveDirection * _currentSpeed;
                    targetVelocity += _stairsHitNormal.normalized * -2f;
                    _rigidbody.velocity = targetVelocity;
                }
                else
                {
                    _rigidbody.velocity = _stairsHitNormal.normalized * -2f;
                }
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                _rigidbody.useGravity = true;
                movement = movement.magnitude <= 0.1f ? movement : 
                    _camera.transform.TransformDirection(movement);
                movement.y = 0f;
                // 计算目标位置和速度
                var targetPosition = transform.position + movement.normalized * (_currentSpeed * Time.fixedDeltaTime);
                var targetVelocity = (targetPosition - transform.position) / Time.fixedDeltaTime;
                // 保持垂直速度
                targetVelocity.y = _rigidbody.velocity.y;
                if (_isOnSlope)
                {
                    // 在斜坡上时，调整移动方向
                    var slopeMovementDirection = Vector3.ProjectOnPlane(movement, _slopeNormal).normalized;
                    targetVelocity = slopeMovementDirection * _currentSpeed;
                    targetVelocity.y = _rigidbody.velocity.y;

                    if (hasMovementInput)
                    {
                        _rigidbody.AddForce(-_slopeNormal * 20f, ForceMode.Force);
                    }
                }
                // 应用速度
                _rigidbody.velocity = targetVelocity;
            }
        }

        private void HandleJump()
        {
            if (_playerAnimationComponent.IsPlayingSpecialAction)
            {
                return;
            }

            if (_playerEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                // 清除当前垂直速度
                var vel = _rigidbody.velocity;
                vel.y = 0f;
                _rigidbody.velocity = vel;
            
                // 应用跳跃力
                var jumpDirection = _isOnSlope ? Vector3.Lerp(Vector3.up, _slopeNormal, 0.5f) : Vector3.up;
                _rigidbody.AddForce(jumpDirection * _jsonDataConfig.PlayerConfig.JumpSpeed, ForceMode.Impulse);
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                _rigidbody.velocity = Vector3.zero;
                _rigidbody.MovePosition(transform.position + _stairsHitNormal.normalized);
                _rigidbody.AddForce(_stairsHitNormal.normalized * _jsonDataConfig.PlayerConfig.JumpSpeed / 5f, ForceMode.Impulse);
            }
        }

        private void HandleMovementAndJump(InputData inputData)
        {
            var inputMovement = inputData.playerInput.movement;
            var hasMovementInput = inputMovement.magnitude > 0.1f;
            if (_playerEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                _rigidbody.useGravity = false;
                if (_playerAnimationComponent.IsPlayingSpecialAction)
                {
                    return;
                }
                if (_currentRequestAnimationState is AnimationState.Jump or AnimationState.SprintJump)
                {
                    _rigidbody.velocity = Vector3.zero;
                    _rigidbody.MovePosition(transform.position + _stairsHitNormal.normalized);
                    _rigidbody.AddForce(_stairsHitNormal.normalized * _jsonDataConfig.PlayerConfig.JumpSpeed / 5f, ForceMode.Impulse);
                    return;
                }
                var movement = inputMovement.z * -_stairsNormal.normalized + transform.right * inputMovement.x;
                if (hasMovementInput)
                {
                    var moveDirection = movement.normalized;
                    var targetVelocity = moveDirection * _currentSpeed;
                    targetVelocity += _stairsHitNormal.normalized * -2f;
                    _rigidbody.velocity = targetVelocity;
                }
                else
                {
                    _rigidbody.velocity = _stairsHitNormal.normalized * -2f;
                }
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                if (_playerAnimationComponent.IsPlayingSpecialAction)
                {
                    if (_currentRequestAnimationState is AnimationState.Roll)
                    {
                        _rigidbody.velocity = Vector3.zero;
                        _rigidbody.AddForce(transform.forward.normalized * _jsonDataConfig.PlayerConfig.RollForce, ForceMode.Impulse);
                    }
                    else if (_currentRequestAnimationState is AnimationState.Attack)
                    {
                        _rigidbody.velocity = Vector3.zero;
                    }
                    return;
                }
                // 处理跳跃
                if (_currentRequestAnimationState is AnimationState.Jump or AnimationState.SprintJump)
                {
                    // 清除当前垂直速度
                    var vel = _rigidbody.velocity;
                    vel.y = 0f;
                    _rigidbody.velocity = vel;
            
                    // 应用跳跃力
                    var jumpDirection = _isOnSlope ? Vector3.Lerp(Vector3.up, _slopeNormal, 0.5f) : Vector3.up;
                    _rigidbody.AddForce(jumpDirection * _jsonDataConfig.PlayerConfig.JumpSpeed, ForceMode.Impulse);
                }

                inputMovement = inputMovement.magnitude <= 0.1f ? inputMovement : 
                    _camera.transform.TransformDirection(inputMovement);
                inputMovement.y = 0f;
                // 计算目标位置和速度
                var targetPosition = transform.position + inputMovement.normalized * (_currentSpeed * Time.fixedDeltaTime);
                var targetVelocity = (targetPosition - transform.position) / Time.fixedDeltaTime;
                // 保持垂直速度
                targetVelocity.y = _rigidbody.velocity.y;
                if (_isOnSlope)
                {
                    // 在斜坡上时，调整移动方向
                    var slopeMovementDirection = Vector3.ProjectOnPlane(inputMovement, _slopeNormal).normalized;
                    targetVelocity = slopeMovementDirection * _currentSpeed;
                    targetVelocity.y = _rigidbody.velocity.y;

                    // 添加额外的向下力以保持贴合斜面
                    if (hasMovementInput)
                    {
                        _rigidbody.AddForce(-_slopeNormal * 20f, ForceMode.Force);
                    }
                }
                // 应用速度
                _rigidbody.velocity = targetVelocity;
            }
        }

        private void SetAnimationParams(InputData inputData)
        {
            _playerAnimationComponent.SetMoveSpeed(_currentSpeed);
            _playerAnimationComponent.SetInputMagnitude(inputData.playerInput.movement.magnitude);
            _playerAnimationComponent.SetIsSprinting(inputData.playerInput.isSprinting);
            _playerAnimationComponent.SetFallSpeed(_verticalSpeed);
        }
        
        private float _groundDistance;
        [SerializeField]
        private float groundMinDistance = 0.25f;
        [SerializeField]
        private float groundMaxDistance = 0.5f;

        private void CheckGroundDistance()
        {
            if (_capsuleCollider)
            {
                var radius = _capsuleCollider.radius * 0.9f;
                var dist = 10f;
                _isOnSlope = false;

                // 向下的射线检测
                var ray2 = new Ray(transform.position + new Vector3(0, _capsuleCollider.height / 2, 0), Vector3.down);
                if (Physics.Raycast(ray2, out var groundHit, _capsuleCollider.height / 2 + dist,
                        _jsonDataConfig.GameConfig.groundSceneLayer) && !groundHit.collider.isTrigger)
                {
                    dist = transform.position.y - groundHit.point.y;

                    // 检查斜坡
                    _slopeNormal = groundHit.normal;
                    _slopeAngle = Vector3.Angle(Vector3.up, _slopeNormal);
                    _isOnSlope = _slopeAngle != 0f && _slopeAngle <= _maxSlopeAngle;
                }

                // 球形检测
                if (dist >= groundMinDistance)
                {
                    var forwardOffset = _inputMovement.magnitude > 0f ? (_inputMovement.normalized * (radius * 0.5f)) : Vector3.zero;
                    var pos = transform.position + Vector3.up * (_capsuleCollider.radius) + forwardOffset;
                    var ray = new Ray(pos, -Vector3.up);

                    if (Physics.SphereCast(ray, radius, out groundHit, _capsuleCollider.radius + groundMaxDistance,
                            _jsonDataConfig.GameConfig.groundSceneLayer) && !groundHit.collider.isTrigger)
                    {
                        Physics.Linecast(groundHit.point + (Vector3.up * 0.1f), groundHit.point + Vector3.down * 0.15f,
                            out groundHit, _jsonDataConfig.GameConfig.groundSceneLayer);
                        var newDist = transform.position.y - groundHit.point.y;
                        if (dist > newDist)
                        {
                            dist = newDist;
                            // 更新斜坡信息
                            _slopeNormal = groundHit.normal;
                            _slopeAngle = Vector3.Angle(Vector3.up, _slopeNormal);
                            _isOnSlope = _slopeAngle != 0f && _slopeAngle <= _maxSlopeAngle;
                        }
                    }
                }

                _groundDistance = Mathf.Clamp((float)Math.Round(dist, 2), 0f, groundMaxDistance);
                _playerAnimationComponent.SetGroundDistance(_groundDistance);

                if (_playerEnvironmentState == PlayerEnvironmentState.InAir)
                {
                    _rigidbody.useGravity = true;
                    var inputSmooth = Vector3.zero;
                    inputSmooth = Vector3.Lerp(inputSmooth, _inputMovement, 6f * Time.fixedDeltaTime);
        
                    if (_inputMovement.magnitude > 0f)
                    {
                        var airMovement = _camera.transform.TransformDirection(inputSmooth).normalized * _currentSpeed;
                        airMovement.y = _rigidbody.velocity.y;
                        _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, airMovement, Time.fixedDeltaTime * 2f);
                    }

                    // 应用额外重力
                    _rigidbody.AddForce(Physics.gravity * Time.fixedDeltaTime, ForceMode.VelocityChange);
                    _verticalSpeed = _rigidbody.velocity.y;
                }
                else
                {
                    _verticalSpeed = 0f;
                }
            }
        }

        private bool CheckStairs(out Vector3 direction, out Vector3 hitNormal)
        {
            direction = Vector3.zero;
            hitNormal = Vector3.zero;

            if (Physics.Raycast(_checkStairsTransform.position, _checkStairsTransform.forward, out var hit, _jsonDataConfig.PlayerConfig.StairsCheckDistance, _jsonDataConfig.GameConfig.stairSceneLayer))
            {
                hitNormal = hit.normal;
                direction = Vector3.Cross(hit.normal, _checkStairsTransform.right).normalized;
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            _uiManager.CloseUI(UIType.PlayerPropertiesOverlay);
        }
    }
}