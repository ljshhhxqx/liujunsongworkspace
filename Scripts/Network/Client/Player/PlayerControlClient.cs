using System;
using HotUpdate.Scripts.Config;
using Network.Client;
using Network.Data;
using Tool.Coroutine;
using Tool.GameEvent;
using UI.UIBase;
using UniRx;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerControlClient : ClientBase
    {
        private PlayerPropertyComponent _playerPropertyComponent;
        private PlayerAnimationComponent _playerAnimationComponent;
        
        private float _speedSmoothTime = 0.1f; // 速度平滑时间
        //决定摄像机的旋转中心
        private Transform _rotateCenter;
        private Transform _checkGroundTransform;
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
        private float attackTimer = 0f; // 攻击计时器
        private float attackCooldown = 0.5f; // 攻击间隔时间
        private int _currentAttackIndex = 0; // 当前攻击动画索引
        private CapsuleCollider _capsuleCollider;
        private readonly int maxAttackIndex = 3; // 最大攻击动画索引（从0开始）

        private ReactiveProperty<PropertyType> _speed;

        protected override void InitCallback()
        {
            _playerAnimationComponent = GetComponent<PlayerAnimationComponent>();
            _playerPropertyComponent = GetComponentInChildren<PlayerPropertyComponent>();
            _rotateCenter = transform.Find("RotateCenter");
            _checkGroundTransform = transform.Find("CheckGround");
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
            HandleMoveInput();
            HandleRotation();
            HandleAttack();
            HandleRoll();
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            CheckPlayerState();
            HandleMovement();
            SyncAnimation();
        }

        private void HandleAttack()
        {
            attackTimer -= Time.deltaTime; // 更新攻击计时器

            if (Input.GetButtonDown("Fire1") && _playerEnvironmentState != PlayerEnvironmentState.InAir && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Attack))
            {
                _playerAnimationComponent.SetAttack(0);
                _playerPropertyComponent.DoAnimation(AnimationState.Attack);
            }
        }

        private void HandleRoll()
        {
            if (Input.GetButtonDown("Roll") && _playerEnvironmentState != PlayerEnvironmentState.InAir && !_isRollRequested && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Roll))
            {
                _isRollRequested = true;
            }
        }

        private void HandleMoveInput()
        {
#if UNITY_ANDROID || UNITY_IOS
#else
            // _inputMovement = new Vector3(VirtualJoystick.Horizontal, 0f, VirtualJoystick.Vertical);
            // _hasMovementInput = Mathf.Abs(_inputMovement.x) > 0 || Mathf.Abs(_inputMovement.z) > 0;
            //
            // _playerPropertyComponent.HasMovementInput = _hasMovementInput;
            //
            // _isSprinting = _hasMovementInput && VirtualButton.IsPressed("Running") && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Sprint);
            // if (VirtualButton.IsPressed("Jump") && !_isJumpRequested && _playerState != PlayerState.InAir && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Jump))
            // {
            //     _isJumpRequested = true;
            //     _rigidbody.velocity = Vector3.zero;
            // }
            _inputMovement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            _hasMovementInput = Mathf.Abs(_inputMovement.x) > 0 || Mathf.Abs(_inputMovement.z) > 0;

            _playerPropertyComponent.HasMovementInput = _hasMovementInput;
            
            _isSprinting = _hasMovementInput && Input.GetButton("Running") && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Sprint);
            if (Input.GetButtonDown("Jump") && _playerEnvironmentState != PlayerEnvironmentState.InAir && !_isJumpRequested && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Jump))
            {
                _isJumpRequested = true;
                _rigidbody.velocity = Vector3.zero;
            }
