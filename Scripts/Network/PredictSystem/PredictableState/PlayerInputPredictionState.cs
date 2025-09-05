using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using InputCommand = HotUpdate.Scripts.Network.PredictSystem.Data.InputCommand;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PlayerInputState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerInputState;
using PropertyClientAnimationCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyClientAnimationCommand;

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
        
        protected override CommandType CommandType => CommandType.Input;
        private CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private bool _isApplyingState;
        private ReactiveDictionary<int, AnimationStateData> _animationStateDataDict;
        
        public event Action<PlayerGameStateData> OnPlayerStateChanged; 
        public event Action<PlayerAnimationCooldownState> OnPlayerAnimationCooldownChanged;
        public event Action<PlayerInputStateData> OnPlayerInputStateChanged;
        public event Func<bool> IsInSpecialState;

        [Inject]
        private void InitContainer(GameSyncManager gameSyncManager, IConfigProvider configProvider, UIManager uiManager)
        {
            base.Init(gameSyncManager, configProvider);
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _skillSyncState = GetComponent<PlayerSkillSyncState>();
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _keyAnimationConfig = configProvider.GetConfig<KeyAnimationConfig>();
            _skillConfig = configProvider.GetConfig<SkillConfig>();

            //UpdateAnimationCooldowns(_cancellationTokenSource.Token, GameSyncManager.TickSeconds).Forget();
            if (NetworkIdentity.isLocalPlayer)
            {                
                _playerAnimationKey = new BindingKey(UIPropertyDefine.Animation, DataScope.LocalPlayer, UIPropertyBinder.LocalPlayerId);

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
                var playerAnimationOverlay = uiManager.SwitchUI<PlayerAnimationOverlay>();
                var animationStateDataDict =
                    UIPropertyBinder.GetReactiveDictionary<AnimationStateData>(_playerAnimationKey);
                playerAnimationOverlay.BindPlayerAnimationData(animationStateDataDict);
            }
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
            // foreach (var inputStateData in propertyState.PlayerAnimationCooldownState.AnimationCooldowns)
            // {
            //     Debug.Log($"[ApplyServerState] - {inputStateData.Value.ToString()}");
            // }

            _isApplyingState = false;
        }

        [Client]
        private void UpdateUIAnimation(MemoryDictionary<AnimationState, CooldownSnapshotData> snapshot)
        {
            if (!NetworkIdentity.isLocalPlayer)
            {
                return;
            }
            var animationStateDataDict =
                UIPropertyBinder.GetReactiveDictionary<AnimationStateData>(_playerAnimationKey);
            
            foreach (var kvp in snapshot)
            {
                if (animationStateDataDict.TryGetValue((int)kvp.Key, out var animationData))
                {
                    //Debug.Log($"[UpdateUIAnimation] {animationData.ToString()}");
                    if (!Mathf.Approximately(animationData.Timer, kvp.Value.CurrentCountdown))
                    {
                        animationData.Timer = kvp.Value.CurrentCountdown;
                    }
                    animationData.Index = kvp.Value.CurrentStage;
                    animationStateDataDict[(int)kvp.Key] = animationData;
                }
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
        [Client]
        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType == CommandType.Input && command is InputCommand inputCommand && IsInSpecialState?.Invoke() == false)
            {
                // if (inputCommand.CommandAnimationState == AnimationState.Attack)
                // {
                //     Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                // }
                //Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                var info = _animationConfig.GetAnimationInfo(inputCommand.CommandAnimationState);
                var actionType = info.actionType;
                var health = _propertyPredictionState.GetProperty(PropertyTypeEnum.Health);
                SkillConfigData skillConfigData = default;
                if (inputCommand.CommandAnimationState == AnimationState.SkillE || inputCommand.CommandAnimationState == AnimationState.SkillQ)
                { 
                    skillConfigData = _skillSyncState.GetSkillConfigData(inputCommand.CommandAnimationState);
                }
                var cost = skillConfigData.id == 0 ? info.cost : skillConfigData.cost;
                var cooldown = skillConfigData.id == 0 ? info.cooldown : skillConfigData.cooldown;
                //Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                if (health <= 0)
                {
                    Debug.Log($"[PlayerInputPredictionState] - Player is dead.");
                    return;
                }
                if (actionType != ActionType.Movement)
                {
                    //Debug.Log($"[PlayerInputPredictionState] - Not enough strength to perform {inputCommand.CommandAnimationState}.");
                    return;
                }

                var animationCooldowns = PlayerComponentController.GetNowAnimationCooldownsDict();
                var cooldownInfo = animationCooldowns.GetValueOrDefault(inputCommand.CommandAnimationState);
                if (cooldown > 0)
                {
                    if (cooldownInfo == null)
                    {
                        Debug.LogWarning($"Animation {inputCommand.CommandAnimationState} is not registered in cooldown.");
                        return;
                    }
                    if (!cooldownInfo.IsReady())
                    {
                        Debug.LogWarning($"Animation {inputCommand.CommandAnimationState} is on cooldown.");
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
                    var animationCommand = ObjectPoolManager<PropertyClientAnimationCommand>.Instance.Get(50);
                    animationCommand.AnimationState = noStrengthState == AnimationState.None ? inputCommand.CommandAnimationState : noStrengthState;
                    animationCommand.Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property, CommandAuthority.Client);
                    animationCommand.SkillId = skillConfigData.id;
                    _propertyPredictionState.AddPredictedCommand(animationCommand);
                    if (cooldownInfo != null)
                    {
                        cooldownInfo.Use();
                        Debug.Log($"[Simulate] [Normal] - CommandAnimationState:{inputCommand.CommandAnimationState} - cooldownInfo:{cooldownInfo.AnimationState} - cooldown:{cooldown} - cost:{cost}");

                    }

                }

                if (skillConfigData.animationState != AnimationState.None)
                {
                    Debug.Log($"[Simulate] [Skill] - CommandAnimationState:{inputCommand.CommandAnimationState} - cooldown:{cooldown} - cost:{cost}");

                    cooldownInfo?.Use(); 
                }

                var inputStateData = ObjectPoolManager<PlayerInputStateData>.Instance.Get(50);
                inputStateData.InputMovement = inputCommand.InputMovement.ToVector3();
                inputStateData.InputAnimations = inputCommand.InputAnimationStates;
                inputStateData.Command = inputCommand.CommandAnimationState;
                PlayerComponentController.HandlePlayerSpecialAction(inputStateData.Command);
                OnPlayerInputStateChanged?.Invoke(inputStateData);
                //Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
            }
        }

        // private async UniTaskVoid UpdateAnimationCooldowns(CancellationToken token, float deltaTime)
        // {
        //     while (token.IsCancellationRequested == false && !_isApplyingState)
        //     {
        //         await UniTask.Delay(TimeSpan.FromSeconds(deltaTime), cancellationToken: token);
        //         PlayerComponentController.UpdateAnimation(deltaTime);
        //     }
        // }
    }
}