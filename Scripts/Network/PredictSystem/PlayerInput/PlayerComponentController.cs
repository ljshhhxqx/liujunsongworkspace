using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Tool.ObjectPool;
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
using HotUpdate.Scripts.Player;
using HotUpdate.Scripts.Skill;
using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Static;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Panel;
using HotUpdate.Scripts.UI.UIs.Panel.Backpack;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using HotUpdate.Scripts.UI.UIs.Popup;
using MemoryPack;
using Mirror;
using Tool.GameEvent;
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
    public class PlayerComponentController : NetworkAutoInjectComponent, IAttackAnimationEvent
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
        
        [Header("States-NetworkBehaviour")]
        private PlayerInputPredictionState _inputState;
        private PropertyPredictionState _propertyPredictionState;
        private PlayerSkillSyncState _skillSyncState;
        
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
        private List<IAnimationCooldown> _animationCooldowns = new List<IAnimationCooldown>();
        private List<ISkillChecker> _skillCheckers = new List<ISkillChecker>();
        
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
        private GameEventManager _gameEventManager;
        private List<PredictableStateBase> _predictionStates = new List<PredictableStateBase>();
        private List<SyncStateBase> _syncStates = new List<SyncStateBase>();
        private Dictionary<AnimationState, ISkillChecker> _skillCheckersDic = new Dictionary<AnimationState, ISkillChecker>();
        private Dictionary<AnimationState, IAnimationCooldown> _animationCooldownsDict = new Dictionary<AnimationState, IAnimationCooldown>();
        private AnimationState _previousAnimationState;
        private KeyframeComboCooldown _attackAnimationCooldown;       
        private PlayerInGameManager _playerInGameManager;
        
        private BindingKey _propertyBindKey;
        private BindingKey _itemBindKey;
        private BindingKey _equipBindKey;
        private BindingKey _shopBindKey;
        private BindingKey _goldBindKey;
        private BindingKey _playerDeathTimeBindKey;
        private BindingKey _playerTraceOtherPlayerHpBindKey;
        private bool _localPlayerHandler;
        
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

        [Inject]
        private void Init(IConfigProvider configProvider, 
            GameSyncManager gameSyncManager, 
            UIManager uiManager,
            GameEventManager gameEventManager)
        {
            _configProvider = configProvider;
            var jsonDataConfig = _configProvider.GetConfig<JsonDataConfig>();
            _gameConfigData = jsonDataConfig.GameConfig;
            _playerConfigData = jsonDataConfig.PlayerConfig;
            _keyFunctionConfig = _configProvider.GetConfig<KeyFunctionConfig>();
            _gameSyncManager = gameSyncManager;
            _interactSystem = FindObjectOfType<InteractSystem>();
            _uiManager = uiManager;
            _rigidbody = GetComponent<Rigidbody>();
            _capsuleCollider = GetComponent<CapsuleCollider>();
            _animator = GetComponent<Animator>();
            _skillConfig = _configProvider.GetConfig<SkillConfig>();
            _camera = Camera.main;
            _animationCooldowns = GetAnimationCooldowns();
            _playerInGameManager = FindObjectOfType<PlayerInGameManager>();
            _gameEventManager = gameEventManager;
            GetAllCalculators(configProvider, gameSyncManager);
            HandleAllSyncState();
            HandleLocalInitCallback();

        }

        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            _localPlayerHandler = true;
            
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
            
            // _capsuleCollider.OnTriggerEnterAsObservable()
            //     .Where(c => c.gameObject.TryGetComponent<PlayerBase>(out _) && isLocalPlayer)
            //     .Subscribe(c =>
            //     {
            //         _canOpenShop = PlayerInGameManager.Instance.IsPlayerInHisBase(netId, out _);
            //     })
            //     .AddTo(this);
            _capsuleCollider.OnTriggerStayAsObservable()
                .Sample(TimeSpan.FromMilliseconds(GameSyncManager.TickSeconds * 1000))
                .Where(c => c.gameObject.TryGetComponent<PlayerBase>(out _) && isLocalPlayer)
                .Subscribe(c =>
                {
                    var header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId,
                        CommandType.Property, CommandAuthority.Client);
                    var playerTouchedBaseCommand = new PlayerTouchedBaseCommand
                    {
                        Header = header,
                    };
                    _gameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(playerTouchedBaseCommand).Item1);
                }).AddTo(_disposables);
            // _capsuleCollider.OnTriggerExitAsObservable()
            //     .Where(c => c.gameObject.TryGetComponent<PlayerBase>(out _) && isLocalPlayer)
            //     .Subscribe(_ =>
            //     {
            //         _canOpenShop = false;
            //     }).AddTo(_disposables);
            
            Observable.EveryUpdate()
                .Where(_ => _localPlayerHandler)
                .Subscribe(_ =>
                {
                    if (_keyFunctionConfig.IsKeyFunction(out var keyFunction))
                    {
                        var uiType = keyFunction.GetUIType();
                        switch (uiType)
                        {
                            case UIType.None:
                                break;
                            case UIType.Backpack:
                                var bagItemOverlay = _uiManager.SwitchUI<BackpackScreenUI>();
                                if (!bagItemOverlay)
                                {
                                    return;
                                }

                                // if (_reactivePropertyBinds.TryGetValue(typeof(BagItemData), out var isBagBind) && isBagBind)
                                // {
                                //     return;
                                // }
                                
                                //_reactivePropertyBinds.Add(typeof(BagItemData), true);
                                bagItemOverlay.BindBagItemData(UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemBindKey));
                                //bagItemOverlay.BindEquipItemData(UIPropertyBinder.GetReactiveDictionary<EquipItemData>(_equipBindKey));
                                break;
                            case UIType.Shop:
                                var shopScreenUI = _uiManager.SwitchUI<ShopScreenUI>();
                                if (!shopScreenUI)
                                {
                                    return;
                                }

                                // if (_reactivePropertyBinds.TryGetValue(typeof(RandomShopItemData), out var isShopBind) && isShopBind)
                                // {
                                //     return;
                                // }
                                shopScreenUI.BindShopItemData(UIPropertyBinder.GetReactiveDictionary<RandomShopItemData>(_shopBindKey));
                                shopScreenUI.BindBagItemData(UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemBindKey));
                                shopScreenUI.BindPlayerGold(UIPropertyBinder.ObserveProperty<ValuePropertyData>(_goldBindKey));
                                shopScreenUI.OnRefresh.Subscribe(_ =>
                                {
                                    var refreshCommand = new RefreshShopCommand
                                    {
                                        Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Shop, CommandAuthority.Client),
                                    };
                                    _gameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(refreshCommand).Item1);
                                }).AddTo(shopScreenUI.gameObject);
                                //_reactivePropertyBinds.Add(typeof(RandomShopItemData), true);
                                // var refreshCommand = new RefreshShopCommand
                                // {
                                //     Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Shop, CommandAuthority.Client
                                //     ),
                                // };
                                // _gameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(refreshCommand).Item1);
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                })
                .AddTo(_disposables);
            
            Observable.EveryUpdate()
                .Where(_ => _localPlayerHandler && Cursor.lockState == CursorLockMode.Locked && _isControlled && GameSyncManager.CurrentTick > 0 
                                                && _subjectedStateType.HasAllStates(SubjectedStateType.None) || _subjectedStateType.HasAllStates(SubjectedStateType.IsInvisible))
                .Subscribe(_ => {
                    var movement = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
                    var animationStates = _inputState.GetAnimationStates();
                    // if (animationStates.HasAnyState(AnimationState.Attack))
                    // {
                    //     Debug.Log($"[PlayerInputController] Attack");
                    // }
                    // else if (animationStates.HasAnyState(AnimationState.Roll))
                    // {
                    //     Debug.Log($"[PlayerInputController] Roll");
                    // }
                    // else if (animationStates.HasAnyState(AnimationState.Jump))
                    // {
                    //     Debug.Log($"[PlayerInputController] Jump");
                    // }
                    var playerInputStateData = new PlayerInputStateData
                    {
                        InputMovement = movement,
                        InputAnimations = animationStates,
                    };
                    var command = GetCurrentAnimationState(playerInputStateData);
                    
                    if (!_playerAnimationCalculator.CanPlayAnimation(command))
                    {
                        //Debug.LogWarning($"[PlayerInputController] Can not play animation {command}");
                        command = AnimationState.None;
                    }
                    playerInputStateData.Command = command;
                    if (_animationCooldownsDict.TryGetValue(command, out var animationCooldown))
                    {
                        //Debug.LogWarning($"[PlayerInputController] Animation cooldown {animationCooldown.AnimationState} is ready => {animationCooldown.IsReady()}.");
                        playerInputStateData.Command = animationCooldown.IsReady() ? command : AnimationState.None;
                    }
                    // if (playerInputStateData.Command == AnimationState.Attack)
                    // {
                    //     Debug.Log($"[PlayerInputController] Attack");
                    // }
                    _playerInputStateData = playerInputStateData;
                    _inputStream.OnNext(_playerInputStateData);
                    //Debug.Log($"playerInputStateData - {playerInputStateData.InputMovement} {playerInputStateData.InputAnimations} {playerInputStateData.Command}");
                })
                .AddTo(_disposables);
            Observable.EveryFixedUpdate()
                .Sample(TimeSpan.FromMilliseconds(0.25f * 1000))
                .Where(_ => _localPlayerHandler && _propertyPredictionState.GetProperty(PropertyTypeEnum.Health) > 0)
                .Subscribe(_ =>
                {
                    var health = _propertyPredictionState.GetCalculator(PropertyTypeEnum.Health);
                    var strength = _propertyPredictionState.GetCalculator(PropertyTypeEnum.Strength);
                    if (Mathf.Approximately(health.CurrentValue, health.MaxCurrentValue) && Mathf.Approximately(strength.CurrentValue, strength.MaxCurrentValue))
                    {
                        return;
                    }
                    var propertyAutoRecoverCommand = ObjectPoolManager<PropertyAutoRecoverCommand>.Instance.Get(50);
                    propertyAutoRecoverCommand.Header = GameSyncManager.CreateNetworkCommandHeader(
                        connectionToClient.connectionId,
                        CommandType.Property, CommandAuthority.Client, CommandExecuteType.Predicate,
                        NetworkCommandType.PropertyAutoRecover);
                    _propertyPredictionState.AddPredictedCommand(propertyAutoRecoverCommand);
                    //ObjectPoolManager<PropertyAutoRecoverCommand>.Instance.Return(propertyAutoRecoverCommand);
                })
                .AddTo(this);
            Observable.EveryFixedUpdate()
                .Where(_ => _localPlayerHandler && _propertyPredictionState.GetProperty(PropertyTypeEnum.Health) > 0 && GameSyncManager.CurrentTick > 0)
                .Subscribe(_ =>
                {
                    HandleInputPhysics(_playerInputStateData);
                    _targetSpeed = _propertyPredictionState.GetMoveSpeed();
                    var propertyEnvironmentChangeCommand = ObjectPoolManager<PropertyEnvironmentChangeCommand>.Instance.Get(50);
                    propertyEnvironmentChangeCommand.Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId,
                        CommandType.Property, CommandAuthority.Client, CommandExecuteType.Predicate, NetworkCommandType.PropertyEnvironmentChange);
                    propertyEnvironmentChangeCommand.HasInputMovement = _playerInputStateData.InputMovement.magnitude > 0.1f;
                    propertyEnvironmentChangeCommand.PlayerEnvironmentState = _gameStateStream.Value;
                    propertyEnvironmentChangeCommand.IsSprinting = _playerInputStateData.Command.HasAnyState(AnimationState.Sprint);
                    _propertyPredictionState.AddPredictedCommand(propertyEnvironmentChangeCommand);
                    for (int i = 0; i < _predictionStates.Count; i++)
                    {
                        var state = _predictionStates[i];
                        state.ExecutePredictedCommands(GameSyncManager.CurrentTick);
                    }
                    //ObjectPoolManager<PropertyEnvironmentChangeCommand>.Instance.Return(propertyEnvironmentChangeCommand);
                })
                .AddTo(this);
            
            //发送网络命令
            _inputStream.Where(x=> _localPlayerHandler && x.Command != AnimationState.None && x.Command != AnimationState.Idle)
                .Sample(TimeSpan.FromMilliseconds(Time.fixedDeltaTime * 1000))
                .Subscribe(HandleSendNetworkCommand)
                .AddTo(this);
            //处理物理信息
            Observable.EveryFixedUpdate().Sample(TimeSpan.FromMilliseconds(FixedDeltaTime * 10 * 1000))
                .Where(_ => _localPlayerHandler)
                .Subscribe(_ =>
                {
                    var otherPlayers = NetworkServer.connections
                        .Where(x => x.Value.connectionId != connectionToClient.connectionId)
                        .Select(x => x.Value.identity.GetComponent<Transform>());
                    var potentialTargets = otherPlayers as Transform[] ?? otherPlayers.ToArray();
                    if (potentialTargets.Length == 0)
                    {
                        return;
                    }
                    var layerMask = _gameConfigData.groundSceneLayer | _gameConfigData.stairSceneLayer | _playerConfigData.PlayerLayer;
                    if (PlayerPhysicsCalculator.TryGetPlayersInScreen(_camera, potentialTargets, out var playersInScreen, layerMask))
                    {
                        var header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId,
                            CommandType.Property, CommandAuthority.Client);
                        var playerInScreenCommand = new PlayerTraceOtherPlayerHpCommand
                        {
                            Header = header,
                            TargetConnectionIds = playersInScreen.ToArray(),
                        };
                        _gameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(playerInScreenCommand).Item1);
                    }
                })
                .AddTo(_disposables);
        }

        private void HandleAllSyncState()
        {
            gameObject.AddComponent<PlayerEquipmentSyncState>();
            gameObject.AddComponent<PlayerInputPredictionState>();
            gameObject.AddComponent<PlayerItemPredictableState>();
            gameObject.AddComponent<PlayerShopPredictableState>();
            gameObject.AddComponent<PlayerSkillSyncState>();
            gameObject.AddComponent<PropertyPredictionState>();
            var states = GetComponents<PredictableStateBase>();
            var syncStates = GetComponents<SyncStateBase>();
            for (int i = 0; i < states.Length; i++)
            {
                _predictionStates.Add(states[i]);
                //ObjectInjectProvider.Instance.InjectMapGameObject(states[i]);
            }

            for (int i = 0; i < syncStates.Length; i++)
            {
                _syncStates.Add(syncStates[i]);
                //ObjectInjectProvider.Instance.Inject(states[i]);
            }
            _inputState = GetComponent<PlayerInputPredictionState>();
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _skillSyncState = GetComponent<PlayerSkillSyncState>();
            _propertyPredictionState.OnPropertyChanged += HandlePropertyChange;
            _propertyPredictionState.OnStateChanged += HandlePropertyStateChanged;
            _propertyPredictionState.OnPlayerDead += HandlePlayerDeadClient;
            _propertyPredictionState.OnPlayerRespawned += HandlePlayerRespawnedClient;
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
        }

        public void SwitchShop()
        {
            if (!isLocalPlayer)
            {
                return;
            }
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
        }

        [Client]
        private void HandleSendNetworkCommand(PlayerInputStateData inputData)
        {
            // _timer+=Time.fixedDeltaTime;
            // _frameCount++;
            // if (_timer >= 1f)
            // {
            //     Debug.Log($"[HandleSendNetworkCommand] 理论frameCount => {1/Time.fixedDeltaTime} 实际frameCount => {_frameCount}");
            //     
            //     _timer = 0;
            //     _frameCount = 0;
            // }3
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

            var inputCommand = ObjectPoolManager<InputCommand>.Instance.Get(50);
            
            _previousAnimationState = inputData.Command;
            inputCommand.InputMovement = CompressedVector3.FromVector3(inputData.InputMovement);
            inputCommand.Header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId,
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


        private void HandleInputPhysics(PlayerInputStateData inputData)
        {
            _currentSpeed = Mathf.SmoothDamp(_currentSpeed, _targetSpeed, ref _speedSmoothVelocity, _speedSmoothTime);
            _playerPhysicsCalculator.CurrentSpeed = _currentSpeed;
            //Debug.Log($"[HandleInputPhysics]- _currentSpeed > {_currentSpeed} _targetSpeed ->{_targetSpeed}");
            _gameStateStream.Value = _playerPhysicsCalculator.CheckPlayerState(new CheckGroundDistanceParam(inputData.InputMovement, FixedDeltaTime));
            _groundDistanceStream.Value = _playerPhysicsCalculator.GroundDistance;
            _isSpecialActionStream.Value = _playerAnimationCalculator.IsSpecialAction;
            _playerAnimationCalculator.SetEnvironmentState(_gameStateStream.Value);
            _playerAnimationCalculator.SetGroundDistance(_groundDistanceStream.Value);
            _playerAnimationCalculator.SetAnimatorParams(inputData.InputMovement.magnitude, _groundDistanceStream.Value, _currentSpeed);
            _playerAnimationCalculator.UpdateAnimationState();
           
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
                // case AnimationState.Dead:
                // case AnimationState.Hit:
                // case AnimationState.SkillE:
                // case AnimationState.SkillQ:
                // case AnimationState.Attack:
                //     _rigidbody.velocity = Vector3.zero;
                //     break;
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
            });
            _playerPhysicsCalculator = new PlayerPhysicsCalculator(new PhysicsComponent(_rigidbody, transform, _checkStairTransform, _capsuleCollider, _camera));
            _playerPropertyCalculator = new PlayerPropertyCalculator(PlayerPropertyCalculator.GetPropertyCalculators());
            _playerAnimationCalculator = new PlayerAnimationCalculator(new AnimationComponent{ Animator = _animator});
            _playerBattleCalculator = new PlayerBattleCalculator(new PlayerBattleComponent(transform));
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
            _gameEventManager.Publish(new PlayerSpawnedEvent(rotateCenter));
            var shopConstant = PlayerShopCalculator.Constant;
            shopConstant.IsServer = isServer;
            shopConstant.IsClient = isClient;
            shopConstant.UIManager = _uiManager;
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
            _disposables?.Clear();
            _propertyPredictionState.OnPropertyChanged -= HandlePropertyChange;
            _propertyPredictionState.OnStateChanged -= HandlePropertyStateChanged;
            _propertyPredictionState.OnPlayerDead -= HandlePlayerDeadClient;
            _propertyPredictionState.OnPlayerRespawned -= HandlePlayerRespawnedClient;
            _inputState.OnPlayerStateChanged -= HandlePlayerStateChanged;
            _inputState.OnPlayerAnimationCooldownChanged -= HandlePlayerAnimationCooldownChanged;
            _inputState.OnPlayerInputStateChanged -= HandlePlayerInputStateChanged;
            _inputState.IsInSpecialState -= HandleSpecialState;
            _uiManager.IsUnlockMouse -= OnIsUnlockMouse;
            _animationCooldowns.Clear();
            _inputStream.Dispose();
            _effectContainer.Clear();
            playerEffectPlayer.StopAllEffect(container => GameObjectPoolManger.Instance.ReturnObject(container.gameObject));
            _effectPool.Clear();
        }

        private void OnIsUnlockMouse(bool isUnlock)
        {
            //Debug.Log($"[OnIsUnlockMouse] isUnlock ->{isUnlock}");
            _isControlled = !isUnlock;
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
            var param = ObjectPoolManager<DetermineAnimationStateParams>.Instance.Get(15);
            param.InputMovement = inputData.InputMovement;
            param.InputAnimationStates = inputData.InputAnimations;
            param.GroundDistance = _groundDistanceStream.Value;
            param.EnvironmentState = _gameStateStream.Value;
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
            var data = ObjectPoolManager<PlayerGameStateData>.Instance.Get(30);
            data.Position = transform.position;
            data.Quaternion = transform.rotation;
            data.Velocity = _rigidbody.velocity;
            data.PlayerEnvironmentState = _gameStateStream.Value;
            data.AnimationState = inputData.Command;
            ObjectPoolManager<PlayerGameStateData>.Instance.Return(data);
            return data;
        }

        [Client]
        public PlayerGameStateData HandleClientMoveAndAnimation(PlayerInputStateData inputData)
        {
            //Debug.Log($"[HandleClientMoveAndAnimation] start");
            return HandleMoveAndAnimation(inputData);
        }

        //这一行开始，写对外接口
        [Server]
        public PlayerGameStateData HandleServerMoveAndAnimation(PlayerInputStateData inputData)
        {
            //Debug.Log($"[HandleServerMoveAndAnimation] start");
            //_inputStream.OnNext(inputData);
            return HandleMoveAndAnimation(inputData);
        }

        [Server]
        public void HandlePropertyRecover(ref PlayerPredictablePropertyState playerPredictablePropertyState)
        {
            _playerPropertyCalculator.HandlePropertyRecover(ref playerPredictablePropertyState);
        }

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
            var info = MemoryPackSerializer.Deserialize<MemoryList<TracedPlayerInfo>>(data);
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
                        Header = InteractSystem.CreateInteractHeader(connection.connectionId, InteractCategory.PlayerToPlayer),
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
            var playerChangeUnionCommand = MemoryPackSerializer.Deserialize<PlayerChangeUnionRequest>(data);
            _interactSystem.EnqueueCommand(playerChangeUnionCommand);
        }
        

        [ClientRpc]
        public void RpcSetPlayerAlpha(float alpha)
        {
            var actualAlpha = alpha * 0.001f;
            if(Mathf.Approximately(actualAlpha, 1))
                return;
            playerControlEffect.SetTransparency( 1 -actualAlpha);
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
            var config = _configProvider.GetConfig<AnimationConfig>();
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

                // if (cooldown is KeyframeCooldown keyframeCooldown)
                // {
                //     Debug.Log($"[UpdateAnimation] {snapshotCoolDown.AnimationState} = {cooldown.AnimationState} ? {cooldown.AnimationState == snapshotCoolDown.AnimationState} {keyframeCooldown.CurrentCountdown} {snapshotCoolDown.ToString()}");
                //
                // }
                // if (cooldown is KeyframeComboCooldown keyframeComboCooldown)
                // {
                //     Debug.Log($"[UpdateAnimation] {snapshotCoolDown.AnimationState} = {cooldown.AnimationState} ? {cooldown.AnimationState == snapshotCoolDown.AnimationState} {keyframeComboCooldown.CurrentCountdown} {snapshotCoolDown.ToString()}");
                //
                // }
                CooldownSnapshotData.CopyTo(cooldown, ref snapshotCoolDown);
                snapshotData.AnimationCooldowns[cooldown.AnimationState] = snapshotCoolDown;
            }
        }
        // public void UpdateAnimation(float deltaTime)
        // {
        //     for (int i = _animationCooldowns.Count - 1; i >= 0; i--)
        //     {
        //         var cooldown = _animationCooldowns[i];
        //         cooldown.Update(deltaTime);
        //     }
        // }

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

                animationCooldown.Refresh(snapshotCoolDown);
            }
            _previousAnimationState = AnimationState.None;
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
            if (!isLocalPlayer || effectType == PlayerEffectType.None)
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

        // private void Update()
        // {
        //     if (_localPlayerHandler)
        //     {
        //     }
        //
        // }
        //
        // private void TestNetworkCommandHeader()
        // {
        //     var header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Input,
        //         CommandAuthority.Client);
        //     Debug.Log($"TestNetworkCommandHeader -> {header.CommandType} -> {header.CommandId} ->{header.ConnectionId} {header.Timestamp} {header.Authority} {header.Tick}");
        //     var data = MemoryPackSerializer.Serialize(header);
        //     var originData = MemoryPackSerializer.Deserialize<NetworkCommandHeader>(data);
        //     Debug.Log($"TestNetworkCommandHeader -> {originData.CommandType} -> {originData.CommandId} ->{originData.ConnectionId} {originData.Timestamp} {originData.Authority} {originData.Tick}");
        // }
        //
        // private void TestInputAnimationStates()
        // {
        //     var inputCommand = new InputCommand();
        //     var header = GameSyncManager.CreateNetworkCommandHeader(connectionToClient.connectionId, CommandType.Input,
        //         CommandAuthority.Client);
        //     var inputMovement = CompressedVector3.FromVector3(new Vector3(0,0,0.125555f));
        //     inputCommand.Header = header;
        //     inputCommand.InputMovement = inputMovement;
        //     inputCommand.InputAnimationStates = AnimationState.None;
        //     inputCommand.CommandAnimationState = AnimationState.Move;
        //     Debug.Log($"TestInputAnimationStates -> {inputCommand.Header.CommandType} -> {inputCommand.Header.CommandId} ->{inputCommand.Header.ConnectionId} {inputCommand.Header.Timestamp} {inputCommand.Header.Authority} {inputCommand.Header.Tick}");
        //     var data = NetworkCommandExtensions.SerializeCommand(inputCommand);
        //     var originData = NetworkCommandExtensions.DeserializeCommand(data.Item1);
        //     var headerData = originData.GetHeader();
        //     Debug.Log($"TestInputAnimationStates -> {headerData.CommandType} -> {headerData.CommandId} ->{headerData.ConnectionId} {headerData.Timestamp} {headerData.Authority} {headerData.Tick}");
        // }
        [ClientRpc]
        public void RpcSpawnSkillEffect(int skillConfigId, Vector3 position, AnimationState code)
        {
            _skillSyncState.SpawnSkillEffect(skillConfigId, position, code);
           
        }
    }
}