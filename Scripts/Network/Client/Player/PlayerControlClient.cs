using HotUpdate.Scripts.Config;
using Mirror;
using Network.Client;
using Network.Data;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.AnimationState;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerControlClient : ClientBase
    {
        private PlayerDataComponent _playerDataComponent;
        private PlayerPropertyComponent _playerPropertyComponent;
        
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
        private const float StairsJumpMultiplier = 1.5f; // 新增：楼梯跳跃倍数
        private float _verticalSpeed;
        private float _currentSpeed;
        private bool _isJumpRequested;
        private Vector3 _stairsNormal;
        private Vector3 _inputMovement;
        private GameDataConfig _gameDataConfig;
        private PlayerState _playerState;
        private Vector3 _stairsHitNormal;

        protected override void InitCallback()
        {
            _playerPropertyComponent = GetComponent<PlayerPropertyComponent>();
            _rotateCenter = transform.Find("RotateCenter");
            _checkGroundTransform = transform.Find("CheckGround");
            _checkStairsTransform = transform.Find("CheckStairs");
            _camera = Camera.main;
            _rigidbody = GetComponent<Rigidbody>();
            _playerDataConfig = configProvider.GetConfig<PlayerDataConfig>();
            _gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            
            gameEventManager.Publish(new PlayerSpawnedEvent(_rotateCenter));
            repeatedTask.StartRepeatingTask(SyncAnimation, _gameDataConfig.GameConfigData.SyncTime);
        }

        private void Update()
        {
            if (!isLocalPlayer) return;
            HandleInput();
            HandleRotation();
        }

        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            CheckPlayerState();
            HandleMovement();
        }

        private void HandleInput()
        {
            _inputMovement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            _hasMovementInput = Mathf.Abs(_inputMovement.x) > 0 || Mathf.Abs(_inputMovement.z) > 0;
            _isSprinting = _hasMovementInput && Input.GetButton("Running") && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Sprint);
            if (Input.GetButtonDown("Jump") && !_isJumpRequested && _playerState != PlayerState.InAir && _playerPropertyComponent.StrengthCanDoAnimation(AnimationState.Jump))
            {
                _isJumpRequested = true;
                _rigidbody.velocity = Vector3.zero;
            }

        }
        
        private void HandleRotation()   
        {
            if (_inputMovement.magnitude > 0.1f && CheckGrounded())
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
            if (CheckStairs(out _stairsNormal, out _stairsHitNormal))
            {
                _playerState = PlayerState.OnStairs;
                return;
            }

            _playerState = CheckGrounded() ? PlayerState.OnGround : PlayerState.InAir;
        }

        private void HandleMovement()
        {
            if (_playerState == PlayerState.OnStairs)
            {
                //Debug.Log("On stairs");
                _rigidbody.useGravity = false;
                _movement = _inputMovement.z * -_stairsNormal.normalized + transform.right * _inputMovement.x;
                _targetSpeed = _isSprinting ? _playerDataConfig.PlayerConfigData.MoveSpeed : _playerDataConfig.PlayerConfigData.OnStairsSpeed;
                if (_isJumpRequested)
                {
                    _playerPropertyComponent.DoAnimation(AnimationState.Jump);
                    _isJumpRequested = false;
                    //Debug.Log("Jump on stairs");
                    _rigidbody.AddForce(_stairsHitNormal.normalized * _playerDataConfig.PlayerConfigData.StairsJumpSpeed, ForceMode.Impulse);
                }
            }
            else if (_playerState == PlayerState.OnGround)
            {
                _rigidbody.velocity = Vector3.zero;
                //Debug.Log("On ground");
                _movement = _inputMovement.magnitude <= 0.1f ? _inputMovement : _camera.transform.TransformDirection(_inputMovement);
                _movement.y = _inputMovement.magnitude <= 0.1f ? _movement.y : 0f;
                _targetSpeed = _isSprinting ? _playerDataConfig.PlayerConfigData.RunSpeed : _hasMovementInput ? _playerDataConfig.PlayerConfigData.MoveSpeed : 0;

                if (_isJumpRequested)
                {
                    _playerPropertyComponent.DoAnimation(AnimationState.Jump);
                    _isJumpRequested = false;
                    _rigidbody.AddForce(Vector3.up * _playerDataConfig.PlayerConfigData.JumpSpeed, ForceMode.Impulse);
                    
                    gameEventManager.Publish(new PlayerJumpEvent());
                }
            }
            else
            {
                _rigidbody.useGravity = true;
                // 如果在空中，只应用水平移动，不改变方向
                //Debug.Log("In air");
                _inputMovement = transform.TransformDirection(_inputMovement);
                _inputMovement *= _playerDataConfig.PlayerConfigData.MoveSpeed;
                _hasMovementInput = Mathf.Abs(_inputMovement.x) > 0 || Mathf.Abs(_inputMovement.z) > 0;
                _velocity.y += Physics.gravity.y * Time.fixedDeltaTime;
                // 保持水平方向的动量
                _movement.x = _inputMovement.x;
                _movement.z = _inputMovement.z;
            }
            
            var position = transform.position;
            
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _movement = _movement.normalized * (_currentSpeed * Time.fixedDeltaTime);
            _rigidbody.MovePosition(_movement + position);
            _playerPropertyComponent.DoAnimation(_hasMovementInput ? (_isSprinting ? AnimationState.Sprint : AnimationState.Move) :AnimationState.Idle);
        }

        private void SyncAnimation()
        {
            gameEventManager.Publish(new PlayerSpeedChangeEvent(_currentSpeed));
            gameEventManager.Publish(new PlayerVerticalSpeedChangeEvent(_velocity.y));
        }
        
        private bool CheckGrounded()
        {
            return Physics.Raycast(_checkGroundTransform.position, Vector3.down, out var hit, _playerDataConfig.PlayerConfigData.GroundCheckRadius, _gameDataConfig.GameConfigData.GroundSceneLayer);        
        }

        private bool CheckStairs(out Vector3 direction, out Vector3 hitNormal)
        {
            direction = Vector3.zero;
            hitNormal = Vector3.zero;

            if (Physics.Raycast(_checkStairsTransform.position, _checkStairsTransform.forward, out var hit1, _playerDataConfig.PlayerConfigData.StairsCheckDistance, _gameDataConfig.GameConfigData.StairSceneLayer))
            {
                hitNormal = hit1.normal;
                direction = Vector3.Cross(hit1.normal, _checkStairsTransform.right).normalized;
                return true;
            }
            return false;
        }
    }
}