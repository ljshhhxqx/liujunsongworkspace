using System;
using System.Collections.Generic;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using AOTScripts.Tool.Resource;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Network.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool.ObjectPool;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = AOTScripts.Data.AnimationState;
using CooldownSnapshotData = AOTScripts.Data.CooldownSnapshotData;
using INetworkCommand = AOTScripts.Data.INetworkCommand;
using InputCommand = AOTScripts.Data.InputCommand;
using ISyncPropertyState = AOTScripts.Data.ISyncPropertyState;
using PlayerAnimationCooldownState = AOTScripts.Data.PlayerAnimationCooldownState;
using PlayerGameStateData = AOTScripts.Data.PlayerGameStateData;
using PlayerInputState = AOTScripts.Data.PlayerInputState;
using PropertyClientAnimationCommand = AOTScripts.Data.PropertyClientAnimationCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerInputPredictionState : PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        public PlayerInputState InputState => (PlayerInputState) CurrentState;
        private PropertyPredictionState _propertyPredictionState;
        private KeyAnimationConfig _keyAnimationConfig;
        private AnimationConfig _animationConfig;
        private JsonDataConfig _jsonDataConfig;
        private SkillConfig _skillConfig;
        private PlayerSkillSyncState _skillSyncState;
        private BindingKey _playerAnimationKey;
        private UIManager _uiManager;
        private bool _isSimulating;
        private VirtualInputOverlay _virtualPlayerAnimationOverlay;
        private float _updatePositionTimer;
        
        protected override CommandType CommandType => CommandType.Input;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isApplyingState;
        private HReactiveDictionary<int, AnimationStateData> _animationStateDataDict = new HReactiveDictionary<int, AnimationStateData>();
        
        public event Action<PlayerGameStateData> OnPlayerStateChanged; 
        public event Action<PlayerAnimationCooldownState> OnPlayerAnimationCooldownChanged;
        public event Action<PlayerInputStateData> OnPlayerInputStateChanged;
        public event Func<bool> IsInSpecialState;
        
        public VirtualInputOverlay VirtualPlayerAnimationOverlay => _virtualPlayerAnimationOverlay;

        [Inject]
        private void InitContainer(GameSyncManager gameSyncManager, IConfigProvider configProvider, UIManager uiManager, PlayerInGameManager playerInGameManager)
        {
            base.Init(gameSyncManager, configProvider, playerInGameManager);
            _uiManager = uiManager;
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _skillSyncState = GetComponent<PlayerSkillSyncState>();
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _keyAnimationConfig = configProvider.GetConfig<KeyAnimationConfig>();
            _skillConfig = configProvider.GetConfig<SkillConfig>();
            if (NetworkIdentity.isLocalPlayer)
            {
                
            }
        }

        protected override void InjectLocalPlayerCallback()
        {
            Debug.Log($"PropertyPredictionState [OnStartLocalPlayer]  ");
            _playerAnimationKey = new BindingKey(UIPropertyDefine.Animation, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);

            _animationStateDataDict =
                UIPropertyBinder.GetReactiveDictionary<AnimationStateData>(_playerAnimationKey);
            if (PlayerPlatformDefine.IsWindowsPlatform())
            {
                var playerAnimationOverlay = _uiManager.SwitchUI<PlayerAnimationOverlay>();
                playerAnimationOverlay.BindPlayerAnimationData(_animationStateDataDict);
            }
            else if (PlayerPlatformDefine.IsJoystickPlatform())
            {
                _virtualPlayerAnimationOverlay = _uiManager.SwitchUI<VirtualInputOverlay>();
                _virtualPlayerAnimationOverlay.transform.SetAsLastSibling();
                _virtualPlayerAnimationOverlay.BindPlayerAnimationData(_animationStateDataDict);
            }
            var dic = new Dictionary<int, AnimationStateData>();
            var animations = _animationConfig.AnimationInfos;
            for (int i = 0; i < animations.Count; i++)
            {
                var ani = animations[i];
                if (ani.showInHud)
                {
                    var stateData = ObjectPoolManager<AnimationStateData>.Instance.Get(animations.Count);
                    stateData.State = ani.state;
                    stateData.Duration = ani.cooldown;
                    stateData.Cost = ani.cost;
                    stateData.Timer = 0;
                    stateData.Icon = UISpriteContainer.GetSprite(ani.icon);
                    stateData.Frame = UISpriteContainer.GetQualitySprite(ani.frame);
                    stateData.Index = 0;
                    dic.Add((int)ani.state, stateData);
                }
            }
            UIPropertyBinder.OptimizedBatchAdd(_playerAnimationKey, dic);
            
            if (ServerHandler)
            {
                return;
            }
            UpdateAnimationCooldowns(_cancellationTokenSource.Token, GameSyncManager.ServerUpdateInterval).Forget();
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is null || state is not PlayerInputState inputState || CurrentState is not PlayerInputState propertyState)
                return false;
            return !inputState.IsEqual(propertyState);
        }

        public override void ApplyServerState<T>(T state) 
        {
            if (state is not PlayerInputState propertyState)
                return;
            _isApplyingState = true;
            base.ApplyServerState(propertyState);
            var snapshot = propertyState.PlayerAnimationCooldownState.AnimationCooldowns;
            PlayerComponentController.RefreshSnapData(snapshot);
            OnPlayerStateChanged?.Invoke(propertyState.PlayerGameStateData);
            OnPlayerAnimationCooldownChanged?.Invoke(propertyState.PlayerAnimationCooldownState);
            UpdateUIAnimation(propertyState.PlayerAnimationCooldownState.AnimationCooldowns);
            if (isClient)
            {
                if (!isLocalPlayer)
                {
                    //transform.position = Vector3.Lerp(transform.position, propertyState.PlayerGameStateData.Position, 0.1f);
                }
                else
                {
                    // _updatePositionTimer += GameSyncManager.ServerUpdateInterval;
                    // if (_updatePositionTimer >= 5f)
                    // {
                    //     _updatePositionTimer = 0f;
                    //     if (Vector3.Distance(transform.position, propertyState.PlayerGameStateData.Position) > 0.1f)
                    //     {
                    //         Debug.Log($"[ApplyServerState] - UpdatePosition {propertyState.PlayerGameStateData.Position}");
                    //         //transform.position = propertyState.PlayerGameStateData.Position;
                    //     }
                    // }
                }
                // else if (Vector3.Distance(transform.position, propertyState.PlayerGameStateData.Position) > 0.2f)
                // {
                //     transform.position = propertyState.PlayerGameStateData.Position;
                // }
            }
            // foreach (var inputStateData in propertyState.PlayerAnimationCooldownState.AnimationCooldowns)
            // {
            //     Debug.Log($"[ApplyServerState] - {inputStateData.Value.ToString()}");
            // }

            _isApplyingState = false;
        }

        private void UpdateUIAnimation(MemoryDictionary<AnimationState, CooldownSnapshotData> snapshot)
        {
            if (!LocalPlayerHandler)
            {
                return;
            }
            
            foreach (var kvp in snapshot)
            {
                if (!_animationStateDataDict.TryGetValue((int)kvp.Key, out var animationData))
                {
                    animationData = new AnimationStateData();
                    animationData.State = kvp.Key;
                    animationData.Duration = _animationConfig.GetAnimationInfo(kvp.Key).cooldown;
                    animationData.Cost = 0;
                    animationData.Timer = kvp.Value.CurrentCountdown;
                    animationData.Icon = UISpriteContainer.GetSprite(_animationConfig.GetAnimationInfo(kvp.Key).icon);
                    animationData.Frame = UISpriteContainer.GetQualitySprite(_animationConfig.GetAnimationInfo(kvp.Key).frame);
                    animationData.Index = kvp.Value.CurrentStage;
                }
                else
                {
                    animationData.Timer = kvp.Value.CurrentCountdown;
                    animationData.Index = kvp.Value.CurrentStage;
                }
                _animationStateDataDict.Update((int)kvp.Key, animationData);//[] = animationData;
                UIPropertyBinder.UpdateDictionary(_playerAnimationKey, (int)kvp.Key, animationData);
                //Debug.Log($"[UpdateUIAnimation] {animationData.ToString()}");
            }
        }

        public AnimationState GetAnimationStates()
        {
            return _keyAnimationConfig.GetAllActiveActions();
        }
        
        /// <summary>
        /// 计算玩家控制逻辑、动画状态
        /// </summary>
        /// <param name="command"></param>
        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            try
            {
                if (_isSimulating || _isApplyingState || IsInSpecialState?.Invoke() == true)
                    return;
                _isSimulating = true;
                if (header.CommandType == CommandType.Input && command is InputCommand inputCommand)
                {
                    if (!_propertyPredictionState.CanDoAnimation(inputCommand.CommandAnimationState))
                    {
                        Debug.LogWarning($"Cannot perform {inputCommand.CommandAnimationState}. now has {_propertyPredictionState.NowStateType}");
                        return;
                    }
                    if (inputCommand.CommandAnimationState is AnimationState.Attack or AnimationState.Jump or AnimationState.SprintJump or AnimationState.Roll or AnimationState.SkillE or AnimationState.SkillQ)
                    {
                        Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                    }
                    var info = _animationConfig.GetAnimationInfo(inputCommand.CommandAnimationState);
                    //Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                    var actionType = info.actionType;
                    if (actionType != ActionType.Movement && actionType != ActionType.Interaction)
                    {
                        //Debug.Log($"[PlayerInputPredictionState] - Not enough strength to perform {inputCommand.CommandAnimationState}.");
                        return;
                    }
                    var health = _propertyPredictionState.GetProperty(PropertyTypeEnum.Health);
                    //Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                    if (health <= 0)
                    {
                        Debug.Log($"[PlayerInputPredictionState] - Player is dead.");
                        return;
                    }
                    SkillConfigData skillConfigData = default;
                    if (inputCommand.CommandAnimationState is AnimationState.SkillE or AnimationState.SkillQ)
                    { 
                        skillConfigData = _skillSyncState.GetSkillConfigData(inputCommand.CommandAnimationState);
                    }                
                    var cost = skillConfigData.id == 0 ? info.cost : skillConfigData.cost;
                    var cooldown = skillConfigData.id == 0 ? info.cooldown : skillConfigData.cooldown;

                    var animationCooldowns = PlayerComponentController.AnimationCooldownsDict;
                    var cooldownInfo = animationCooldowns.GetValueOrDefault(inputCommand.CommandAnimationState);
                    if (cooldown > 0)
                    {
                        if (cooldownInfo == null)
                        {
                            Debug.LogWarning($"Animation {inputCommand.CommandAnimationState} is not registered in cooldown.");
                            return;
                        }
                    }
                    if (cost > 0)
                    {
                        var currentStrength = _propertyPredictionState.GetProperty(PropertyTypeEnum.Strength);
                        if (!_animationConfig.IsStrengthEnough(inputCommand.CommandAnimationState, currentStrength, out var noStrengthState, GameSyncManager.TickSeconds)
                            && noStrengthState == AnimationState.None)
                        {
                            Debug.LogWarning($"Not enough strength to perform {inputCommand.CommandAnimationState}.");
                            return;
                        }

                        if (cooldownInfo != null)
                        {
                            if (!cooldownInfo.IsReady())
                            {
                                Debug.LogWarning($"Animation {inputCommand.CommandAnimationState} is on cooldown.");
                                return;
                            }

                            cooldownInfo.Use();
                            Debug.Log($"[Simulate] [Normal] - CommandAnimationState:{inputCommand.CommandAnimationState}- cooldownInfo:{cooldownInfo.AnimationState} - cooldown:{cooldown} - cost:{cost}");
                            if (animationCooldowns.ContainsKey(inputCommand.CommandAnimationState))
                            {
                                animationCooldowns[inputCommand.CommandAnimationState] = cooldownInfo;
                                PlayerComponentController.AnimationCooldownsDict = animationCooldowns;
                            }
                        }

                        var animationCommand = new PropertyClientAnimationCommand(); //ObjectPoolManager<PropertyClientAnimationCommand>.Instance.Get(50);
                        animationCommand.AnimationState = noStrengthState == AnimationState.None ? inputCommand.CommandAnimationState : noStrengthState;
                        animationCommand.Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property, CommandAuthority.Client);
                        animationCommand.SkillId = skillConfigData.id;
                        _propertyPredictionState.AddPredictedCommand(animationCommand);
                    }

                    if (skillConfigData.animationState != AnimationState.None)
                    {
                        Debug.Log($"[Simulate] [Skill] - CommandAnimationState:{inputCommand.CommandAnimationState} - cooldown:{cooldown} - cost:{cost}");

                        cooldownInfo?.Use(); 
                    }

                    var inputStateData = ObjectPoolManager<PlayerInputStateData>.Instance.Get(50);
                    inputStateData.InputMovement = inputCommand.InputMovement.ToVector3() * _propertyPredictionState.NowSpeedRatio;
                    inputStateData.InputAnimations = inputCommand.InputAnimationStates;
                    inputStateData.Command = inputCommand.CommandAnimationState;
                    PlayerComponentController.HandlePlayerSpecialAction(inputStateData.Command);
                    OnPlayerInputStateChanged?.Invoke(inputStateData);
                    //Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
            finally
            {
                _isSimulating = false;
            }
            
        }
        
        private async UniTaskVoid UpdateAnimationCooldowns(CancellationToken token, float deltaTime)
        {
            while (!token.IsCancellationRequested && !_isApplyingState && !_isSimulating)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(deltaTime), cancellationToken: token);
                PlayerComponentController.UpdateAnimation(deltaTime);
            }
        }
    }

    public struct PlayerInputStateData : IPoolObject,IEquatable<PlayerInputStateData>
    {
        public Vector3 InputMovement;   // 输入的移动
        public AnimationState InputAnimations; // 输入指令的动画
        public AnimationState Command; // 指令
        public Vector3 Velocity; // 速度
        public void Init()
        {
        }

        public void Clear()
        {
            InputMovement = Vector3.zero;
            InputAnimations = AnimationState.None;
            Command = AnimationState.None;
            Velocity = Vector3.zero;
        }
        
        public override string ToString()
        {
            return $"InputMovement: {InputMovement}, InputAnimations: {InputAnimations}, Command: {Command} , Velocity: {Velocity}";
        }

        public bool Equals(PlayerInputStateData other)
        {
            return InputMovement.Equals(other.InputMovement) && InputAnimations == other.InputAnimations && Command == other.Command && Velocity.Equals(other.Velocity);
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerInputStateData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(InputMovement, (int)InputAnimations, (int)Command, Velocity);
        }

        public static bool operator ==(PlayerInputStateData left, PlayerInputStateData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PlayerInputStateData left, PlayerInputStateData right)
        {
            return !left.Equals(right);
        }
    }
}