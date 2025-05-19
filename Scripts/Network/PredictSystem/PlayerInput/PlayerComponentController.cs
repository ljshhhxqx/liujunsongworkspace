using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.GameBase;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.Tool.Static;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Panel;
using HotUpdate.Scripts.UI.UIs.Panel.Backpack;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Popup;
using MemoryPack;
using Mirror;
using UI.UIBase;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using InputCommand = HotUpdate.Scripts.Network.PredictSystem.Data.InputCommand;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PropertyAutoRecoverCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyAutoRecoverCommand;
using PropertyCalculator = HotUpdate.Scripts.Network.PredictSystem.State.PropertyCalculator;
using PropertyEnvironmentChangeCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyEnvironmentChangeCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class PlayerComponentController : NetworkAutoInjectComponent,IAttackAnimationEvent
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
        [SerializeField]
        private PlayerEffectPlayer playerEffectPlayer;
        [SerializeField]
        private Transform effectContainer;
        
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
        private PlayerItemCalculator _playerItemCalculator;
        private PlayerElementCalculator _playerElementCalculator;
        private PlayerEquipmentCalculator _playerEquipmentCalculator;
        private PlayerShopCalculator _playerShopCalculator;
        
        [Header("Parameters")]
        private float _currentSpeed;
        private float _targetSpeed;
        private float _speedSmoothTime = 0.1f;
        private float _speedSmoothVelocity;
        private bool _canOpenShop;
        private SubjectedStateType _subjectedStateType;
        private List<IAnimationCooldown> _animationCooldowns = new List<IAnimationCooldown>();
        
        private static float FixedDeltaTime => Time.fixedDeltaTime;
        private static float DeltaTime => Time.deltaTime;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private GameSyncManager _gameSyncManager;
        private MapBoundDefiner _mapBoundDefiner;
        private IConfigProvider _configProvider;
        private GameConfigData _gameConfigData;
        private PlayerConfigData _playerConfigData;
        private UIManager _uiManager;
        private PlayerInGameManager _playerInGameManager;
        private InteractSystem _interactSystem;
        private UIHoleOverlay _uiHoleOverlay;
        
        private BindingKey _propertyBindKey;
        private BindingKey _itemBindKey;
        private BindingKey _equipBindKey;
        private BindingKey _shopBindKey;
        private BindingKey _goldBindKey;
        private BindingKey _playerDeathTimeBindKey;
        private BindingKey _playerTraceOtherPlayerHpBindKey;
        
        private Dictionary<Type, bool> _reactivePropertyBinds = new Dictionary<Type, bool>();
        
        private Vector3 _bornPosition;

        [SyncVar] 
        public int unionId;

        [SyncVar] 
        public bool isDead;

        public int CurrentComboStage { get; private set; }
        public IObservable<PlayerInputStateData> InputStream => _inputStream;
        public IReadOnlyReactiveProperty<PlayerEnvironmentState> GameStateStream => _gameStateStream;
        public IReadOnlyReactiveProperty<float> GroundDistanceStream => _groundDistanceStream;
        public IObservable<bool> IsSpecialAction => _isSpecialActionStream;
        public IObservable<int> AttackPointReached => _onAttackPoint;
        public IObservable<int> AttackEnded => _onAttackEnd;
        
        public List<IAnimationCooldown> GetNowAnimationCooldowns()
        {
            return _animationCooldowns;
        }

        [Inject]
        private void Init(IConfigProvider configProvider, 
            GameSyncManager gameSyncManager, 
            MapBoundDefiner mapBoundDefiner, 
            PlayerInGameManager playerInGameManager,
            UIManager uiManager)
        {
            _configProvider = configProvider;
            var jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _gameConfigData = jsonDataConfig.GameConfig;
            _playerConfigData = jsonDataConfig.PlayerConfig;
            _gameSyncManager = gameSyncManager;
            _mapBoundDefiner = mapBoundDefiner;
            _playerInGameManager = playerInGameManager;
            _interactSystem = FindObjectOfType<InteractSystem>();
            _uiManager = uiManager;
            _rigidbody = GetComponent<Rigidbody>();
            _inputState = GetComponent<PlayerInputPredictionState>();
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _animator = GetComponent<Animator>();
            _camera = Camera.main;
            GetAllCalculators(configProvider, gameSyncManager);
            _propertyPredictionState.OnPropertyChanged += HandlePropertyChange;
            _propertyPredictionState.OnStateChanged += HandlePropertyStateChanged;
            _propertyPredictionState.OnPlayerDead += HandlePlayerDeadClient;
            _propertyPredictionState.OnPlayerRespawned += HandlePlayerRespawnedClient;
            _inputState.OnPlayerStateChanged += HandlePlayerStateChanged;
            _inputState.OnPlayerAnimationCooldownChanged += HandlePlayerAnimationCooldownChanged;
            _inputState.OnPlayerInputStateChanged += HandlePlayerInputStateChanged;
            _inputState.IsInSpecialState += HandleSpecialState;
            _animationCooldowns = GetAnimationCooldowns();
            _capsuleCollider.OnTriggerEnterAsObservable()
                .Where(c => c.gameObject.TryGetComponent<PlayerBase>(out _) && isLocalPlayer)
                .Subscribe(c =>
                {
                    _canOpenShop = _playerInGameManager.IsPlayerInHisBase(netId, out _);
                })
                .AddTo(_disposables);
            _capsuleCollider.OnTriggerStayAsObservable()
                .Throttle(TimeSpan.FromMilliseconds(_gameSyncManager.TickRate * 1000))
                .Where(c => c.gameObject.TryGetComponent<PlayerBase>(out _) && isLocalPlayer)
                .Subscribe(c =>
                {
                    var header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId,
                        CommandType.Property, CommandAuthority.Client);
                    var playerTouchedBaseCommand = new PlayerTouchedBaseCommand
                    {
                        Header = header,
                    };
                    _gameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(playerTouchedBaseCommand));
                }).AddTo(_disposables);
            _capsuleCollider.OnTriggerExitAsObservable()
                .Where(c => c.gameObject.TryGetComponent<PlayerBase>(out _) && isLocalPlayer)
                .Subscribe(_ =>
                {
                    _canOpenShop = false;
                }).AddTo(_disposables);
            
            Observable.EveryUpdate()
                .Where(_ => isLocalPlayer && _subjectedStateType.HasAllStates(SubjectedStateType.None))
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
                    _playerAnimationCalculator.UpdateAnimationState();
                    _inputStream.OnNext(playerInputStateData);
                })
                .AddTo(_disposables);
            
            Observable.EveryUpdate()
                .Where(_ => !_isSpecialActionStream.Value)
                .Subscribe(_ => 
                {
                    _targetSpeed = _playerPropertyCalculator.GetProperty(PropertyTypeEnum.Speed);
                    _playerAnimationCalculator.UpdateAnimationState();
                })
                .AddTo(_disposables);
            //发送网络命令
            _inputStream.Where(x=> isLocalPlayer && x.InputMovement.magnitude > 0.1f && x.InputAnimations.Count > 0 && x.Command != AnimationState.None)
                .Subscribe(HandleSendNetworkCommand)
                .AddTo(_disposables);
            //处理物理信息
            _inputStream.Throttle(TimeSpan.FromMilliseconds(FixedDeltaTime * 1000))
                .Subscribe(HandleInputPhysics)
                .AddTo(_disposables);
            Observable.EveryUpdate().Throttle(TimeSpan.FromMilliseconds(FixedDeltaTime * 10 * 1000))
                .Where(_ => isLocalPlayer)
                .Subscribe(_ =>
                {
                    var otherPlayers = NetworkServer.connections
                        .Where(x => x.Value.connectionId != connectionToClient.connectionId)
                        .Select(x => x.Value.identity.GetComponent<Transform>());
                    var layerMask = _gameConfigData.groundSceneLayer | _gameConfigData.stairSceneLayer | _playerConfigData.PlayerLayer;
                    if (PlayerPhysicsCalculator.TryGetPlayersInScreen(_camera, otherPlayers, out var playersInScreen, layerMask))
                    {
                        var header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId,
                            CommandType.Property, CommandAuthority.Client);
                        var playerInScreenCommand = new PlayerTraceOtherPlayerHpCommand
                        {
                            Header = header,
                            TargetConnectionIds = playersInScreen.ToArray(),
                        };
                        _gameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(playerInScreenCommand));
                    }
                })
                .AddTo(_disposables);
            if (isLocalPlayer)
            {
                _propertyBindKey = new BindingKey(UIPropertyDefine.PlayerProperty, DataScope.LocalPlayer,
                    UIPropertyBinder.LocalPlayerId);
                _itemBindKey = new BindingKey(UIPropertyDefine.BagItem, DataScope.LocalPlayer,
                    UIPropertyBinder.LocalPlayerId);
                _shopBindKey = new BindingKey(UIPropertyDefine.ShopItem, DataScope.LocalPlayer,
                    UIPropertyBinder.LocalPlayerId);
                _goldBindKey = new BindingKey(UIPropertyDefine.PlayerBaseData, DataScope.LocalPlayer,
                    UIPropertyBinder.LocalPlayerId);
                _equipBindKey = new BindingKey(UIPropertyDefine.EquipmentItem, DataScope.LocalPlayer,
                    UIPropertyBinder.LocalPlayerId);
                _playerDeathTimeBindKey = new BindingKey(UIPropertyDefine.PlayerDeathTime, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                _playerTraceOtherPlayerHpBindKey = new BindingKey(UIPropertyDefine.PlayerTraceOtherPlayerHp, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
                HandleLocalInitCallback();
            }
        }

        private void HandlePropertyStateChanged(SubjectedStateType subjectedStateType)
        {
            _subjectedStateType = subjectedStateType;
            switch (_subjectedStateType)
            {
                case SubjectedStateType.None:
                    break;
                case SubjectedStateType.IsInvisible:
                    break;
                case SubjectedStateType.IsFrozen:
                    break;
                case SubjectedStateType.IsElectrified:
                    break;
                case SubjectedStateType.IsBlowup:
                    break;
                case SubjectedStateType.IsStunned:
                    break;
                case SubjectedStateType.IsDead:
                    //_playerAnimationCalculator.HandleAnimation(AnimationState.Dead);
                    break;
                case SubjectedStateType.IsCantMoved:
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void HandleLocalInitCallback()
        {
            _uiHoleOverlay = _uiManager.SwitchUI<UIHoleOverlay>();
            if (!_reactivePropertyBinds.ContainsKey(typeof(PropertyItemData)))
            {
                _reactivePropertyBinds.Add(typeof(PropertyItemData), true);
                var playerPropertiesOverlay = _uiManager.SwitchUI<PlayerPropertiesOverlay>();
                playerPropertiesOverlay.BindPlayerProperty(UIPropertyBinder.GetReactiveDictionary<PropertyItemData>(_propertyBindKey));
            }

            if (!_reactivePropertyBinds.ContainsKey(typeof(ValuePropertyData)))
            {
                _reactivePropertyBinds.Add(typeof(ValuePropertyData), true);
                var playerDamageOverlay = _uiManager.SwitchUI<PlayerDamageDeathOverlay>();
                playerDamageOverlay.BindGold(UIPropertyBinder.ObserveProperty<ValuePropertyData>(_goldBindKey));
                _uiHoleOverlay.BindGoldData(UIPropertyBinder.ObserveProperty<ValuePropertyData>(_goldBindKey));
            }

            if (!_reactivePropertyBinds.ContainsKey(typeof(PlayerHpItemData)))
            {
                var followData = new FollowTargetParams();
                followData.ScreenBorderOffset = _gameConfigData.screenBorderOffset;
                followData.MainCamera = _camera;
                _reactivePropertyBinds.Add(typeof(PlayerHpItemData), true);
                var playerDamageOverlay = _uiManager.SwitchUI<PlayerHpShowOverlay>();
                playerDamageOverlay.BindPlayersHp(UIPropertyBinder.GetReactiveDictionary<PlayerHpItemData>(_playerTraceOtherPlayerHpBindKey), followData);
            }
        }

        public void SwitchBag()
        {
            if (!isLocalPlayer)
            {
                return;
            }

            if (_uiManager.IsUIOpen(UIType.Backpack))
            {
                _uiManager.CloseUI(UIType.Backpack);
                return;
            }
            var bagItemOverlay = _uiManager.SwitchUI<BackpackScreenUI>();
            bagItemOverlay.BindBagItemData(UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemBindKey));
            bagItemOverlay.BindEquipItemData(UIPropertyBinder.GetReactiveDictionary<EquipItemData>(_equipBindKey));
        }

        public void SwitchShop()
        {
            if (!isLocalPlayer)
            {
                return;
            }
            
            if (_uiManager.IsUIOpen(UIType.Shop))
            {
                _uiManager.CloseUI(UIType.Shop);
                return;
            }
            var shopScreenUI = _uiManager.SwitchUI<ShopScreenUI>();
            shopScreenUI.BindShopItemData(UIPropertyBinder.GetReactiveDictionary<RandomShopItemData>(_shopBindKey));
            shopScreenUI.BindBagItemData(UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemBindKey));
            shopScreenUI.BindPlayerGold(UIPropertyBinder.ObserveProperty<ValuePropertyData>(_propertyBindKey));
            shopScreenUI.OnRefresh.Subscribe(_ =>
            {
                var refreshCommand = new RefreshShopCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Shop, CommandAuthority.Client
                    , CommandExecuteType.Immediate),
                };
                _gameSyncManager.EnqueueCommand(MemoryPackSerializer.Serialize(refreshCommand));
            }).AddTo(shopScreenUI.gameObject);
        }
        
        // public void SwitchPlayerDeathTime()
        // {
        //     if (!isLocalPlayer)
        //     {
        //         return;
        //     }
        //     
        //     // if (_uiManager.IsUIOpen(UIType.PlayerDamageDeathOverlay))
        //     // {
        //     //     _uiManager.CloseUI(UIType.PlayerDeathTime);
        //     //     return;
        //     // }
        //     // var playerDeathTimeUI = _uiManager.SwitchUI<PlayerDeathTimeUI>();
        //     // playerDeathTimeUI.BindPlayerDeathTime(UIPropertyBinder.ObserveProperty<PlayerDeathTimeData>(_playerDeathTimeBindKey));
        // }

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
            _playerItemCalculator = new PlayerItemCalculator();
            _playerElementCalculator = new PlayerElementCalculator();
            _playerEquipmentCalculator = new PlayerEquipmentCalculator();
            _playerShopCalculator = new PlayerShopCalculator();
            var jsonData = configProvider.GetConfig<JsonDataConfig>();
            var propertyConfig = configProvider.GetConfig<PropertyConfig>();
            var gameData = jsonData.GameConfig;
            var playerData = jsonData.PlayerConfig;
            var noneWeapon = propertyConfig.GetAttackBaseParams();
            PlayerElementCalculator.SetPlayerElementComponent(_configProvider.GetConfig<ElementAffinityConfig>(), _configProvider.GetConfig<TransitionLevelBaseDamageConfig>(), _configProvider.GetConfig<ElementConfig>());
            PlayerPhysicsCalculator.SetPhysicsDetermineConstant(new PhysicsDetermineConstant
            {
                GroundMinDistance = gameData.groundMinDistance,
                GroundMaxDistance = gameData.groundMaxDistance,
                MaxSlopeAngle = gameData.maxSlopeAngle,
                StairsCheckDistance = gameData.stairsCheckDistance,
                GroundSceneLayer = gameData.groundSceneLayer,
                StairsSceneLayer = gameData.stairSceneLayer,
                RotateSpeed = playerData.RotateSpeed,
                IsServer = isServer,
                MaxDetermineDistance = gameData.maxTraceDistance,
                ViewAngle = gameData.maxViewAngle,
                ObstructionCheckRadius = gameData.obstacleCheckRadius,
            });
            PlayerPropertyCalculator.SetCalculatorConstant(new PropertyCalculatorConstant
            {
                TickRate = gameSyncManager.TickRate,
                IsServer = isServer,
            });
            PlayerAnimationCalculator.SetAnimationConstant(new AnimationConstant
            {
                MaxGroundDistance = gameData.groundMaxDistance,
                InputThreshold = gameData.inputThreshold,
                AttackComboMaxCount = playerData.AttackComboMaxCount,
                AnimationConfig = configProvider.GetConfig<AnimationConfig>(),
                IsServer = isServer,
            });
            PlayerBattleCalculator.SetAttackConfigData(noneWeapon);
            PlayerItemCalculator.SetConstant(new PlayerItemConstant
            {
                ItemConfig = configProvider.GetConfig<ItemConfig>(),
                WeaponConfig = configProvider.GetConfig<WeaponConfig>(),
                ArmorConfig = configProvider.GetConfig<ArmorConfig>(),
                PropertyConfig = configProvider.GetConfig<PropertyConfig>(),
                ConditionConfig = configProvider.GetConfig<BattleEffectConditionConfig>(),
                GameSyncManager = gameSyncManager,
                InteractSystem = _interactSystem,
                IsServer = isServer,
                ConstantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>(),
                RandomBuffConfig = configProvider.GetConfig<RandomBuffConfig>(),
            });
            PlayerEquipmentCalculator.SetConstant(new PlayerEquipmentConstant
            {
                GameSyncManager = gameSyncManager,
                ItemConfig = configProvider.GetConfig<ItemConfig>(),
                IsServer = isServer,
            });
            PlayerShopCalculator.SetConstant(new ShopCalculatorConstant
            {
                GameSyncManager = gameSyncManager,
                ShopConfig = configProvider.GetConfig<ShopConfig>(),
                ItemConfig = configProvider.GetConfig<ItemConfig>(),
                IsServer = isServer,
            });
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
            _propertyPredictionState.OnStateChanged -= HandlePropertyStateChanged;
            _propertyPredictionState.OnPlayerDead -= HandlePlayerDeadClient;
            _propertyPredictionState.OnPlayerRespawned -= HandlePlayerRespawnedClient;
            _inputState.OnPlayerStateChanged -= HandlePlayerStateChanged;
            _inputState.OnPlayerAnimationCooldownChanged -= HandlePlayerAnimationCooldownChanged;
            _inputState.OnPlayerInputStateChanged -= HandlePlayerInputStateChanged;
            _inputState.IsInSpecialState -= HandleSpecialState;
            _animationCooldowns.Clear();
            _inputStream.Dispose();
            _effectContainer.Clear();
            playerEffectPlayer.StopAllEffect(container => GameObjectPoolManger.Instance.ReturnObject(container.gameObject));
            _effectPool.Clear();
        }

        private void HandlePlayerDeadClient(float countdownTime)
        {
            if (!isLocalPlayer)
            {
                return;
            }
            _playerAnimationCalculator.HandleAnimation(AnimationState.Dead);
            var playerDamageDeathOverlay = _uiManager.GetUI<PlayerDamageDeathOverlay>();
            playerDamageDeathOverlay.PlayDeathEffect(countdownTime);
        }

        private void HandlePlayerRespawnedClient()
        {
            var playerDamageDeathOverlay = _uiManager.GetUI<PlayerDamageDeathOverlay>();
            playerDamageDeathOverlay.Clear();
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
        public void HandlePropertyRecover(ref PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            _playerPropertyCalculator.HandlePropertyRecover(ref playerPredictablePropertyState);
        }

        [Server]
        public void HandleAnimationCost(ref PlayerPredictablePropertyState playerPredictablePropertyState, AnimationState animationState, float cost)
        {
            _playerPropertyCalculator.HandleAnimationCommand(ref playerPredictablePropertyState, animationState, cost);
        }

        [Server]
        public void HandleEnvironmentChange(ref PlayerPredictablePropertyState playerPredictablePropertyState, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            _playerPropertyCalculator.HandleEnvironmentChange(ref playerPredictablePropertyState, hasInputMovement, environmentType, isSprinting);
        }

        [Server]
        public uint[] HandleAttack(AttackParams attackParams)
        {
            return _playerBattleCalculator.IsInAttackRange(attackParams);
        }

        [Server]
        public PlayerPredictablePropertyState HandlePlayerDie(PlayerPredictablePropertyState playerPredictablePropertyState, float countdownTime)
        {
            var diePlayerState = _playerPropertyCalculator.HandlePlayerDeath(playerPredictablePropertyState);
            isDead = true;
            return diePlayerState;
        }

        [Server]
        public PlayerPredictablePropertyState HandlePlayerRespawn(
            PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            var playerState = _playerPropertyCalculator.HandlePlayerRespawn(playerPredictablePropertyState);
            isDead = false;
            return playerState;
        }

        [Server]
        public void HandleTracedPlayerHp(int connectionId, List<TracedPlayerInfo> tracedInfo)
        {
            var player = NetworkServer.connections[connectionId];
            if (player == null)
            {
                return;
            }
            TargetRpcHandlePlayerHp(player, MemoryPackSerializer.Serialize(tracedInfo));
        }

        [TargetRpc]
        private void TargetRpcHandlePlayerHp(NetworkConnection target, byte[] data)
        {
            var info = MemoryPackSerializer.Deserialize<List<TracedPlayerInfo>>(data);
            var dic = UIPropertyBinder.GetReactiveDictionary<PlayerHpItemData>(_playerTraceOtherPlayerHpBindKey);
            for (int i = 0; i < info.Count; i++)
            {
                var tracedInfo = info[i];
                dic[tracedInfo.PlayerId] = new PlayerHpItemData
                {
                    PlayerId = tracedInfo.PlayerId,
                    PlayerPosition = transform.position,
                    TargetPosition = tracedInfo.Position,
                    MaxMp = tracedInfo.MaxMana,
                    CurrentHp = tracedInfo.Hp,
                    CurrentMp = tracedInfo.Mana,
                    MaxHp = tracedInfo.MaxHp,
                    Name = tracedInfo.PlayerName,
                };
            }
        }

        [Server]
        public void ShowPlayerCanChangeUnionUI(int connectionId, uint killerPlayerId, uint victimPlayerId)
        {
            var connection = NetworkServer.connections[connectionId];
            if (connection == null)
            {
                return;
            }
            TargetRpcShowPlayerCanChangeUnionUI(connection, killerPlayerId, victimPlayerId);
        }

        [TargetRpc]
        private void TargetRpcShowPlayerCanChangeUnionUI(NetworkConnection connection, uint killerPlayerId, uint victimPlayerId)
        {
            DelayInvoker.DelayInvoke(10, () =>
            {
                if (_uiManager.IsUIOpen(UIType.TipsPopup))
                {
                    _uiManager.CloseUI(UIType.TipsPopup);
                }
            });
            _uiManager.SwitchUI<TipsPopup>(ui =>
            {
                ui.ShowTips($"是否与刚刚击杀的玩家交换阵营？", () =>
                {
                    var playerChangeUnionCommand = new PlayerChangeUnionRequest
                    {
                        Header = GameSyncManager.CreateInteractHeader(connection.connectionId, InteractCategory.PlayerToPlayer),
                        KillerPlayerId = killerPlayerId,
                        DeadPlayerId = victimPlayerId,
                    };
                    _interactSystem.EnqueueCommand(MemoryPackSerializer.Serialize(playerChangeUnionCommand));
                }, () =>
                {
                    _uiManager.CloseUI(UIType.TipsPopup);
                });
            });
        }

        [ClientRpc]
        public void RpcSetPlayerAlpha(float alpha)
        {
            playerEffectPlayer.SetAlpha(alpha / 1000f);
        }

        public void SetAnimatorSpeed(AnimationState animationState, float speed)
        {
            var propertyConfig = _configProvider.GetConfig<PropertyConfig>();
            var property = propertyConfig.GetPropertyType(animationState);
            var minMaxAttackSpeed = propertyConfig.GetMinMaxProperty(property);
            var cooldown = _animationCooldowns.Find(x => x.AnimationState == animationState);
            if (cooldown == null)
            {
                return;
            }
            speed = Mathf.Clamp(speed, minMaxAttackSpeed.Item1, minMaxAttackSpeed.Item2);

            cooldown.SetAnimationSpeed(speed);
            _playerAnimationCalculator.SetClipSpeed(animationState, speed);
        }
        
        private List<IAnimationCooldown> GetAnimationCooldowns()
        {
            var list = new List<IAnimationCooldown>();
            var animationStates = Enum.GetValues(typeof(AnimationState)).Cast<AnimationState>();
            var config = _configProvider.GetConfig<AnimationConfig>();
            foreach (var animationState in animationStates)
            {
                var info = config.GetAnimationInfo(animationState);
                switch (info.cooldownType)
                {
                    case CooldownType.Normal:
                        list.Add(new AnimationCooldown(animationState, info.cooldown, 1));
                        break;
                    case CooldownType.KeyFrame:
                        list.Add(new KeyframeCooldown(animationState, info.cooldown, info.keyframeData.ToList(), 1));
                        break;
                    case CooldownType.Combo:
                        list.Add(new ComboCooldown(animationState, info.keyframeData.Select(x => x.resetCooldownWindowTime).ToList(), info.cooldown, 1));
                        break;
                    case CooldownType.KeyFrameAndCombo:
                        list.Add(new KeyframeComboCooldown(animationState, info.cooldown, info.keyframeData.ToList(), 1));
                        break;
                }
            }
            return list;
        }

        public void UpdateAnimation(float deltaTime)
        {
            for (int i = _animationCooldowns.Count - 1; i >= 0; i--)
            {
                var cooldown = _animationCooldowns[i];
                cooldown.Update(deltaTime);
            }
        }

        public void RefreshSnapData(List<CooldownSnapshotData> snapshotData)
        {
            for (var i = _animationCooldowns.Count - 1; i >= 0; i--)
            {
                if (i == snapshotData.Count - 1)
                {
                    _animationCooldowns[i].Reset();
                    break;
                }
                var animationCooldown = _animationCooldowns[i];
                var snapshotCoolDown = snapshotData[i];
                animationCooldown.Refresh(snapshotCoolDown);
            }
        }

        [ClientRpc]
        public void HandlePlayerPropertyDifference(byte[] data)
        {
            if (!isLocalPlayer)
            {
                return;
            }
            var tracedInfo = MemoryPackSerializer.Deserialize<TracedPlayerInfo>(data);

            PlayerComponentController playerComponent;
            if (tracedInfo.PlayerId == connectionToClient.connectionId)
            {
                playerComponent = this;
            }
            else
            {
                var otherPlayer = NetworkServer.connections[tracedInfo.PlayerId].identity.transform;
                playerComponent = otherPlayer.GetComponent<PlayerComponentController>();
                
                var transforms = new List<Transform> { otherPlayer };
                var layerMask = _gameConfigData.groundSceneLayer | _gameConfigData.stairSceneLayer | _playerConfigData.PlayerLayer;
                
                if (PlayerPhysicsCalculator.TryGetPlayersInScreen(_camera, transforms, out var playersInScreen, layerMask))
                {
                    if (playersInScreen.Count == 0)
                    {
                        return;       
                    }
                    var player = playersInScreen[0];
                    if (player != tracedInfo.PlayerId)
                    {
                        return;
                    }
                }
            }

            playerComponent.HandlePlayerPropertyChange(tracedInfo);
        }

        [Client]
        private void HandlePlayerPropertyChange(TracedPlayerInfo tracedInfo)
        {
            var dic = UIPropertyBinder.GetReactiveDictionary<PlayerHpItemData>(_playerTraceOtherPlayerHpBindKey);
            dic[tracedInfo.PlayerId] = new PlayerHpItemData
            {
                PlayerId = tracedInfo.PlayerId,
                PlayerPosition = transform.position,
                TargetPosition = tracedInfo.Position,
                MaxMp = tracedInfo.MaxMana,
                CurrentHp = tracedInfo.Hp,
                CurrentMp = tracedInfo.Mana,
                MaxHp = tracedInfo.MaxHp,
                Name = tracedInfo.PlayerName,
                PropertyType = tracedInfo.PropertyDifferentPropertyType,
                DiffValue = tracedInfo.PropertyDifferentValue,
            };
        }
        
        private readonly Dictionary<PlayerEffectType, GameObject> _effectPool = new Dictionary<PlayerEffectType, GameObject>();
        private readonly Dictionary<PlayerEffectType, PlayerEffectContainer> _effectContainer = new Dictionary<PlayerEffectType, PlayerEffectContainer>();

        [ClientRpc]
        public void RpcPlayEffect(PlayerEffectType effectType)
        {
            if (!isLocalPlayer)
            {
                return;
            }

            if (!_effectPool.TryGetValue(effectType, out var effectPrefab))
            {
                effectPrefab = ResourceManager.Instance.GetResource<GameObject>(effectType.ToString());
                _effectPool.Add(effectType, effectPrefab);
            }
            var prefab = effectPrefab;

            if (!_effectContainer.TryGetValue(effectType, out var container))
            {
                var go = GameObjectPoolManger.Instance.GetObject(prefab, parent: effectContainer);
                container = go.GetComponent<PlayerEffectContainer>();
                _effectContainer.Add(effectType, container);
            }
            var player = container;
            playerEffectPlayer.PlayEffect(player);
        }
    }
}