#endif
            _playerAnimationComponent.SetInputMagnitude(_inputMovement.magnitude);
        }
        
        private void HandleRotation()   
        {
            if (_inputMovement.magnitude > 0.1f && _groundDistance <= _groundMinDistance)
            {
                //前进方向转化为摄像机面对的方向
                var targetRotationAngle = Quaternion.LookRotation(_movement);
                //锁定玩家旋转轴
                var targetRotation = Quaternion.Euler(0f, targetRotationAngle.eulerAngles.y, 0f);
                var rotation = transform.rotation;
                rotation = Quaternion.Slerp(rotation, targetRotation, Time.deltaTime * _playerDataConfig.PlayerConfigData.RotateSpeed);
                transform.rotation = rotation;
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
            else if (_groundDistance <= _groundMinDistance)
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
        
        private void HandleMovement()
        {
            if (_playerEnvironmentState == PlayerEnvironmentState.OnStairs)
            {
                _rigidbody.useGravity = false;
                _rigidbody.velocity = Vector3.zero;
                _movement = _inputMovement.z * -_stairsNormal.normalized + transform.right * _inputMovement.x;
                if (_isJumpRequested)
                {
                    _isJumpRequested = false;
                    _playerPropertyComponent.DoAnimation(AnimationState.Jump);
                    _rigidbody.AddForce(_stairsHitNormal.normalized * _playerDataConfig.PlayerConfigData.StairsJumpSpeed, ForceMode.Impulse);
                }
                else
                {
                    _playerPropertyComponent.DoAnimation(_hasMovementInput ? (_isSprinting ? AnimationState.Sprint : AnimationState.Move) :AnimationState.Idle);
                }
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.OnGround)
            {
                _movement = _inputMovement.magnitude <= 0.1f ? _inputMovement : _camera.transform.TransformDirection(_inputMovement);
                _movement.y = _inputMovement.magnitude <= 0.1f ? _movement.y : 0f;

                if (_isJumpRequested)
                {
                    _isJumpRequested = false;
                    _playerPropertyComponent.DoAnimation(_isSprinting ? AnimationState.SprintJump : AnimationState.Jump);
                    _rigidbody.AddForce(Vector3.up * _playerDataConfig.PlayerConfigData.JumpSpeed, ForceMode.Impulse);
                    
                   _playerAnimationComponent.SetJump(_isSprinting);
                }
                else
                {
                    _playerPropertyComponent.DoAnimation(_hasMovementInput ? (_isSprinting ? AnimationState.Sprint : AnimationState.Move) : AnimationState.Idle);
                }
                
                if (_isRollRequested)
                {
                    _isRollRequested = false;
                    
                    _playerPropertyComponent.DoAnimation(AnimationState.Roll);
                    _playerAnimationComponent.SetRoll();
                    _rigidbody.AddForce(transform.forward * _playerDataConfig.PlayerConfigData.RollForce, ForceMode.Impulse);
                }
                _rigidbody.velocity = new Vector3(_rigidbody.velocity.x, 0f, _rigidbody.velocity.z);
                _playerAnimationComponent.SetMoveSpeed(_currentSpeed);
            }
            else if (_playerEnvironmentState == PlayerEnvironmentState.InAir)
            {
                if (transform.position.y < _gameDataConfig.GameConfigData.SafeHorizontalOffsetY)
                {
                    _rigidbody.velocity = Vector3.zero;
                    transform.position = _gameDataConfig.GameConfigData.SafePosition;
                    return;
                }

                _verticalSpeed = _rigidbody.velocity.y;
                _rigidbody.useGravity = true;
                // 如果在空中，只应用水平移动，不改变方向
                _inputMovement = transform.TransformDirection(_inputMovement);
                _inputMovement *= _currentSpeed;
                _playerAnimationComponent.SetMoveSpeed(_currentSpeed);
                _hasMovementInput = Mathf.Abs(_inputMovement.x) > 0 || Mathf.Abs(_inputMovement.z) > 0;
                _velocity.y += Physics.gravity.y * Time.fixedDeltaTime;
                // 保持水平方向的动量
                _movement.x = _inputMovement.x;
                _movement.z = _inputMovement.z;
                //_playerAnimationComponent.SetMoveSpeed(0);
            }
            CheckGroundDistance();
            _playerAnimationComponent.SetGroundDistance(_groundDistance);
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _movement = _movement.normalized * (_currentSpeed * Time.fixedDeltaTime);
            if (Physics.Raycast(transform.position, _movement.normalized, _movement.magnitude + 0.1f, _gameDataConfig.GameConfigData.GroundSceneLayer))
            {
                _movement = Vector3.zero;
            }
            _rigidbody.MovePosition(_movement + transform.position);
        }
        
        private Vector3 _enterBuildingPosition;
        private float _insideBuildingTimer;
        private bool _isInsideBuilding;

        // private void OnCollisionEnter(Collision other)
        // {
        //     if (other.gameObject.CompareTag("Building")) // 假设建筑物的层为 "Building"
        //     {
        //         if (!_isInsideBuilding)
        //         {
        //             _isInsideBuilding = true;
        //             _enterBuildingPosition = transform.position; // 记录进入建筑时的位置
        //             _insideBuildingTimer = 0f; // 重置计时器
        //         }
        //     }
        // }
        //
        // private void OnCollisionStay(Collision other)
        // {
        //     if (other.gameObject.CompareTag("Building") && _isInsideBuilding)
        //     {
        //         _insideBuildingTimer += Time.deltaTime; // 计时
        //         if (_insideBuildingTimer >= 3f && _isInsideBuilding)
        //         {
        //             _insideBuildingTimer = 0f; // 重置计时器
        //             _isInsideBuilding = false; // 重置状态
        //             // 传送回进入建筑时的位置
        //             transform.position = _enterBuildingPosition;
        //         }
        //     }
        // }
        //
        // private void OnCollisionExit(Collision other)
        // {
        //     if (other.gameObject.CompareTag("Building"))
        //     {
        //         _isInsideBuilding = false; // 玩家离开建筑，重置状态
        //         _insideBuildingTimer = 0f; // 重置计时器
        //     }
        // }

        private void SyncAnimation()
        {
            _playerAnimationComponent.SetFallSpeed(_verticalSpeed);
        }
        
        private float _groundDistance;
        [SerializeField]
        private float _groundMinDistance = 0.25f;
        [SerializeField]
        private float _groundMaxDistance = 0.5f;

        private void CheckGroundDistance()
        {
            if (_capsuleCollider != null)
            {
                var radius = _capsuleCollider.radius * 0.9f;
                var dist = 10f;
                Ray ray2 = new Ray(transform.position + new Vector3(0, _capsuleCollider.height / 2, 0), Vector3.down);
                // raycast for check the ground distance
                if (Physics.Raycast(ray2, out var groundHit, (_capsuleCollider.height / 2) + dist, _gameDataConfig.GameConfigData.GroundSceneLayer) && !groundHit.collider.isTrigger)
                    dist = transform.position.y - groundHit.point.y;
                // sphere cast around the base of the capsule to check the ground distance
                if (dist >= _groundMinDistance)
                {
                    Vector3 pos = transform.position + Vector3.up * (_capsuleCollider.radius);
                    Ray ray = new Ray(pos, -Vector3.up);
                    if (Physics.SphereCast(ray, radius, out groundHit, _capsuleCollider.radius + _groundMaxDistance, _gameDataConfig.GameConfigData.GroundSceneLayer) && !groundHit.collider.isTrigger)
                    {
                        Physics.Linecast(groundHit.point + (Vector3.up * 0.1f), groundHit.point + Vector3.down * 0.15f, out groundHit, _gameDataConfig.GameConfigData.GroundSceneLayer);
                        float newDist = transform.position.y - groundHit.point.y;
                        if (dist > newDist) dist = newDist;
                    }
                }
                _groundDistance = (float)System.Math.Round(dist, 2);
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
            if (Physics.Raycast(_checkStairsTransform.position, -_checkStairsTransform.up, out hit, _playerDataConfig.PlayerConfigData.StairsCheckDistance, _gameDataConfig.GameConfigData.StairSceneLayer))
            {
                hitNormal = hit.normal;
                direction = Vector3.Cross(hit.normal, _checkStairsTransform.right).normalized;
                return true;
            }
            return false;
        }

        private void OnDestroy()
        {
            uiManager.CloseUI(UIType.PlayerPropertiesOverlay);
        }
    }
}