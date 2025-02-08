using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Data.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using HotUpdate.Scripts.Network.Data.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Data.PredictSystem.State;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.PlayerInput
{
    public class PlayerComponentController : NetworkBehaviour,IAttackAnimationEvent
    {
        [Header("Components")]
        [SerializeField]
        private Rigidbody _rigidbody;
        [SerializeField]
        private CapsuleCollider _capsuleCollider;
        [SerializeField]
        private Animator _animator;
        [SerializeField]
        private Transform _checkStairTransform;
        [SerializeField]
        private Camera _camera;
        
        [Header("Input")]
        private Vector3 _movement;
        private Vector3 _rotation;
        
        [Header("States-NetworkBehaviour")]
        private PlayerInputPredictionState _inputState;
        private PropertyPredictionState _propertyPredictionState;
        
        [Header("Subject")]
        private readonly Subject<PlayerInputStateData> _inputStream = new Subject<PlayerInputStateData>();
        private readonly Subject<int> _onAttackPoint = new Subject<int>();
        private readonly Subject<int> _onAttackEnd = new Subject<int>();
        
        private readonly ReactiveProperty<PlayerEnvironmentState> _gameStateStream = new ReactiveProperty<PlayerEnvironmentState>();
        private readonly ReactiveProperty<float> _groundDistanceStream = new ReactiveProperty<float>();
        
        [Header("Calculators")]
        private PlayerPhysicsCalculator _playerPhysicsCalculator;
        private PlayerPropertyCalculator _playerPropertyCalculator;
        private PlayerAnimationCalculator _playerAnimationCalculator;
        
        [Header("Parameters")]
        private float _currentSpeed;
        private float _targetSpeed;
        private float _health = 1f;
        private float _speedSmoothTime = 0.1f;
        private float _speedSmoothVelocity;
        
        private float FixedDeltaTime => Time.fixedDeltaTime;
        private float DeltaTime => Time.deltaTime;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        
        public int CurrentComboStage { get; private set; }
        public IObservable<PlayerInputStateData> InputStream => _inputStream;
        public IReadOnlyReactiveProperty<PlayerEnvironmentState> GameStateStream => _gameStateStream;
        public IObservable<int> AttackPointReached => _onAttackPoint;
        public IObservable<int> AttackEnded => _onAttackEnd;

        private void Awake()
        {
            ObjectInjectProvider.Instance.Inject(this);
        }

        [Inject]
        private void Init(IConfigProvider configProvider, GameSyncManager gameSyncManager)
        {
            _rigidbody = GetComponent<Rigidbody>();
            _inputState = GetComponent<PlayerInputPredictionState>();
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _animator = GetComponent<Animator>();
            _camera = Camera.main;
            GetAllCalculators(configProvider, gameSyncManager);
            _propertyPredictionState.OnPropertyChanged += HandlePropertyChange;
            
            Observable.EveryUpdate()
                .Where(_ => isLocalPlayer && _health > 0)
                .Subscribe(_ => {
                    var movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
                    var animationStates = _inputState.GetAnimationStates();
                    _targetSpeed = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Speed);
                    _health = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Health);
                    _playerAnimationCalculator.UpdateAnimationState();
                    _inputStream.OnNext(new PlayerInputStateData
                    {
                        inputMovement = movement,
                        inputAnimations = animationStates,
                    });
                })
                .AddTo(_disposables);
            
            Observable.EveryUpdate()
                .Subscribe(_ => {
                    _targetSpeed = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Speed);
                    _playerAnimationCalculator.UpdateAnimationState();
                })
                .AddTo(_disposables);
            //发送网络命令
            _inputStream.Where(x=> isLocalPlayer && x.inputMovement.magnitude > 0.1f && x.inputAnimations.Count > 0)
                .Subscribe(HandleSendNetworkCommand)
                .AddTo(_disposables);
            //处理物理信息
            _inputStream.ThrottleFirst(TimeSpan.FromMilliseconds(FixedDeltaTime * 1000))
                .Subscribe(HandleInputPhysics)
                .AddTo(_disposables);
        }

        private void HandleSendNetworkCommand(PlayerInputStateData inputData)
        {
            _inputState.AddPredictedCommand(new InputCommand
            {
                inputMovement = inputData.inputMovement,
                inputAnimationStates = inputData.inputAnimations.ToArray(),
            });
        }

        private void HandleInputPhysics(PlayerInputStateData inputData)
        {
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _playerPhysicsCalculator.CurrentSpeed = _currentSpeed;
            _gameStateStream.Value = _playerPhysicsCalculator.CheckPlayerState(new CheckGroundDistanceParam(inputData.inputMovement, FixedDeltaTime));
            _groundDistanceStream.Value = _playerPhysicsCalculator.GroundDistance;
            _playerAnimationCalculator.SetEnvironmentState(_gameStateStream.Value);
            _playerAnimationCalculator.SetGroundDistance(_groundDistanceStream.Value);
            _playerAnimationCalculator.SetAnimatorParams(inputData.inputMovement.magnitude, _groundDistanceStream.Value, _currentSpeed);
        }

        private void HandlePropertyChange(PropertyTypeEnum propertyType, PropertyCalculator value)
        {
            _playerPropertyCalculator.UpdateProperty(propertyType, value);
        }

        //IConfigProvider可能会有用
        private void GetAllCalculators(IConfigProvider configProvider, GameSyncManager gameSyncManager)
        {
            _playerPhysicsCalculator = new PlayerPhysicsCalculator(new PhysicsComponent(_rigidbody, transform, _checkStairTransform, _capsuleCollider, _camera));
            _playerPropertyCalculator = new PlayerPropertyCalculator(new Dictionary<PropertyTypeEnum, PropertyCalculator>());
            _playerAnimationCalculator = new PlayerAnimationCalculator(new AnimationComponent{ Animator = _animator});
            var jsonData = configProvider.GetConfig<JsonDataConfig>();
            var gameData = jsonData.GameConfig;
            var playerData = jsonData.PlayerConfig;
            PlayerPhysicsCalculator.SetPhysicsDetermineConstant(new PhysicsDetermineConstant
            {
                GroundMinDistance = gameData.groundMinDistance,
                GroundMaxDistance = gameData.groundMaxDistance,
                MaxSlopeAngle = gameData.maxSlopeAngle,
                StairsCheckDistance = gameData.stairsCheckDistance,
                GroundSceneLayer = gameData.groundSceneLayer,
                StairsSceneLayer = gameData.stairSceneLayer,
                RotateSpeed = gameData.rotateSpeed,
            });
            PlayerPropertyCalculator.SetCalculatorConstant(new PropertyCalculatorConstant
            {
                TickRate = gameSyncManager.TickRate,
            });
            PlayerAnimationCalculator.SetAnimationConstant(new AnimationConstant
            {
                MaxGroundDistance = gameData.groundMaxDistance,
                InputThreshold = gameData.inputThreshold,
                AttackComboMaxCount = playerData.AttackComboMaxCount,
                AnimationConfig = configProvider.GetConfig<AnimationConfig>(),
            });
        }

        private void OnAttack(int stage)
        {
            CurrentComboStage = stage;
            _onAttackPoint.OnNext(stage);
        }
        
        private void OnAttackEnd(int stage)
        {
            _onAttackEnd.OnNext(stage);
        }

        private void OnDestroy()
        {
            _disposables?.Dispose();
        }

        public AnimationState GetCurrentAnimationState(PlayerInputStateData inputData)
        {
            return _playerAnimationCalculator.DetermineAnimationState(CreateDetermineAnimationStateParams(inputData));
        }
        
        private DetermineAnimationStateParams CreateDetermineAnimationStateParams(PlayerInputStateData inputData)
        {
            return new DetermineAnimationStateParams
            {
                InputMovement = inputData.inputMovement,
                InputAnimationStates = inputData.inputAnimations,                
                GroundDistance = _groundDistanceStream.Value,
                EnvironmentState = _gameStateStream.Value,
            };            
        }
        
        //这一行开始，写对外接口
        [Server]
        public PlayerGameStateData HandleMoveAndAnimation(PlayerInputStateData inputData)
        {
            _inputStream.OnNext(inputData);
            var currentAnimationState = GetCurrentAnimationState(inputData);
            //移动
            _playerPhysicsCalculator.HandleMove(new MoveParam
            {
                InputMovement = inputData.inputMovement,
                DeltaTime = DeltaTime,
                IsMovingState = _playerAnimationCalculator.IsMovingState(),
            });
            //执行动画
            _playerAnimationCalculator.HandleAnimation(currentAnimationState);
            return new PlayerGameStateData
            {
                position = transform.position,
                rotation = transform.rotation,
                velocity = _rigidbody.velocity,
                environmentState = _gameStateStream.Value,
                command = currentAnimationState,
            };
        }

        [Server]
        public void HandlePropertyRecover(ref PlayerPropertyState playerPropertyState)
        {
            _playerPropertyCalculator.HandlePropertyRecover(ref playerPropertyState);
        }

        [Server]
        public void HandleAttackProperty(ref PlayerPropertyState playerPropertyState, ref Dictionary<int, PlayerPropertyState> defenders, Func<float, float, float, float, float> getDamageFunction)
        {
            _playerPropertyCalculator.HandleAttack(ref playerPropertyState, ref defenders, getDamageFunction);
        }

        [Server]
        public void HandleAnimationCost(ref PlayerPropertyState playerPropertyState, AnimationState animationState, float cost)
        {
            _playerPropertyCalculator.HandleAnimationCommand(ref playerPropertyState, animationState, cost);
        }
    }
}