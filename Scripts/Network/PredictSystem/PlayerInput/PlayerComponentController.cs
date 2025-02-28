using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.Data.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using InputCommand = HotUpdate.Scripts.Network.PredictSystem.Data.InputCommand;
using NetworkCommandHeader = HotUpdate.Scripts.Network.PredictSystem.Data.NetworkCommandHeader;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PlayerPropertyState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerPropertyState;
using PropertyAutoRecoverCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyAutoRecoverCommand;
using PropertyCalculator = HotUpdate.Scripts.Network.PredictSystem.State.PropertyCalculator;
using PropertyEnvironmentChangeCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyEnvironmentChangeCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
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
        
        [Header("States-NetworkBehaviour")]
        private PlayerInputPredictionState _inputState;
        private PropertyPredictionState _propertyPredictionState;
        
        [Header("Subject")]
        private readonly Subject<PlayerInputStateData> _inputStream = new Subject<PlayerInputStateData>();
        private readonly Subject<int> _onAttackPoint = new Subject<int>();
        private readonly Subject<int> _onAttackEnd = new Subject<int>();
        
        private readonly ReactiveProperty<PlayerEnvironmentState> _gameStateStream = new ReactiveProperty<PlayerEnvironmentState>();
        private readonly ReactiveProperty<float> _groundDistanceStream = new ReactiveProperty<float>();
        private readonly ReactiveProperty<bool> _isSpecialActionStream = new ReactiveProperty<bool>();
        
        [Header("Calculators")]
        private PlayerPhysicsCalculator _playerPhysicsCalculator;
        private PlayerPropertyCalculator _playerPropertyCalculator;
        private PlayerAnimationCalculator _playerAnimationCalculator;
        private PlayerBattleCalculator _playerBattleCalculator;
        
        [Header("Parameters")]
        private float _currentSpeed;
        private float _targetSpeed;
        private float _health = 1f;
        private float _speedSmoothTime = 0.1f;
        private float _speedSmoothVelocity;
        
        private float FixedDeltaTime => Time.fixedDeltaTime;
        private float DeltaTime => Time.deltaTime;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private GameSyncManager _gameSyncManager;
        private MapBoundDefiner _mapBoundDefiner;
        private PlayerInGameManager _playerInGameManager;
        
        public int CurrentComboStage { get; private set; }
        public IObservable<PlayerInputStateData> InputStream => _inputStream;
        public IReadOnlyReactiveProperty<PlayerEnvironmentState> GameStateStream => _gameStateStream;
        public IReadOnlyReactiveProperty<float> GroundDistanceStream => _groundDistanceStream;
        public IObservable<bool> IsSpecialAction => _isSpecialActionStream;
        public IObservable<int> AttackPointReached => _onAttackPoint;
        public IObservable<int> AttackEnded => _onAttackEnd;

        private void Awake()
        {
            ObjectInjectProvider.Instance.Inject(this);
        }

        [Inject]
        private void Init(IConfigProvider configProvider, GameSyncManager gameSyncManager, MapBoundDefiner mapBoundDefiner, PlayerInGameManager playerInGameManager)
        {
            _gameSyncManager = gameSyncManager;
            _mapBoundDefiner = mapBoundDefiner;
            _playerInGameManager = playerInGameManager;
            _rigidbody = GetComponent<Rigidbody>();
            _inputState = GetComponent<PlayerInputPredictionState>();
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _animator = GetComponent<Animator>();
            _camera = Camera.main;
            GetAllCalculators(configProvider, gameSyncManager);
            _propertyPredictionState.OnPropertyChanged += HandlePropertyChange;
            _inputState.OnPlayerStateChanged += HandlePlayerStateChanged;
            _inputState.OnPlayerAnimationCooldownChanged += HandlePlayerAnimationCooldownChanged;
            _inputState.OnPlayerInputStateChanged += HandlePlayerInputStateChanged;
            _inputState.IsInSpecialState += HandleSpecialState;
            
            Observable.EveryUpdate()
                .Where(_ => isLocalPlayer && _health > 0)
                .Subscribe(_ => {
                    var movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
                    var animationStates = _inputState.GetAnimationStates();
                    var playerInputStateData = new PlayerInputStateData
                    {
                        InputMovement = movement,
                        InputAnimations = animationStates,
                    };
                    var command = GetCurrentAnimationState(playerInputStateData);
                    playerInputStateData.Command = command;
                    _targetSpeed = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Speed);
                    _health = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Health);
                    _playerAnimationCalculator.UpdateAnimationState();
                    _inputStream.OnNext(playerInputStateData);
                })
                .AddTo(_disposables);
            
            Observable.EveryUpdate()
                .Where(_ => !_isSpecialActionStream.Value)
                .Subscribe(_ => {
                    _targetSpeed = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Speed);
                    _playerAnimationCalculator.UpdateAnimationState();
                })
                .AddTo(_disposables);
            //发送网络命令
            _inputStream.Where(x=> isLocalPlayer && x.InputMovement.magnitude > 0.1f && x.InputAnimations.Count > 0 && x.Command != AnimationState.None)
                .Subscribe(HandleSendNetworkCommand)
                .AddTo(_disposables);
            //处理物理信息
            _inputStream.ThrottleFirst(TimeSpan.FromMilliseconds(FixedDeltaTime * 1000))
                .Subscribe(HandleInputPhysics)
                .AddTo(_disposables);
        }

        private bool HandleSpecialState()
        {
            return _playerAnimationCalculator.IsSpecialAction;
        }

        [ClientCallback]
        private void HandlePlayerInputStateChanged(PlayerInputStateData playerInputStateData)
        {
            HandleClientMoveAndAnimation(playerInputStateData);
        }

        [ClientCallback]
        private void HandlePlayerAnimationCooldownChanged(PlayerAnimationCooldownState newCooldownState)
        {
            
        }

        [ClientCallback]
        private void HandlePlayerStateChanged(PlayerGameStateData newState)
        {
            transform.position = newState.Position;
            transform.rotation = newState.Quaternion;
            _rigidbody.velocity = newState.Velocity;
            _gameStateStream.Value = newState.PlayerEnvironmentState;
            _playerAnimationCalculator.SetEnvironmentState(newState.PlayerEnvironmentState);
        }

        [Client]
        private void HandleSendNetworkCommand(PlayerInputStateData inputData)
        {
            _inputState.AddPredictedCommand(new InputCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Input, CommandAuthority.Client),
                InputMovement = inputData.InputMovement,
                InputAnimationStates = inputData.InputAnimations.ToArray(),
                CommandAnimationState = inputData.Command,
            });
            _propertyPredictionState.AddPredictedCommand(new PropertyAutoRecoverCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Property, CommandAuthority.Client),
            });
            _propertyPredictionState.AddPredictedCommand(new PropertyEnvironmentChangeCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Property, CommandAuthority.Client),
                HasInputMovement = inputData.InputMovement.magnitude > 0.1f,
                PlayerEnvironmentState = _gameStateStream.Value,
                IsSprinting = inputData.InputAnimations.Any(x => x == AnimationState.Sprint),
            });
        }

        private void HandleInputPhysics(PlayerInputStateData inputData)
        {
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _playerPhysicsCalculator.CurrentSpeed = _currentSpeed;
            _gameStateStream.Value = _playerPhysicsCalculator.CheckPlayerState(new CheckGroundDistanceParam(inputData.InputMovement, FixedDeltaTime));
            _groundDistanceStream.Value = _playerPhysicsCalculator.GroundDistance;
            _isSpecialActionStream.Value = _playerAnimationCalculator.IsSpecialAction;
            _playerAnimationCalculator.SetEnvironmentState(_gameStateStream.Value);
            _playerAnimationCalculator.SetGroundDistance(_groundDistanceStream.Value);
            _playerAnimationCalculator.SetAnimatorParams(inputData.InputMovement.magnitude, _groundDistanceStream.Value, _currentSpeed);
        }

        private void HandlePropertyChange(PropertyTypeEnum propertyType, PropertyCalculator value)
        {
            _playerPropertyCalculator.UpdateProperty(propertyType, value);
        }

        private void GetAllCalculators(IConfigProvider configProvider, GameSyncManager gameSyncManager)
        {
            _playerPhysicsCalculator = new PlayerPhysicsCalculator(new PhysicsComponent(_rigidbody, transform, _checkStairTransform, _capsuleCollider, _camera));
            _playerPropertyCalculator = new PlayerPropertyCalculator(new Dictionary<PropertyTypeEnum, PropertyCalculator>());
            _playerAnimationCalculator = new PlayerAnimationCalculator(new AnimationComponent{ Animator = _animator});
            _playerBattleCalculator = new PlayerBattleCalculator(new PlayerBattleComponent(transform,_mapBoundDefiner, _playerInGameManager));
            var jsonData = configProvider.GetConfig<JsonDataConfig>();
            var propertyConfig = configProvider.GetConfig<PropertyConfig>();
            var gameData = jsonData.GameConfig;
            var playerData = jsonData.PlayerConfig;
            var noneWeapon = propertyConfig.GetAttackBaseParams();
            PlayerPhysicsCalculator.SetPhysicsDetermineConstant(new PhysicsDetermineConstant
            {
                GroundMinDistance = gameData.groundMinDistance,
                GroundMaxDistance = gameData.groundMaxDistance,
                MaxSlopeAngle = gameData.maxSlopeAngle,
                StairsCheckDistance = gameData.stairsCheckDistance,
                GroundSceneLayer = gameData.groundSceneLayer,
                StairsSceneLayer = gameData.stairSceneLayer,
                RotateSpeed = playerData.RotateSpeed,
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
            PlayerBattleCalculator.SetAttackConfigData(noneWeapon);
        }

        private void OnAttack(int stage)
        {
            _isSpecialActionStream.Value = false;
            CurrentComboStage = stage;
            _onAttackPoint.OnNext(stage);
        }
        
        private void OnAttackEnd(int stage)
        {
            _isSpecialActionStream.Value = false;
            _onAttackEnd.OnNext(stage);
        }

        private void OnDestroy()
        {
            _disposables?.Clear();
            _propertyPredictionState.OnPropertyChanged -= HandlePropertyChange;
            _inputState.OnPlayerStateChanged -= HandlePlayerStateChanged;
            _inputState.OnPlayerAnimationCooldownChanged -= HandlePlayerAnimationCooldownChanged;
            _inputState.OnPlayerInputStateChanged -= HandlePlayerInputStateChanged;
            _inputState.IsInSpecialState -= HandleSpecialState;
        }

        public AnimationState GetCurrentAnimationState(PlayerInputStateData inputData)
        {
            return _playerAnimationCalculator.DetermineAnimationState(CreateDetermineAnimationStateParams(inputData));
        }
        
        public bool IsInSpecialState()
        {
            return _playerAnimationCalculator.IsSpecialAction;
        }

        public bool CanPlayAttackAnimation(AnimationState command)
        {
            return _playerAnimationCalculator.CanPlayAnimation(command);
        }

        private DetermineAnimationStateParams CreateDetermineAnimationStateParams(PlayerInputStateData inputData)
        {
            return new DetermineAnimationStateParams
            {
                InputMovement = inputData.InputMovement,
                InputAnimationStates = inputData.InputAnimations.ToList(),                
                GroundDistance = _groundDistanceStream.Value,
                EnvironmentState = _gameStateStream.Value,
            };            
            
        }

        private PlayerGameStateData HandleMoveAndAnimation(PlayerInputStateData inputData)
        {
            var cameraForward = Vector3.Scale(_camera.transform.forward, new Vector3(1, 0, 1)).normalized;
            //移动
            _playerPhysicsCalculator.HandleMove(new MoveParam
            {
                InputMovement = inputData.InputMovement,
                DeltaTime = DeltaTime,
                IsMovingState = _playerAnimationCalculator.IsMovingState(),
                CameraForward = _playerPhysicsCalculator.CompressYaw(cameraForward.y),
                IsClearVelocity = PlayerAnimationCalculator.IsClearVelocity(inputData.Command),
            }, isLocalPlayer);
            //执行动画
            _playerAnimationCalculator.HandleAnimation(inputData.Command);
            return new PlayerGameStateData
            {
                Position = transform.position,
                Quaternion = transform.rotation,
                Velocity = _rigidbody.velocity,
                PlayerEnvironmentState = _gameStateStream.Value,
                AnimationState = inputData.Command,
            };
        }

        [Client]
        public PlayerGameStateData HandleClientMoveAndAnimation(PlayerInputStateData inputData)
        {
            return HandleMoveAndAnimation(inputData);
        }

        //这一行开始，写对外接口
        [Server]
        public PlayerGameStateData HandleServerMoveAndAnimation(PlayerInputStateData inputData)
        {
            _inputStream.OnNext(inputData);
            return HandleMoveAndAnimation(inputData);
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

        [Server]
        public void HandleEnvironmentChange(ref PlayerPropertyState playerPropertyState, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            _playerPropertyCalculator.HandleEnvironmentChange(ref playerPropertyState, hasInputMovement, environmentType, isSprinting);
        }

        [Server]
        public uint[] HandleAttack(AttackParams attackParams)
        {
            return _playerBattleCalculator.IsInAttackRange(attackParams);
        }
    }
}