using System;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Data.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PlayerInput
{
    public class PlayerComponentController : MonoBehaviour
    {
        [Header("Components")]
        private Rigidbody _rigidbody;
        private CapsuleCollider _capsuleCollider;
        private Animator _animator;
        private Transform _checkStairTransform;
        private Camera _camera;
        
        [Header("Input")]
        private Vector3 _movement;
        private Vector3 _rotation;
        
        [Header("States-NetworkBehaviour")]
        private PlayerInputPredictionState _inputState;
        private PropertyPredictionState _propertyPredictionState;
        private PlayerInputStateData _currentInputState;
        
        [Header("Calculators")]
        private PlayerPhysicsCalculator _playerPhysicsCalculator;
        private PlayerPropertyCalculator _playerPropertyCalculator;
        private PlayerAnimationCalculator _playerAnimationCalculator;
        
        [Header("Parameters")]
        private float _currentSpeed;
        private float _targetSpeed;
        private float _speedSmoothTime = 0.1f;
        private float _speedSmoothVelocity;
        public bool IsLocalPlayer { get; private set; }
        public bool IsServer { get; private set; }

        private void Start()
        {
            _rigidbody = GetComponent<Rigidbody>();
            _inputState = GetComponent<PlayerInputPredictionState>();
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _animator = GetComponent<Animator>();
            _camera = Camera.main;
            ObjectInjectProvider.Instance.Inject(this);
        }

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            GetAllCalculators(configProvider);
        }

        private void GetAllCalculators(IConfigProvider configProvider)
        {
            _playerPhysicsCalculator = new PlayerPhysicsCalculator(new PhysicsComponent(_rigidbody, transform, _checkStairTransform, _capsuleCollider, _camera));
            //_playerPropertyCalculator = new PlayerPropertyCalculator(new );
            _playerAnimationCalculator = new PlayerAnimationCalculator(new AnimationComponent{ Animator = _animator});
        }

        private void Update()
        {
            if (IsLocalPlayer)
            {
                HandleInput();
                HandleNetworkCommand();
            }
            HandleSpeed();
            HandleAnimations();
        }

        private void HandleAnimations()
        {
            _playerAnimationCalculator.UpdateAnimationState();
        }

        private void HandleSpeed()
        {
            _targetSpeed = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Speed);
        }

        private void FixedUpdate()
        {
            if (!IsLocalPlayer) return;
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _playerPhysicsCalculator.CheckPlayerState(new CheckGroundDistanceParam(_currentInputState.inputMovement, Time.fixedDeltaTime));
        }

        private void HandleInput()
        {
            _currentInputState.inputMovement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
            _currentInputState.inputAnimations = _inputState.GetAnimationStates();
        }

        private void HandleNetworkCommand()
        {
            if (_currentInputState.inputMovement.magnitude > 0.1f && _currentInputState.inputAnimations.Count > 0)
            {
                _inputState.AddPredictedCommand(new InputCommand
                {
                    inputMovement = _currentInputState.inputMovement,
                    inputAnimationStates = _currentInputState.inputAnimations.ToArray(),
                });
            }
        }

        public void HandleMovement(MoveParam moveParam)
        {
            _playerPhysicsCalculator.HandleMove(moveParam);
        }
    }
}