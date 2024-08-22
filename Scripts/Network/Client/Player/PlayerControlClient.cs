using Mirror;
using Network.Client;
using Network.Data;
using Tool.GameEvent;
using Tool.Message;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerControlClient : ClientBase
    {
        private PlayerDataComponent _playerDataComponent;
        
        private const float SpeedSmoothTime = 0.1f; // 速度平滑时间
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
        private bool _isOnStairs;
        private Vector3 _stairsNormal;
        private Vector3 _inputMovement;
        private GameDataConfig _gameDataConfig;

        protected override void InitCallback()
        {
            var playerDataComponent = GetComponent<PlayerDataComponent>();
            _playerDataComponent = playerDataComponent ? playerDataComponent : gameObject.AddComponent<PlayerDataComponent>();
            
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

        [Client]
        private void FixedUpdate()
        {
            if (!isLocalPlayer) return;
            HandleMovement();
        }

        [Client]
        private void HandleInput()
        {
            _inputMovement = new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical"));
            _hasMovementInput = Mathf.Abs(_inputMovement.x) > 0 || Mathf.Abs(_inputMovement.z) > 0;
            _isSprinting = _hasMovementInput && Input.GetButton("Running");
            messageCenter.Post(new PlayerInputMessage(_isSprinting));
            if (Input.GetButtonDown("Jump") && !_isJumpRequested && CheckGrounded())
            {
                _isJumpRequested = true;
            }
        }
        
        [Client]
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

        [Client]
        private void HandleMovement()
        {
            if (CheckStairs(out _stairsNormal, out var hitNormal))
            {
                Debug.Log("On stairs");
                _rigidbody.useGravity = false;
                _movement = _inputMovement.z * _stairsNormal.normalized + transform.right * _inputMovement.x;
                if (_isJumpRequested)
                {
                    _isJumpRequested = false;
                    _rigidbody.AddForce(hitNormal * _playerDataConfig.PlayerConfigData.JumpSpeed, ForceMode.Impulse);
                }
            }
            else if (CheckGrounded())
            {
                _rigidbody.useGravity = false;
                Debug.Log("On ground");
                _movement = _inputMovement.magnitude <= 0.1f ? _inputMovement : _camera.transform.TransformDirection(_inputMovement);
                _movement.y = _inputMovement.magnitude <= 0.1f ? _movement.y : 0f;
                _targetSpeed = _isSprinting ? _playerDataConfig.PlayerConfigData.RunSpeed : _hasMovementInput ? _playerDataConfig.PlayerConfigData.MoveSpeed : 0;

                if (_isJumpRequested)
                {
                    _isJumpRequested = false;
                    _rigidbody.AddForce(Vector3.up * _playerDataConfig.PlayerConfigData.JumpSpeed, ForceMode.Impulse);
                    
                    gameEventManager.Publish(new PlayerJumpEvent());
                }
            }
            else
            {
                _rigidbody.useGravity = true;
                // 如果在空中，只应用水平移动，不改变方向
                Debug.Log("In air");
                _inputMovement = transform.TransformDirection(_inputMovement);
                _inputMovement *= _playerDataConfig.PlayerConfigData.MoveSpeed;
                _hasMovementInput = Mathf.Abs(_inputMovement.x) > 0 || Mathf.Abs(_inputMovement.z) > 0;
                _velocity.y += Physics.gravity.y * Time.fixedDeltaTime;
                // 保持水平方向的动量
                _movement.x = _inputMovement.x;
                _movement.z = _inputMovement.z;
            }
            
            var position = transform.position;
            
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, SpeedSmoothTime);
            _movement = _movement.normalized * (_currentSpeed * Time.fixedDeltaTime);
            _rigidbody.MovePosition(_movement + position);
        }

        private void SyncAnimation()
        {
            gameEventManager.Publish(new PlayerSpeedChangeEvent(_currentSpeed));
            gameEventManager.Publish(new PlayerVerticalSpeedChangeEvent(_velocity.y));
        }
        
        [Client]
        private bool CheckGrounded()
        {
            return Physics.CheckSphere(_checkGroundTransform.position, _playerDataConfig.PlayerConfigData.GroundCheckRadius, _gameDataConfig.GameConfigData.GroundSceneLayer);
        }

        [Client]
        private bool CheckStairs(out Vector3 direction, out Vector3 hitNormal)
        {
            direction = Vector3.zero;
            hitNormal = Vector3.zero;
            if (Physics.Raycast(transform.position, transform.forward, out var hit, _playerDataConfig.PlayerConfigData.StairsCheckDistance, _gameDataConfig.GameConfigData.StairSceneLayer))
            {
                hitNormal = hit.normal;
                direction = Vector3.Cross(hit.normal, transform.right).normalized;
                return true;
            }
            return false;
        }
    }
}