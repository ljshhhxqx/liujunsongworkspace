using System;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.Server.Sync;
using Mirror;
using Network.NetworkMes;
using Tool.GameEvent;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;

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
        
        private float _speedSmoothTime = 0.1f; // 速度平滑时间
        //决定摄像机的旋转中心
        private Transform _rotateCenter;
        private Transform _checkStairsTransform;
        private PlayerDataConfig _playerDataConfig;
        private Rigidbody _rigidbody;
        //用来决定玩家移动方向
        private Vector3 _movement;
        //玩家的摄像机
        private Camera _camera;
        private Vector3 _velocity;
        private bool _isSprinting;
        private bool _isMoving;
        private bool _isJumping;
        private bool _hasMovementInput;
        private float _speedSmoothVelocity;
        private float _targetSpeed;
        private float _verticalSpeed;
        private float _currentSpeed;
        private bool _isJumpRequested;
        private bool _isRollRequested;
        private bool _isAttackRequested;
        private Vector3 _stairsNormal;
        private Vector3 _inputMovement;
        private GameDataConfig _gameDataConfig;
        private PlayerEnvironmentState _playerEnvironmentState;
        private Vector3 _stairsHitNormal;
        private CapsuleCollider _capsuleCollider;

        private ReactiveProperty<PropertyType> _speed;// 添加物理材质字段
        private bool _isOnSlope;
        private Vector3 _slopeNormal;
        private float _slopeAngle;
        private readonly float _maxSlopeAngle = 45f; // 可行走的最大斜坡角度

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
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            _gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            if (!isServer)
            {
                gameEventManager.Publish(new PlayerSpawnedEvent(_rotateCenter));
            }
            _speed = _playerPropertyComponent.GetProperty(PropertyTypeEnum.Speed);
            _speed.Subscribe(x => _targetSpeed = x.Value).AddTo(this);
        }


        private void Update()
        {
            if (!isLocalPlayer) return;
            SendInput();
            HandleRotation();
        }

        private void SendInput()
        {
            _messageHandler.SendToServer(new MirrorPlayerInputMessage(new PlayerInputInfo
            {
                frame = _frameSyncManager.GetCurrentFrame(),
                playerId = netId,
                movement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical")),
                isJumpRequested = Input.GetButtonDown("Jump"),
                isRollRequested = Input.GetButtonDown("Roll"),
                isAttackRequested = Input.GetButtonDown("Fire1"),
                isSprinting = Input.GetButton("Running")
            }));
        }

        public void ExecuteInput(PlayerInputInfo input)
        {
            _playerInput = input;
            _inputMovement = _playerInput.movement;
            _hasMovementInput = _playerInput.movement.magnitude > 0;
            _isSprinting = _playerInput.isSprinting;
            _isJumpRequested = _playerInput.isJumpRequested;
            _isRollRequested = _playerInput.isRollRequested;
            _isAttackRequested = _playerInput.isAttackRequested;
            HandleAllInput();
        }

        private void HandleAllInput()
        {
            CheckGroundDistance();
            CheckPlayerState();
            HandleAnimation();
            HandleMovementAndJump();
            SyncAnimation();
        }
        
        private AnimationState _currentRequestAnimationState;

        private void HandleAnimation()
        {
            _currentRequestAnimationState = _playerAnimationComponent.ExecuteAnimationState(_playerInput, _playerEnvironmentState, _groundDistance);
        }
        
        private void HandleRotation()   
        {
            var canRotate = _playerEnvironmentState is not PlayerEnvironmentState.InAir;
            var isMoving = _playerAnimationComponent.IsMovingState();
            if (_inputMovement.magnitude > 0.1f && _groundDistance <= groundMinDistance && canRotate && isMoving)
            {
                //前进方向转化为摄像机面对的方向
                var movementDirection = _movement.normalized;
                var targetRotation = Quaternion.LookRotation(movementDirection);
                targetRotation = Quaternion.Euler(0f, targetRotation.eulerAngles.y, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * _playerDataConfig.PlayerConfigData.RotateSpeed);
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
        
        private void HandleMovementAndJump()
        {
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _playerAnimationComponent.SetGroundDistance(_groundDistance);
            _playerPropertyComponent.HasMovementInput = _hasMovementInput;
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
                    _rigidbody.AddForce(_stairsHitNormal.normalized * _playerDataConfig.PlayerConfigData.JumpSpeed / 5f, ForceMode.Impulse);
                    return;
                }
                _movement = _inputMovement.z * -_stairsNormal.normalized + transform.right * _inputMovement.x;
                if (_hasMovementInput)
                {
                    var moveDirection = _movement.normalized;
                    var targetVelocity = moveDirection * _currentSpeed;
                    targetVelocity += _stairsHitNormal.normalized * -2f;
                    _rigidbody.velocity = targetVelocity;
                    _playerAnimationComponent.SetMoveSpeed(_currentSpeed);
                }
                else
                {
                    _rigidbody.velocity = _stairsHitNormal.normalized * -2f;
                    _playerAnimationComponent.SetMoveSpeed(0);
                }
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                if (_playerAnimationComponent.IsPlayingSpecialAction)
                {
                    if (_currentRequestAnimationState is AnimationState.Roll)
                    {
                        _rigidbody.velocity = Vector3.zero;
                        _rigidbody.AddForce(transform.forward.normalized * _playerDataConfig.PlayerConfigData.RollForce, ForceMode.Impulse);
                    }
                    else if (_currentRequestAnimationState is AnimationState.Attack)
                    {
                        _rigidbody.velocity = Vector3.zero;
                    }
                    return;
                }
                _movement = _inputMovement.magnitude <= 0.1f ? _inputMovement : 
                    _camera.transform.TransformDirection(_inputMovement);
                _movement.y = 0f;
                // 计算目标位置和速度
                var targetPosition = transform.position + _movement.normalized * (_currentSpeed * Time.fixedDeltaTime);
                var targetVelocity = (targetPosition - transform.position) / Time.fixedDeltaTime;
                // 保持垂直速度
                targetVelocity.y = _rigidbody.velocity.y;
                // 处理跳跃
                if (_currentRequestAnimationState is AnimationState.Jump or AnimationState.SprintJump)
                {
                    // 清除当前垂直速度
                    var vel = _rigidbody.velocity;
                    vel.y = 0f;
                    _rigidbody.velocity = vel;
            
                    // 应用跳跃力
                    var jumpDirection = _isOnSlope ? Vector3.Lerp(Vector3.up, _slopeNormal, 0.5f) : Vector3.up;
                    _rigidbody.AddForce(jumpDirection * _playerDataConfig.PlayerConfigData.JumpSpeed, ForceMode.Impulse);
                }

                if (_isOnSlope)
                {
                    // 在斜坡上时，调整移动方向
                    var slopeMovementDirection = Vector3.ProjectOnPlane(_movement, _slopeNormal).normalized;
                    targetVelocity = slopeMovementDirection * _currentSpeed;
                    targetVelocity.y = _rigidbody.velocity.y;

                    // 添加额外的向下力以保持贴合斜面
                    if (_hasMovementInput)
                    {
                        _rigidbody.AddForce(-_slopeNormal * 20f, ForceMode.Force);
                    }
                }
                // 应用速度
                _rigidbody.velocity = targetVelocity;
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.InAir)
            {
                _rigidbody.useGravity = true;
                var inputSmooth = Vector3.zero;
                inputSmooth = Vector3.Lerp(inputSmooth, _inputMovement, 6f * Time.fixedDeltaTime);
        
                if (_hasMovementInput)
                {
                    var airMovement = _camera.transform.TransformDirection(inputSmooth).normalized * _currentSpeed;
                    airMovement.y = _rigidbody.velocity.y;
                    _rigidbody.velocity = Vector3.Lerp(_rigidbody.velocity, airMovement, Time.fixedDeltaTime * 2f);
                }

                // 应用额外重力
                _rigidbody.AddForce(Physics.gravity * Time.fixedDeltaTime, ForceMode.VelocityChange);
                _playerAnimationComponent.SetMoveSpeed(0);
            }
            _verticalSpeed = _rigidbody.velocity.y;
        }

        private void SyncAnimation()
        {
            _playerAnimationComponent.SetMoveSpeed(_currentSpeed);
            _playerAnimationComponent.SetInputMagnitude(_inputMovement.magnitude);
            _playerAnimationComponent.SetIsSprinting(_isSprinting);
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
                        _gameDataConfig.GameConfigData.GroundSceneLayer) && !groundHit.collider.isTrigger)
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
                    var forwardOffset = _hasMovementInput ? (_movement.normalized * (radius * 0.5f)) : Vector3.zero;
                    var pos = transform.position + Vector3.up * (_capsuleCollider.radius) + forwardOffset;
                    var ray = new Ray(pos, -Vector3.up);

                    if (Physics.SphereCast(ray, radius, out groundHit, _capsuleCollider.radius + groundMaxDistance,
                            _gameDataConfig.GameConfigData.GroundSceneLayer) && !groundHit.collider.isTrigger)
                    {
                        Physics.Linecast(groundHit.point + (Vector3.up * 0.1f), groundHit.point + Vector3.down * 0.15f,
                            out groundHit, _gameDataConfig.GameConfigData.GroundSceneLayer);
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
            }
        }

        private bool CheckStairs(out Vector3 direction, out Vector3 hitNormal)
        {
            direction = Vector3.zero;
            hitNormal = Vector3.zero;

            if (Physics.Raycast(_checkStairsTransform.position, _checkStairsTransform.forward, out var hit, _playerDataConfig.PlayerConfigData.StairsCheckDistance, _gameDataConfig.GameConfigData.StairSceneLayer))
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