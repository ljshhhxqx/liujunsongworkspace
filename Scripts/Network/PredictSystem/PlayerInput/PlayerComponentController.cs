using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.Coroutine;
using AOTScripts.Tool.ObjectPool;
using AOTScripts.Tool.Resource;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Effect;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.GameBase;
using HotUpdate.Scripts.Map;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Player;
using HotUpdate.Scripts.Skill;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Panel;
using HotUpdate.Scripts.UI.UIs.Panel.Backpack;
using HotUpdate.Scripts.UI.UIs.Popup;
using HotUpdate.Scripts.UI.UIs.SecondPanel;
using MemoryPack;
using Mirror;
using UI.UIBase;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using VContainer;
using AnimationState = AOTScripts.Data.AnimationState;
using CooldownSnapshotData = AOTScripts.Data.CooldownSnapshotData;
using InputCommand = AOTScripts.Data.InputCommand;
using PlayerAnimationCooldownState = AOTScripts.Data.PlayerAnimationCooldownState;
using PlayerGameStateData = AOTScripts.Data.PlayerGameStateData;
using PlayerPredictablePropertyState = AOTScripts.Data.PlayerPredictablePropertyState;
using PropertyAutoRecoverCommand = AOTScripts.Data.PropertyAutoRecoverCommand;
using PropertyCalculator = AOTScripts.Data.PropertyCalculator;
using PropertyEnvironmentChangeCommand = AOTScripts.Data.PropertyEnvironmentChangeCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PlayerInput
{
    public class PlayerComponentController : NetworkAutoInjectHandlerBehaviour, IAttackAnimationEvent, IEffectPlayer, IAnimationPlayer
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
        private Camera _camera;
        [SerializeField]
        private PlayerEffectPlayer playerEffectPlayer;
        [SerializeField]
        private Transform effectContainer;
        [SerializeField]
        private PlayerControlEffect playerControlEffect;
        [SerializeField]
        private Transform rotateCenter;
        protected override bool AutoInjectClient => false;
        private PropertyConfig _propertyConfig;
        
        [Header("States-NetworkBehaviour")]
        private PlayerInputPredictionState _inputState;
        private PropertyPredictionState _propertyPredictionState;
        private PlayerSkillSyncState _skillSyncState;
        
        [Header("Subject")]
        private readonly Subject<int> _onAttackPoint = new Subject<int>();
        private readonly Subject<int> _onAttackEnd = new Subject<int>();

        private PlayerEnvironmentState _gameState;
        private readonly HReactiveProperty<float> _groundDistanceStream = new HReactiveProperty<float>();
        private readonly HReactiveProperty<bool> _isSpecialActionStream = new HReactiveProperty<bool>();
        
        [Header("Calculators")]
        private PlayerPhysicsCalculator _playerPhysicsCalculator;
        private PlayerPropertyCalculator _playerPropertyCalculator;
        private PlayerAnimationCalculator _playerAnimationCalculator;
        private PlayerBattleCalculator _playerBattleCalculator;
        private PlayerItemCalculator _playerItemCalculator;
        private PlayerElementCalculator _playerElementCalculator;
        private PlayerEquipmentCalculator _playerEquipmentCalculator;
        private PlayerShopCalculator _playerShopCalculator;
        private PlayerSkillCalculator _playerSkillCalculator;
        
        private List<IPlayerStateCalculator> _playerStateCalculators = new List<IPlayerStateCalculator>(8);
        
        [Header("Parameters")]
        private PlayerInputStateData _playerInputStateData;
        private float _currentSpeed;
        private float _targetSpeed;
        private float _speedSmoothTime = 0.1f;
        private float _speedSmoothVelocity;
        private float _sprintSpeedRatio;
        private float _stairsSpeedRatio;
        private bool _isControlled = true;
        private SubjectedStateType _subjectedStateType;
        
        private float _autoRecoverTime;
        private List<IAnimationCooldown> _animationCooldowns = new List<IAnimationCooldown>();
        private List<ISkillChecker> _skillCheckers = new List<ISkillChecker>();
        private SyncDictionary<AnimationState, float> _currentAnimationCooldowns = new SyncDictionary<AnimationState, float>();
        
        private static float FixedDeltaTime => Time.fixedDeltaTime;
        private static float DeltaTime => Time.deltaTime;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private GameSyncManager _gameSyncManager;
        private IConfigProvider _configProvider;
        private GameConfigData _gameConfigData;
        private KeyFunctionConfig _keyFunctionConfig;
        private SkillConfig _skillConfig;
        private PlayerConfigData _playerConfigData;
        private UIManager _uiManager;
        private InteractSystem _interactSystem;
        private UIHoleOverlay _uiHoleOverlay;
        private VirtualInputOverlay _virtualInputOverlay;
        private GameEventManager _gameEventManager;
        private List<PredictableStateBase> _predictionStates = new List<PredictableStateBase>();
        private List<SyncStateBase> _syncStates = new List<SyncStateBase>();
        private Dictionary<AnimationState, ISkillChecker> _skillCheckersDic = new Dictionary<AnimationState, ISkillChecker>();
        private Dictionary<AnimationState, IAnimationCooldown> _animationCooldownsDict = new Dictionary<AnimationState, IAnimationCooldown>();
        private AnimationState _previousAnimationState;
        private KeyframeComboCooldown _attackAnimationCooldown;       
        private PlayerInGameManager _playerInGameManager;
        private NetworkEndHandler _networkEndHandler;
        private Picker _picker;
        public IColliderConfig ColliderConfig { get; private set; }

        private BindingKey _propertyBindKey;
        private BindingKey _itemBindKey;
        private BindingKey _equipBindKey;
        private BindingKey _shopBindKey;
        private BindingKey _goldBindKey;
        private BindingKey _playerDeathTimeBindKey;
        private BindingKey _playerTraceOtherPlayerHpBindKey;
        private BindingKey _minimumBindKey;
        
        private Dictionary<Type, bool> _reactivePropertyBinds = new Dictionary<Type, bool>();
        
        private Vector3 _bornPosition;
        [SyncVar]  
        public int unionId;

        [SyncVar] 
        public bool isDead;

        public int CurrentComboStage { get; private set; }
        public IObservable<int> AttackPointReached => _onAttackPoint;
        public IObservable<int> AttackEnded => _onAttackEnd;

        public Dictionary<AnimationState, IAnimationCooldown> GetAnimationCooldownsDict(AnimationConfig animationConfig)
        {
            if (_animationCooldownsDict.Count == 0)
            {
                if (_animationCooldowns.Count == 0)
                {
                    _animationCooldowns = GetAnimationCooldowns(animationConfig);
                }
                _animationCooldownsDict = _animationCooldowns.ToDictionary(x => x.AnimationState, x => x);
            }
            return _animationCooldownsDict;
        }

        public Dictionary<AnimationState, IAnimationCooldown> AnimationCooldownsDict
        {
            get
            {
                if (_animationCooldownsDict.Count == 0)
                {
                    if (_animationCooldowns.Count > 0)
                    {
                        _animationCooldownsDict = _animationCooldowns.ToDictionary(x => x.AnimationState, x => x);
                    }
                }

                return _animationCooldownsDict;
            }
            set
            {
                _animationCooldownsDict = value;
                _animationCooldowns = value.Values.ToList();
            }
        }
        
        public Dictionary<AnimationState, ISkillChecker> SkillCheckerDict
        {
            get
            {
                if (_skillCheckersDic.Count == 0)
                {
                    if (_skillCheckers.Count > 0)
                    {
                        _skillCheckersDic =
                            _skillCheckers.ToDictionary(x => x.GetCommonSkillCheckerHeader().AnimationState, x => x);
                        return _skillCheckersDic;
                    }
                }
                return _skillCheckersDic;
            }
            set
            {
                _skillCheckersDic = value;
                _skillCheckers = value.Values.ToList();
            }
        }
        
        private bool _isInit;
        
        public void SetPlayerVelocity(Vector3 velocity)
        {
            Debug.Log($"SetPlayerVelocity: {velocity}");
            _rigidbody.velocity = velocity;
        }

        [Inject]
        private void Init(IConfigProvider configProvider, 
            GameSyncManager gameSyncManager, 
            UIManager uiManager,
            GameEventManager gameEventManager,
            PlayerInGameManager playerInGameManager)
        {
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            _configProvider = configProvider;
            _networkEndHandler = FindObjectOfType<NetworkEndHandler>();
            var jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _gameConfigData = jsonDataConfig.GameConfig;
            _playerConfigData = jsonDataConfig.PlayerConfig;
            _keyFunctionConfig = _configProvider.GetConfig<KeyFunctionConfig>();
            _picker = GetComponent<Picker>();
            _gameSyncManager = gameSyncManager;
            _interactSystem = FindObjectOfType<InteractSystem>();
            _uiManager = uiManager;
            _rigidbody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _animator = GetComponent<Animator>();
            _skillConfig = _configProvider.GetConfig<SkillConfig>();
            _camera = Camera.main;
            _animationCooldowns = GetAnimationCooldowns(_configProvider.GetConfig<AnimationConfig>());
            _playerInGameManager = playerInGameManager;
            _gameEventManager = gameEventManager;
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, ColliderConfig, ObjectType.Player, gameObject.layer, gameObject.tag);
            Debug.Log($"[PlayerInGameController] Init: {gameObject.name}");
            _propertyConfig = _configProvider.GetConfig<PropertyConfig>();
            GetAllCalculators(configProvider, gameSyncManager);
            HandleAllSyncState();
            _uiManager.CloseUI(UIType.Main);
        }

        private void OnDevelopItemGet(DevelopItemGetEvent developItemGetEvent)
        {
            CmdSendCommand(NetworkCommandExtensions.SerializeCommand(developItemGetEvent.ItemsGetCommand).Buffer);
        }

        private void OnTargetShow(TargetShowEvent targetShowEvent)
        {
            var isShow = targetShowEvent.Target != null;
            if (!isShow)
            {
                UIPropertyBinder.RemoveFromDictionary<MinimapItemData>(_minimumBindKey, (int)targetShowEvent.TargetId);
            }
            else
            {
                var minimapItemData = new MinimapItemData
                {
                    Id = (int)targetShowEvent.TargetId,
                    TargetType = MinimapTargetType.Chest,
                    WorldPosition = targetShowEvent.Target.transform.position,
                    QualityType = targetShowEvent.Quality,
                };
                UIPropertyBinder.AddToDictionary(_minimumBindKey, (int)targetShowEvent.TargetId, minimapItemData);
            }
        }

        private Vector3 _movement;

        private void OnGameFunctionUIShow(GameFunctionUIShowEvent gameFunctionUIShowEvent)
        {
            if (_uiManager.IsUIOpen(gameFunctionUIShowEvent.UIType))
            {
                _uiManager.CloseUI(gameFunctionUIShowEvent.UIType);
                return;
            }
            switch (gameFunctionUIShowEvent.UIType)
            {
                case UIType.Backpack:
                    var bagItemOverlay = _uiManager.SwitchUI<BackpackScreenUI>();
                    if (!bagItemOverlay)
                    {
                        return;
                    }
                    UIAudioManager.Instance.PlayUIEffect(UIAudioEffectType.Bag);
                    bagItemOverlay.BindBagItemData(UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemBindKey));
                    break;
                case UIType.Shop:
                    var shopScreenUI = _uiManager.SwitchUI<ShopScreenUI>();
                    if (!shopScreenUI)
                    {
                        return;
                    }

                    shopScreenUI.BindShopItemData(UIPropertyBinder.GetReactiveDictionary<RandomShopItemData>(_shopBindKey));
                    shopScreenUI.BindBagItemData(UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemBindKey));
                    shopScreenUI.BindPlayerGold(UIPropertyBinder.ObserveProperty<ValuePropertyData>(_goldBindKey));
                    UIAudioManager.Instance.PlayUIEffect(UIAudioEffectType.Bag);
                    shopScreenUI.OnRefresh.Subscribe(_ =>
                    {
                        var refreshCommand = new RefreshShopCommand
                        {
                            Header = GameSyncManager.CreateNetworkCommandHeader(_playerInGameManager.LocalPlayerId, CommandType.Shop, CommandAuthority.Client),
                        };
                        CmdSendCommand(NetworkCommandExtensions.SerializeCommand(refreshCommand).Buffer);
                    }).AddTo(shopScreenUI.gameObject);
                    break;
                case UIType.PlayerInGameInfo:
                    _propertyPredictionState.OpenPlayerInGameInfo();
                    break;
                default:
                    Debug.LogWarning($"Not support UIType: {gameFunctionUIShowEvent.UIType}");
                    break;
            }
        }
        
        private Minimap _minimap;
        private PlayerInputStateData _lastInputStateData;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();

        protected override void InjectLocalPlayerCallback()
        {
            Debug.Log($"[PlayerInputController] OnStartLocalPlayer");
            _gameEventManager.Publish(new PlayerUnListenMessageEvent());
            _gameEventManager.Publish(new PlayerSpawnedEvent(rotateCenter, gameObject, netId, true));
            _gameEventManager.Subscribe<DevelopItemGetEvent>(OnDevelopItemGet);
            _gameEventManager.Subscribe<GameFunctionUIShowEvent>(OnGameFunctionUIShow);
            _gameEventManager.Subscribe<TargetShowEvent>(OnTargetShow);
            var attackSector = gameObject.GetComponent<AttackSectorLine>();
            var radius = _propertyConfig.GetBaseValue(PropertyTypeEnum.AttackRadius);
            var range = _propertyConfig.GetBaseValue(PropertyTypeEnum.AttackAngle);
            var height = _propertyConfig.GetBaseValue(PropertyTypeEnum.AttackHeight);
            attackSector.SetParams(new AttackConfigData
            {
                AttackRadius = radius,
                AttackRange = range,
                AttackHeight = height,
            });
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
            _minimumBindKey = new BindingKey(UIPropertyDefine.MinimumValue, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);
            
            Observable.EveryUpdate()
                .Where(x=> LocalPlayerHandler)
                .Subscribe(_ =>
                {
                    try
                    {
                        GetFunctionButton();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                    try
                    {
                        GetInput();
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                })
                .AddTo(this);
            
            Observable.EveryUpdate()
                .Where(x=> LocalPlayerHandler)
                .Sample(TimeSpan.FromSeconds(Time.fixedDeltaTime))
                .Subscribe(_ =>
                {
                    HandleNetworkCommand();
                })
                .AddTo(this);
            Observable.EveryFixedUpdate()
                .Where(x=> LocalPlayerHandler)
                .Subscribe(_ =>
                {
                    try
                    {
                        HandleInputPhysics(_playerInputStateData);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        throw;
                    }
                })
                .AddTo(this);
            
            HandleLocalInitCallback();
        }

        private void GetOtherPlayerPosition()
        {
            var potentialTargets = new List<Transform>();
            UIPropertyBinder.UpdateDictionary(_minimumBindKey, (int)netId, new MinimapItemData
            {
                Id = (int)netId,
                TargetType = MinimapTargetType.Player,
                WorldPosition = transform.position,
            });
            foreach (var networkIdentity in NetworkClient.spawned.Values)
            {
                if (networkIdentity.TryGetComponent<PlayerComponentController>(out var component) && networkIdentity.netId != netId)
                {
                    potentialTargets.Add(component.transform);
                }
            }
            if (potentialTargets.Count == 0)
            {
                return;
            }
            var layerMask = _gameConfigData.groundSceneLayer | _gameConfigData.stairSceneLayer | _playerConfigData.PlayerLayer;
            if (PlayerPhysicsCalculator.TryGetPlayersInScreen(_camera, potentialTargets, out var playersInScreen, layerMask))
            {
                var header = GameSyncManager.CreateNetworkCommandHeader(_playerInGameManager.LocalPlayerId,
                    CommandType.Property, CommandAuthority.Client);
                var playerInScreenCommand = new PlayerTraceOtherPlayerHpCommand
                {
                    Header = header,
                    TargetConnectionIds = playersInScreen.ToArray(),
                };
                foreach (var player in playersInScreen)
                {
                    if (NetworkClient.spawned.TryGetValue(player, out var playerObject))
                    {
                        UIPropertyBinder.UpdateDictionary(_minimumBindKey, (int)netId, new MinimapItemData
                        {
                            Id = (int)player,
                            TargetType = MinimapTargetType.Enemy,
                            WorldPosition = playerObject.transform.position
                        });
                    }
                }
            
                CmdSendCommand(NetworkCommandExtensions.SerializeCommand(playerInScreenCommand).Buffer);
            }
        }

        private void SendNetworkCommand()
        {
            //Debug.Log($"[PlayerComponentController] Start -*-- {_propertyPredictionState} -- {_propertyPredictionState.PlayerPredictablePropertyState}");
            _targetSpeed = _propertyPredictionState.GetMoveSpeed();
            //Debug.Log($"[PlayerComponentController] TargetSpeed: {_targetSpeed}");
            //Debug.Log($"[PlayerComponentController] _gameSyncManager.isGameOver: {_gameSyncManager.isGameOver} _picker.IsTouching: {_picker.IsTouching} _propertyPredictionState.GetProperty(PropertyTypeEnum.Health): {_propertyPredictionState.GetProperty(PropertyTypeEnum.Health)} GameSyncManager.CurrentTick: {GameSyncManager.CurrentTick} _subjectedStateType: {_subjectedStateType} ");
            if (_gameSyncManager.isGameOver || _picker.IsTouching || _propertyPredictionState.GetProperty(PropertyTypeEnum.Health) <= 0 ||
                GameSyncManager.CurrentTick <= 0 || !(_subjectedStateType.HasAllStates(SubjectedStateType.None) || _subjectedStateType.HasAllStates(SubjectedStateType.IsInvisible)) || 
                _subjectedStateType.HasAnyState(SubjectedStateType.IsCantMoved))
            {
                Debug.Log("[PlayerComponentController] SendNetworkCommand: Idle");
                _playerInputStateData.Command = AnimationState.Idle;
                _playerInputStateData.InputAnimations = AnimationState.Idle;
                _playerInputStateData.InputMovement = default;
                _playerInputStateData.Velocity = default;
                _targetSpeed = 0;
                //Debug.Log("[PlayerComponentController] SendNetworkCommand: End");
            }
            //Debug.Log($"[PlayerComponentController] _playerInputStateData: {_playerInputStateData}");
            if (_playerInputStateData != default)
            {
                //Debug.Log($"[PlayerComponentController] SendNetworkCommand: {_playerInputStateData} Start");
                HandleSendNetworkCommand(_playerInputStateData);
                var propertyEnvironmentChangeCommand = new PropertyEnvironmentChangeCommand();
                propertyEnvironmentChangeCommand.Header = GameSyncManager.CreateNetworkCommandHeader(_playerInGameManager.LocalPlayerId,
                    CommandType.Property, CommandAuthority.Client, CommandExecuteType.Predicate, NetworkCommandType.PropertyEnvironmentChange);
                propertyEnvironmentChangeCommand.HasInputMovement = _playerInputStateData.InputMovement.magnitude > 0.1f;
                propertyEnvironmentChangeCommand.PlayerEnvironmentState = _gameState;
                propertyEnvironmentChangeCommand.IsSprinting = _playerInputStateData.Command.HasAnyState(AnimationState.Sprint);
                _propertyPredictionState.AddPredictedCommand(propertyEnvironmentChangeCommand);
                //Debug.Log($"[PlayerComponentController] SendNetworkCommand: {_playerInputStateData} End");
            }
            //Debug.Log("[PlayerComponentController] End");
        }

        private void GetFunctionButton()
        {
            if (_keyFunctionConfig.IsKeyFunction(out var keyFunction))
            {
                var uiType = keyFunction.GetUIType();
                _gameEventManager.Publish(new GameFunctionUIShowEvent(uiType));
            }
        }

        private void GetInput()
        {
            if (PlayerPlatformDefine.IsWindowsPlatform())
            {
                _movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
                if (_movement.magnitude == 0)
                {
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.FootStep);
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.Sprint);
                }
                if (Cursor.lockState != CursorLockMode.Locked || !_isControlled)
                {
                    //Debug.Log("Cursor is locked or not controlled");
                    return;
                }
                var animationStates = _inputState.GetAnimationStates();
                var playerInputStateData = new PlayerInputStateData
                {
                    InputMovement = _movement,
                    InputAnimations = animationStates,
                };
                var command = GetCurrentAnimationState(playerInputStateData);
                        
                if (!_playerAnimationCalculator.CanPlayAnimation(command))
                {
                    command = AnimationState.None;
                }
                playerInputStateData.Command = command;
                if (_animationCooldownsDict.TryGetValue(command, out var animationCooldown))
                {
                    playerInputStateData.Command = animationCooldown.IsReady() ? command : AnimationState.None;
                }
                _playerInputStateData = playerInputStateData;
            }
            else if (PlayerPlatformDefine.IsJoystickPlatform())
            {
                _virtualInputOverlay ??= _inputState.VirtualPlayerAnimationOverlay;
                _movement = _virtualInputOverlay ? _virtualInputOverlay.GetMovementInput() : Vector3.zero;
                var isSprinting = _virtualInputOverlay && _virtualInputOverlay.IsSprinting();
                if (_virtualInputOverlay && Mathf.Approximately(_movement.magnitude, 1))
                {
                    Debug.Log($"[PlayerComponentController] GetInput: {_movement}");
                    return;
                }
                if (_movement.magnitude == 0)
                {
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.FootStep);
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.Sprint);
                }

                var animationStates = _virtualInputOverlay.ActiveButtons.FirstOrDefault();
                if (isSprinting)
                {
                    animationStates = animationStates != default
                        ? animationStates.AddState(AnimationState.Sprint)
                        : AnimationState.Sprint;
                }

                var playerInputStateData = new PlayerInputStateData
                {
                    InputMovement = _movement,
                    InputAnimations = animationStates,
                };
                var command = GetCurrentAnimationState(playerInputStateData);
                if (!_playerAnimationCalculator.CanPlayAnimation(command))
                {
                    command = AnimationState.None;
                }

                playerInputStateData.Command = command;
                if (_animationCooldownsDict.TryGetValue(command, out var animationCooldown))
                {
                    playerInputStateData.Command = animationCooldown.IsReady() ? command : AnimationState.None;
                    {
                        _playerInputStateData = playerInputStateData;
                    }
                }
            }
        }

        private void AutoRecover()
        {
            var health = _propertyPredictionState.GetCalculator(PropertyTypeEnum.Health);
            var strength = _propertyPredictionState.GetCalculator(PropertyTypeEnum.Strength);
            if (Mathf.Approximately(health.CurrentValue, health.MaxCurrentValue) && Mathf.Approximately(strength.CurrentValue, strength.MaxCurrentValue))
            {
                return;
            }
            var propertyAutoRecoverCommand = new PropertyAutoRecoverCommand();
            propertyAutoRecoverCommand.Header = GameSyncManager.CreateNetworkCommandHeader(
                _playerInGameManager.LocalPlayerId,
                CommandType.Property, CommandAuthority.Client, CommandExecuteType.Predicate,
                NetworkCommandType.PropertyAutoRecover);
            _propertyPredictionState.AddPredictedCommand(propertyAutoRecoverCommand);
        }

        private void HandleAllSyncState()
        {
            var states = GetComponents<PredictableStateBase>();
            var syncStates = GetComponents<SyncStateBase>();
            for (int i = 0; i < states.Length; i++)
            {
                _predictionStates.Add(states[i]);
            }

            for (int i = 0; i < syncStates.Length; i++)
            {
                _syncStates.Add(syncStates[i]);
            }
            _inputState = GetComponent<PlayerInputPredictionState>();
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _skillSyncState = GetComponent<PlayerSkillSyncState>();
            _propertyPredictionState.OnPropertyChanged += HandlePropertyChange;
            _propertyPredictionState.OnStateChanged += HandlePropertyStateChanged;
            //_propertyPredictionState.OnPlayerDead += HandlePlayerDeadClient;
            //_propertyPredictionState.OnPlayerRespawned += HandlePlayerRespawnedClient;
            _inputState.OnPlayerStateChanged += HandlePlayerStateChanged;
            _inputState.OnPlayerAnimationCooldownChanged += HandlePlayerAnimationCooldownChanged;
            _inputState.OnPlayerInputStateChanged += HandlePlayerInputStateChanged;
            _inputState.IsInSpecialState += HandleSpecialState;
            _uiManager.IsUnlockMouse += OnIsUnlockMouse;
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
                case SubjectedStateType.IsStoned:
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
            }
        }

        private void HandleLocalInitCallback()
        {
            _uiManager.CloseUI(UIType.Loading);
            _uiManager.CloseUI(UIType.TipsPopup);
            _uiManager.SwitchUI<DevelopSwitchUI>();
            _uiHoleOverlay = _uiManager.SwitchUI<UIHoleOverlay>();
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
                var playerHpShowOverlay = _uiManager.SwitchUI<PlayerHpShowOverlay>();
                playerHpShowOverlay.BindPlayersHp(UIPropertyBinder.GetReactiveDictionary<PlayerHpItemData>(_playerTraceOtherPlayerHpBindKey), followData);
            }
            _uiHoleOverlay.gameObject.SetActive(false);

            _uiManager.SwitchUI<TargetShowOverlay>();
            _uiManager.SwitchUI<Minimap>(ui =>
            {
                ui.BindPositions(UIPropertyBinder.GetReactiveDictionary<MinimapItemData>(_minimumBindKey));
            });
        }
        

        
        private bool HandleSpecialState()
        {
            //Debug.Log($"[HandleSpecialState] -> {_playerAnimationCalculator.IsSpecialAction}");
            return _playerAnimationCalculator.IsSpecialAction;
        }

        [ClientCallback]
        private void HandlePlayerInputStateChanged(PlayerInputStateData playerInputStateData)
        {
            //Debug.Log("HandlePlayerInputStateChanged");
            HandleClientMoveAndAnimation(playerInputStateData);
        }

        [ClientCallback]
        private void HandlePlayerAnimationCooldownChanged(PlayerAnimationCooldownState newCooldownState)
        {
            
        }

        [ClientCallback]
        private void HandlePlayerStateChanged(PlayerGameStateData newState)
        {
            // transform.position = newState.Position;
            // transform.rotation = newState.Quaternion;
            // _rigidbody.velocity = newState.Velocity;
            // _gameStateStream.Value = newState.PlayerEnvironmentState;
            // _playerAnimationCalculator.SetEnvironmentState(newState.PlayerEnvironmentState);
            if(LocalPlayerHandler)
                return;
            
            _playerAnimationCalculator.PlayAnimationWithNoCondition(newState.AnimationState, newState.Index);
        }

        private void HandleSendNetworkCommand(PlayerInputStateData inputData)
        {
            if (_previousAnimationState == inputData.Command && 
                _previousAnimationState!= AnimationState.Idle && 
                _previousAnimationState!= AnimationState.Move && 
                _previousAnimationState!= AnimationState.Sprint)
            {
                return;
            }

            if (inputData.Command == AnimationState.SkillE || inputData.Command == AnimationState.SkillQ)
            {
                if (!_skillSyncState.IsSkillExist(inputData.Command))
                {
                    return;
                }
            }

            var inputCommand = new InputCommand();
            
            _previousAnimationState = inputData.Command;
            inputCommand.InputMovement = CompressedVector3.FromVector3(inputData.InputMovement);
            inputCommand.Header = GameSyncManager.CreateNetworkCommandHeader( _playerInGameManager.LocalPlayerId,
                CommandType.Input, CommandAuthority.Client, CommandExecuteType.Predicate, NetworkCommandType.Input);
            inputCommand.InputAnimationStates = inputData.InputAnimations; 
            inputCommand.CommandAnimationState = inputData.Command;
            _inputState.AddPredictedCommand(inputCommand);
        }

        public void PlayerAddCommand<T>(CommandType commandType, T command) where T : INetworkCommand
        {
            switch (commandType)
            {
                case CommandType.Property:
                    _propertyPredictionState.AddPredictedCommand(command);
                    break;
                case CommandType.Input:
                    _inputState.AddPredictedCommand(command);
                    break;
                case CommandType.Skill:
                    _skillSyncState.AddPredictedCommand(command);
                    break;
            }
        }

        private readonly HashSet<uint> _cachedDynamicObjectData = new HashSet<uint>();

        private void HandleInputPhysics(PlayerInputStateData inputData)
        {
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _playerPhysicsCalculator.CurrentSpeed = _currentSpeed;
            _gameState = _playerPhysicsCalculator.CheckPlayerState(new CheckGroundDistanceParam(inputData.InputMovement, FixedDeltaTime));
            _groundDistanceStream.Value = _playerPhysicsCalculator.GroundDistance;
            _isSpecialActionStream.Value = _playerAnimationCalculator.IsSpecialAction;
            _playerAnimationCalculator.SetEnvironmentState(_gameState);
            _playerAnimationCalculator.SetGroundDistance(_groundDistanceStream.Value);
            _playerAnimationCalculator.SetAnimatorParams(inputData.InputMovement.magnitude, _groundDistanceStream.Value, _currentSpeed);
            _playerAnimationCalculator.UpdateAnimationState();
            if (GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, ColliderConfig, _cachedDynamicObjectData))
            {
                foreach (var data in _cachedDynamicObjectData)
                {
                    var objectData = GameObjectContainer.Instance.GetDynamicObjectData(data);
                    if (objectData.Type == ObjectType.Base)
                    {
                        var header = GameSyncManager.CreateNetworkCommandHeader(_playerInGameManager.LocalPlayerId,
                            CommandType.Property, CommandAuthority.Client);
                        var playerTouchedBaseCommand = new PlayerTouchedBaseCommand
                        {
                            Header = header,
                        };
                        CmdSendCommand(NetworkCommandExtensions.SerializeCommand(playerTouchedBaseCommand).Buffer);
                        break;
                    }
                }
            }
            //Debug.Log($"[HandleInputPhysics]- _currentSpeed > {_currentSpeed} _targetSpeed ->{_targetSpeed} _speedSmoothVelocity -> {_speedSmoothVelocity} _speedSmoothTime -> {_speedSmoothTime}");
           
        }

        [ClientRpc]
        public void RpcHandlePlayerSpecialAction(AnimationState animationState)
        {
            // if (isLocalPlayer)
            // {
            //     return;
            // }
            HandlePlayerSpecialAction(animationState);
        }

        public void HandlePlayerSpecialAction(AnimationState animationState)
        {
            if (animationState is AnimationState.Move or AnimationState.None or AnimationState.Sprint or AnimationState.Idle)
            {
                //Debug.Log($"[HandlePlayerSpecialAction] Animation State: {animationState}");
                return;
            }
            //Debug.Log($"[HandlePlayerSpecialAction] Animation State: {animationState}");
            switch (animationState)
            {
                case AnimationState.Jump:
                case AnimationState.SprintJump:
                    _playerPhysicsCalculator.HandlePlayerJump();
                    break;
                case AnimationState.Roll:
                    _rigidbody.velocity = Vector3.zero;
                    _playerPhysicsCalculator.HandlePlayerRoll();
                    break;
            }
        }

        private void HandlePropertyChange(PropertyTypeEnum propertyType, PropertyCalculator value)
        {
            _playerPropertyCalculator.UpdateProperty(propertyType, value);
        }

        private void GetAllCalculators(IConfigProvider configProvider, GameSyncManager gameSyncManager)
        {
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
                RollForce = playerData.RollForce,
                JumpSpeed = playerData.JumpSpeed,
                SpeedToVelocityRatio = playerData.SpeedToVelocityRatio,
            });
            PlayerPropertyCalculator.SetCalculatorConstant(new PropertyCalculatorConstant
            {
                TickRate = GameSyncManager.TickSeconds,
                PropertyConfig =   configProvider.GetConfig<PropertyConfig>(),
                PlayerConfig = configProvider.GetConfig<JsonDataConfig>().PlayerConfig,
            });
            PlayerAnimationCalculator.SetAnimationConstant(new AnimationConstant
            {
                MaxGroundDistance = gameData.groundMaxDistance,
                InputThreshold = gameData.inputThreshold,
                AttackComboMaxCount = playerData.AttackComboMaxCount,
                AnimationConfig = configProvider.GetConfig<AnimationConfig>(),
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
                ConstantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>(),
                RandomBuffConfig = configProvider.GetConfig<RandomBuffConfig>(),
                SkillConfig = configProvider.GetConfig<SkillConfig>(),
                PlayerComponentController = this,
            });
            PlayerEquipmentCalculator.SetConstant(new PlayerEquipmentConstant
            {
                GameSyncManager = gameSyncManager,
                ItemConfig = configProvider.GetConfig<ItemConfig>(),
                SkillConfig = _configProvider.GetConfig<SkillConfig>(),
                
            });
            PlayerShopCalculator.SetConstant(new ShopCalculatorConstant
            {
                GameSyncManager = gameSyncManager,
                ShopConfig = configProvider.GetConfig<ShopConfig>(),
                ItemConfig = configProvider.GetConfig<ItemConfig>(),
                PlayerInGameManager = _playerInGameManager,
                PlayerConfigData = _playerConfigData,
                UIManager = _uiManager,
            });
            PlayerSkillCalculator.SetConstant(new SkillCalculatorConstant
            {
                GameSyncManager = gameSyncManager,
                SkillConfig = configProvider.GetConfig<SkillConfig>(),
                SceneLayerMask = gameData.stairSceneLayer,
                CasterId = netId,
                PlayerInGameManager = _playerInGameManager,
            });
            _playerPhysicsCalculator = new PlayerPhysicsCalculator(new PhysicsComponent(_rigidbody, transform, _checkStairTransform, _capsuleCollider, _camera));
            _playerPropertyCalculator = new PlayerPropertyCalculator(PlayerPropertyCalculator.GetPropertyCalculators());
            _playerAnimationCalculator = new PlayerAnimationCalculator(new AnimationComponent{ Animator = _animator});
            _playerBattleCalculator = new PlayerBattleCalculator(new PlayerBattleComponent(transform, _interactSystem));
            _playerItemCalculator = new PlayerItemCalculator();
            _playerElementCalculator = new PlayerElementCalculator();
            _playerEquipmentCalculator = new PlayerEquipmentCalculator();
            _playerShopCalculator = new PlayerShopCalculator();
            _playerSkillCalculator = new PlayerSkillCalculator();
            _playerStateCalculators.Add(_playerPhysicsCalculator);
            _playerStateCalculators.Add(_playerPropertyCalculator);
            _playerStateCalculators.Add(_playerAnimationCalculator);
            _playerStateCalculators.Add(_playerBattleCalculator);
            _playerStateCalculators.Add(_playerItemCalculator);
            _playerStateCalculators.Add(_playerElementCalculator);
            _playerStateCalculators.Add(_playerEquipmentCalculator);
            _playerStateCalculators.Add(_playerShopCalculator);
            _playerStateCalculators.Add(_playerSkillCalculator);
            var shopConstant = PlayerShopCalculator.Constant;
            shopConstant.IsServer = isServer;
            shopConstant.IsClient = isClient;
            shopConstant.IsLocalPlayer = isLocalPlayer;
            PlayerShopCalculator.SetConstant(shopConstant);
            var equipConstant = PlayerEquipmentCalculator.Constant;
            equipConstant.IsServer = isServer;
            equipConstant.IsClient = isClient;
            equipConstant.IsLocalPlayer = isLocalPlayer;
            PlayerEquipmentCalculator.SetConstant(equipConstant);
            var itemConstant = PlayerItemCalculator.Constant;
            itemConstant.IsServer = isServer;
            itemConstant.IsClient = isClient;
            itemConstant.IsLocalPlayer = isLocalPlayer;
            PlayerItemCalculator.SetConstant(itemConstant);
            var propertyConstant = PlayerPropertyCalculator.CalculatorConstant;
            propertyConstant.IsServer = isServer;
            propertyConstant.IsClient = isClient;
            propertyConstant.IsLocalPlayer = isLocalPlayer;
            PlayerPropertyCalculator.SetCalculatorConstant(propertyConstant);
            var physicsConstant = PlayerPhysicsCalculator.PhysicsDetermineConstant;
            physicsConstant.IsServer = isServer;
            physicsConstant.IsClient = isClient;
            physicsConstant.IsLocalPlayer = isLocalPlayer;
            PlayerPhysicsCalculator.SetPhysicsDetermineConstant(physicsConstant);
            var animationConstant = PlayerAnimationCalculator.AnimationConstant;
            animationConstant.IsServer = isServer;
            animationConstant.IsClient = isClient;
            animationConstant.IsLocalPlayer = isLocalPlayer;
            PlayerAnimationCalculator.SetAnimationConstant(animationConstant);
            var constant = PlayerSkillCalculator.Constant;
            constant.IsServer = isServer;
            PlayerSkillCalculator.SetConstant(constant);
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
            _gameEventManager.Publish(new PlayerSpawnedEvent(rotateCenter, gameObject, netId, false));
            GameObjectContainer.Instance.RemoveDynamicObject(netId);
            _disposables?.Clear();
            _propertyPredictionState.OnPropertyChanged -= HandlePropertyChange;
            _propertyPredictionState.OnStateChanged -= HandlePropertyStateChanged;
            // _propertyPredictionState.OnPlayerDead -= HandlePlayerDeadClient;
            // _propertyPredictionState.OnPlayerRespawned -= HandlePlayerRespawnedClient;
            _inputState.OnPlayerStateChanged -= HandlePlayerStateChanged;
            _inputState.OnPlayerAnimationCooldownChanged -= HandlePlayerAnimationCooldownChanged;
            _inputState.OnPlayerInputStateChanged -= HandlePlayerInputStateChanged;
            _inputState.IsInSpecialState -= HandleSpecialState;
            _uiManager.IsUnlockMouse -= OnIsUnlockMouse;
            _gameEventManager.Unsubscribe<DevelopItemGetEvent>(OnDevelopItemGet);
            _gameEventManager.Unsubscribe<GameFunctionUIShowEvent>(OnGameFunctionUIShow);
            _gameEventManager.Unsubscribe<TargetShowEvent>(OnTargetShow);
            _animationCooldowns.Clear();
            _effectContainer.Clear();
            playerEffectPlayer.StopAllEffect(container => GameObjectPoolManger.Instance.ReturnObject(container.gameObject));
            _effectPool.Clear();
        }

        private void OnIsUnlockMouse(bool isUnlock)
        {
            //Debug.Log($"[OnIsUnlockMouse] isUnlock ->{isUnlock}");
            _isControlled = !isUnlock;
        }

        [ClientRpc]
        public void RpcHandlePlayerDeadClient(float countdownTime)
        {
            _playerAnimationCalculator.PlayAnimationWithNoCondition(AnimationState.Dead);
            var playerDamageDeathOverlay = _uiManager.GetActiveUI<PlayerDamageDeathOverlay>(UIType.PlayerDamageDeathOverlay, UICanvasType.Overlay);
            playerDamageDeathOverlay.PlayDeathEffect(countdownTime);
            Debug.Log($"[RpcHandlePlayerDeadClient] {netId}===countdownTime ->{countdownTime}");
        }

        [ClientRpc]
        public void RpcHandlePlayerRespawnedClient()
        {
            _playerAnimationCalculator.PlayAnimationWithNoCondition(AnimationState.Idle);
            var playerDamageDeathOverlay = _uiManager.GetActiveUI<PlayerDamageDeathOverlay>(UIType.PlayerDamageDeathOverlay, UICanvasType.Overlay);
            Debug.Log($"[RpcHandlePlayerRespawnedClient] {netId}");
            playerDamageDeathOverlay.Clear();
        }

        public AnimationState GetCurrentAnimationState(PlayerInputStateData inputData)
        {
            var stateParams = CreateDetermineAnimationStateParams(inputData);
            //Debug.Log($"[GetCurrentAnimationState] stateParams.InputMovement ->{stateParams.InputMovement} stateParams.InputAnimationStates.Count ->{stateParams.InputAnimationStates} stateParams.GroundDistance->{stateParams.GroundDistance} stateParams.EnvironmentState->{stateParams.EnvironmentState}");
            return _playerAnimationCalculator.DetermineAnimationState(stateParams);
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
            var param = ObjectPoolManager<DetermineAnimationStateParams>.Instance.Get(15);
            param.InputMovement = inputData.InputMovement;
            param.InputAnimationStates = inputData.InputAnimations;
            param.GroundDistance = _groundDistanceStream.Value;
            param.EnvironmentState = _gameState;
            ObjectPoolManager<DetermineAnimationStateParams>.Instance.Return(param);
            
            return param;    
            
        }

        private PlayerGameStateData HandleMoveAndAnimation(PlayerInputStateData inputData)
        {
           // Debug.Log($"[HandleMoveAndAnimation]- inputData.InputMovement ->{inputData.InputMovement} inputData.InputAnimations.Count ->{inputData.InputAnimations} inputData.Command->{inputData.Command}");
            
            if (PlayerAnimationCalculator.IsMovingState(inputData.Command))
            {
                var cameraForward = Vector3.Scale(_camera.transform.forward, new Vector3(1, 0, 1)).normalized;
                //移动
                var movePara = ObjectPoolManager<MoveParam>.Instance.Get(30);
                movePara.InputMovement = inputData.InputMovement;
                movePara.IsMovingState = PlayerAnimationCalculator.IsMovingState(inputData.Command);
                movePara.CameraForward = _playerPhysicsCalculator.CompressYaw(cameraForward.y);
                movePara.IsClearVelocity = PlayerAnimationCalculator.IsClearVelocity(inputData.Command);
                movePara.DeltaTime = FixedDeltaTime;
                ObjectPoolManager<MoveParam>.Instance.Return(movePara);
                _playerPhysicsCalculator.HandleMove(movePara, isLocalPlayer);
            }
            //执行动画
            _playerAnimationCalculator.HandleAnimation(inputData.Command, _attackAnimationCooldown.CurrentStage);
            var data = new PlayerGameStateData();// ObjectPoolManager<PlayerGameStateData>.Instance.Get(20);
            data.Position = transform.position;
            data.Quaternion = transform.rotation;
            data.Velocity = _rigidbody.velocity;
            data.PlayerEnvironmentState = _gameState;
            data.AnimationState = inputData.Command;
            data.Index = _attackAnimationCooldown.CurrentStage;
            return data;
        }
        
        [ClientRpc]
        public void RpcPlayAudioEffect(AnimationState command)
        {
            if (LocalPlayerHandler) return;
            PlayEffect(command);
        }

        private void PlayEffect(AnimationState command)
        {
            switch (command)
            {
                case AnimationState.Move:
                    if (_targetSpeed == 0)
                        break;
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.Sprint);
                    GameAudioManager.Instance.PlayLoopingMusic(AudioEffectType.FootStep, transform.position, transform);
                    break;
                case AnimationState.Sprint:
                    if (_targetSpeed == 0)
                        break;
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.FootStep);
                    GameAudioManager.Instance.PlayLoopingMusic(AudioEffectType.Sprint, transform.position, transform);
                    break;
                case AnimationState.Jump:
                case AnimationState.SprintJump:
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.Sprint);
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.FootStep);
                    GameAudioManager.Instance.PlaySFX(AudioEffectType.Jump, transform.position, transform);
                    break;
                case AnimationState.Dead:
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.Sprint);
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.FootStep);
                    GameAudioManager.Instance.PlaySFX(AudioEffectType.Die, transform.position, transform);
                    break;
                case AnimationState.Hit:
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.Sprint);
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.FootStep);
                    GameAudioManager.Instance.PlaySFX(AudioEffectType.Hurt, transform.position, transform);
                    break;
                default:
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.Sprint);
                    GameAudioManager.Instance.StopLoopingMusic(AudioEffectType.FootStep);
                    break;
            }
        }

        public PlayerGameStateData HandleClientMoveAndAnimation(PlayerInputStateData inputData)
        {
            //Debug.Log($"[HandleClientMoveAndAnimation] start");
            PlayEffect(inputData.Command);
            return HandleMoveAndAnimation(inputData);
        }

        //这一行开始，写对外接口
        public PlayerGameStateData HandleServerMoveAndAnimation(PlayerInputStateData inputData)
        {
            //Debug.Log($"[HandleServerMoveAndAnimation] start");
            //_inputStream.OnNext(inputData);
            return HandleMoveAndAnimation(inputData);
        }

        public void HandlePropertyRecover(ref PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            if (_playerInGameManager.IsPlayerDead(netId, out _))
            {
                return;
            }
            _playerPropertyCalculator.HandlePropertyRecover(ref playerPredictablePropertyState);
        }

        public void HandleAnimationCost(ref PlayerPredictablePropertyState playerPredictablePropertyState, AnimationState animationState, float cost)
        {
            _playerPropertyCalculator.HandleAnimationCommand(ref playerPredictablePropertyState, animationState, cost);
        }

        public void HandleEnvironmentChange(ref PlayerPredictablePropertyState playerPredictablePropertyState, bool hasInputMovement, PlayerEnvironmentState environmentType, bool isSprinting)
        {
            _playerPropertyCalculator.HandleEnvironmentChange(ref playerPredictablePropertyState, hasInputMovement, environmentType, isSprinting);
        }

        public HashSet<uint> HandleAttack(AttackParams attackParams)
        {
            return _playerBattleCalculator.IsInAttackRange(attackParams);
        }

        public PlayerPredictablePropertyState HandlePlayerDie(PlayerPredictablePropertyState playerPredictablePropertyState, float countdownTime)
        {
            var diePlayerState = _playerPropertyCalculator.HandlePlayerDeath(playerPredictablePropertyState);
            isDead = true;
            return diePlayerState;
        }

        public PlayerPredictablePropertyState HandlePlayerRespawn(
            PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            var playerState = _playerPropertyCalculator.HandlePlayerRespawn(playerPredictablePropertyState);
            isDead = false;
            return playerState;
        }

        public void HandleTracedPlayerHp(int connectionId, MemoryList<TracedPlayerInfo> tracedInfo)
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
            var info = BoxingFreeSerializer.MemoryDeserialize<MemoryList<TracedPlayerInfo>>(data);
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
                        Header = InteractSystem.CreateInteractHeader(_playerInGameManager.LocalPlayerId, InteractCategory.PlayerToPlayer),
                        KillerPlayerId = killerPlayerId,
                        DeadPlayerId = victimPlayerId,
                    };
                    var changeUnionRequest = MemoryPackSerializer.Serialize(playerChangeUnionCommand);
                    CmdChangePlayerUnion(changeUnionRequest);
                }, () =>
                {
                    _uiManager.CloseUI(UIType.TipsPopup);
                });
            });
        }
        
        [Command]
        public void CmdChangePlayerUnion(byte[] data)
        {
            var playerChangeUnionCommand = BoxingFreeSerializer.MemoryDeserialize<PlayerChangeUnionRequest>(data);
            _interactSystem.EnqueueCommand(playerChangeUnionCommand);
        }

        [ClientRpc]
        public void RpcSetPlayerInfo(float hp, float mp, float maxHp, float maxMp, uint playerId, string playerName)
        {
            _gameEventManager.Publish(new PlayerInfoChangedEvent(hp, mp, maxHp, maxMp, playerId, playerName));
        }

        [ClientRpc]
        public void RpcSetPlayerAlpha(float alpha)
        {
            var actualAlpha = alpha * 0.001f;
            if(Mathf.Approximately(actualAlpha, 1))
                return;
            playerControlEffect.SetTransparency( 1 -actualAlpha);
        }

        [ClientRpc]
        public void RpcSetAnimatorSpeed(AnimationState animationState, float speed)
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
        
        private List<IAnimationCooldown> GetAnimationCooldowns(AnimationConfig config)
        {
            var list = new List<IAnimationCooldown>();
            var animations = config.AnimationInfos;
            for (int i = 0; i < animations.Count; i++)
            {
                var info = animations[i];
                if (!info.showInHud)
                {
                    continue;
                }
                IAnimationCooldown cooldown = null;
                switch (info.cooldownType)
                {
                    case CooldownType.Normal:
                        cooldown = new AnimationCooldown(info.state, info.cooldown, info.animationSpeed);
                        break;
                    case CooldownType.KeyFrame:
                        cooldown = new KeyframeCooldown(info.state, info.cooldown, info.keyframeData, info.animationSpeed);
                        break;
                    case CooldownType.Combo:
                        cooldown = new ComboCooldown(info.state, info.keyframeData.Select(x => x.resetCooldownWindowTime).ToList(), info.cooldown, info.animationSpeed);
                        break;
                    case CooldownType.KeyFrameAndCombo:
                        cooldown = new KeyframeComboCooldown(info.state, info.cooldown, info.keyframeData.ToList(), info.animationSpeed);
                        break;
                }

                list.Add(cooldown);
                if (info.state == AnimationState.Attack && cooldown is KeyframeComboCooldown kcd)
                {
                    _attackAnimationCooldown ??= kcd;
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

        public void UpdateAnimation(float deltaTime, ref PlayerAnimationCooldownState snapshotData)
        {
            for (int i = _animationCooldowns.Count - 1; i >= 0; i--)
            {
                var cooldown = _animationCooldowns[i];
                cooldown.Update(deltaTime);
                if (!snapshotData.AnimationCooldowns.TryGetValue(cooldown.AnimationState, out var snapshotCoolDown))
                {
                    snapshotCoolDown = new CooldownSnapshotData();
                    snapshotData.AnimationCooldowns.Add(cooldown.AnimationState, snapshotCoolDown);
                    continue;
                }

                CooldownSnapshotData.CopyTo(cooldown, ref snapshotCoolDown);
                snapshotData.AnimationCooldowns[cooldown.AnimationState] = snapshotCoolDown;
            }
        }

        public void RefreshSnapData(Dictionary<AnimationState, CooldownSnapshotData> snapshotData)
        {
            if (snapshotData == null || snapshotData.Count == 0)
            {
                return;
            }
            for (var i = _animationCooldowns.Count - 1; i >= 0; i--)
            {
                var animationCooldown = _animationCooldowns[i];
                if (!snapshotData.TryGetValue(animationCooldown.AnimationState, out var snapshotCoolDown))
                {
                    Debug.LogError($"snapshotData not contain animationState {animationCooldown.AnimationState}");
                    continue;
                }

                // if (snapshotCoolDown.CurrentCountdown != 0)
                // {
                //     Debug.Log($"RefreshSnapData {snapshotCoolDown}");
                // }


                animationCooldown.Refresh(snapshotCoolDown);
                if (animationCooldown is KeyframeComboCooldown kcd)
                {
                    _attackAnimationCooldown = kcd;
                }
            }
            _previousAnimationState = AnimationState.None;
        }

        [ClientRpc]
        public void HandlePlayerPropertyDifference(byte[] data)
        {
            if (!LocalPlayerHandler)
            {
                return;
            }
            var tracedInfo = BoxingFreeSerializer.MemoryDeserialize<TracedPlayerInfo>(data);

            PlayerComponentController playerComponent;
            if (tracedInfo.PlayerId == _playerInGameManager.LocalPlayerId)
            {
                playerComponent = this;
            }
            else
            {
                var otherPlayer = _gameSyncManager.GetPlayerConnection(tracedInfo.PlayerId).transform;
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
                localPlayerId = _playerInGameManager.LocalPlayerId
            };
        }
        
        private readonly Dictionary<PlayerEffectType, GameObject> _effectPool = new Dictionary<PlayerEffectType, GameObject>();
        private readonly Dictionary<PlayerEffectType, PlayerEffectContainer> _effectContainer = new Dictionary<PlayerEffectType, PlayerEffectContainer>();

        [ClientRpc]
        public void RpcPlayEffect(PlayerEffectType effectType)
        {
            if (!LocalPlayerHandler || effectType == PlayerEffectType.None)
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

        [ClientRpc]
        public void RpcPlayControlledEffect(ControlSkillType controlSkillType)
        {
            playerControlEffect.SetEffect(controlSkillType);
        }
        
        [Command(channel = Channels.Unreliable)]/**/
        public void CmdSendCommand(byte[] commandJson)
        {
            _gameSyncManager.EnqueueCommand(commandJson);
        }
        [ClientRpc]
        public void RpcSpawnSkillEffect(int skillConfigId, Vector3 position, AnimationState code)
        {
            _skillSyncState.SpawnSkillEffect(skillConfigId, position, code);
           
        }
        
        [ClientRpc]
        public void RpcPlayEffect(ParticlesType type)
        {
            EffectPlayer.Instance.PlayEffect(type, transform.position, transform);
        }

        [ClientRpc]
        public void RpcPlayAnimation(AnimationState animationState, bool forcePlay)
        {
            Debug.Log($"[PlayerComponentController] RpcPlayAnimation {animationState} {forcePlay}");
            if (animationState == AnimationState.Hit)
            {
                _picker.IsTouching = false;
            }
            _playerAnimationCalculator.HandleAnimation(animationState, forcePlay: false);
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Hurt, transform.position, transform);
        }

        [ClientRpc]
        public void RpcSetRb(bool isEnabled)
        {
            _rigidbody.isKinematic = isEnabled;
        }

        [Command]
        public void CmdEndGame(int connectionId)
        {
            _networkEndHandler.ConfirmCleanup(connectionId);
        }

        [Command]
        public void CmdCleanupClient(int connectionId)
        {
            _networkEndHandler.CmdReportCleanupCompleted(connectionId);
        }

        private void HandleNetworkCommand()
        {
            SendNetworkCommand();
            GetOtherPlayerPosition();
            _autoRecoverTime += Time.fixedDeltaTime;
            if (_autoRecoverTime > 0.25f)
            {
                _autoRecoverTime = 0;
                AutoRecover();
            }
        }
    }
}