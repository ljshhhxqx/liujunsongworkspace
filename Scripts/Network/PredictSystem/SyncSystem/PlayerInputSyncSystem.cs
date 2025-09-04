using System;
using System.Collections.Generic;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.Server.InGame;
using MemoryPack;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationEvent = HotUpdate.Scripts.Config.ArrayConfig.AnimationEvent;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using CooldownSnapshotData = HotUpdate.Scripts.Network.PredictSystem.Data.CooldownSnapshotData;
using INetworkCommand = HotUpdate.Scripts.Network.PredictSystem.Data.INetworkCommand;
using InputCommand = HotUpdate.Scripts.Network.PredictSystem.Data.InputCommand;
using PlayerAnimationCooldownState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerAnimationCooldownState;
using PlayerGameStateData = HotUpdate.Scripts.Network.PredictSystem.State.PlayerGameStateData;
using PlayerInputState = HotUpdate.Scripts.Network.PredictSystem.State.PlayerInputState;
using PropertyAttackCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyAttackCommand;
using PropertyServerAnimationCommand = HotUpdate.Scripts.Network.PredictSystem.Data.PropertyServerAnimationCommand;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerInputSyncSystem : BaseSyncSystem
    {
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        private readonly Dictionary<int, PlayerInputPredictionState> _inputPredictionStates = new Dictionary<int, PlayerInputPredictionState>();
        private AnimationConfig _animationConfig;
        private JsonDataConfig _jsonDataConfig;
        private PlayerPropertySyncSystem _playerPropertySyncSystem;
        private SkillConfig _skillConfig;
        private PlayerSkillSyncSystem _playerSkillSyncSystem;
        private PlayerInGameManager _playerInGameManager;
        private List<IAnimationCooldown> _animationCooldownConfig;
        private CancellationTokenSource _cts = new CancellationTokenSource();
        protected override CommandType CommandType => CommandType.Input;

        [Inject]
        private void InitContainers(IConfigProvider configProvider)
        {
            _animationConfig = configProvider.GetConfig<AnimationConfig>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _skillConfig = configProvider.GetConfig<SkillConfig>();
            _playerInGameManager = PlayerInGameManager.Instance;
        }
        protected override void OnGameStart(bool isGameStarted)
        {
            if (!isGameStarted)
            {
                return;
            }
            //游戏开始才能开始倒计时
            UpdatePlayerAnimationAsync(_cts.Token, GameSyncManager.TickSeconds).Forget();
        }

        public override byte[] GetPlayerSerializedState(int connectionId)
        {
            if (PropertyStates.TryGetValue(connectionId, out var playerState))
            {
                if (playerState is PlayerInputState playerInputState)
                {
                    return NetworkCommandExtensions.SerializePlayerState(playerInputState).Item1;
                }

                Debug.LogError($"Player {connectionId} input state is not PlayerInputState.");
                return null;
            }
            Debug.LogError($"Player {connectionId} input state not found.");
            return null;
        }

        protected override void OnAllSystemInit()
        {
            _playerPropertySyncSystem = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            _playerSkillSyncSystem = GameSyncManager.GetSyncSystem<PlayerSkillSyncSystem>(CommandType.Skill);
        }

        private bool _isStartAnimation;
        private async UniTaskVoid UpdatePlayerAnimationAsync(CancellationToken token, float deltaTime)
        {
            if (_isStartAnimation)
            {
                return;
            }

            _isStartAnimation = true;
            while (!token.IsCancellationRequested)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(deltaTime), cancellationToken: token);
                UpdatePlayerAnimationCooldowns(deltaTime);
            }
        }

        private void UpdatePlayerAnimationCooldowns(float deltaTime)
        {
            foreach (var connectionsKey in NetworkServer.connections.Keys)
            {
                var playerController = GameSyncManager.GetPlayerConnection(connectionsKey);
                var inputState = PropertyStates[connectionsKey];
                if (inputState is not PlayerInputState playerInputState)
                {
                    Debug.LogError($"Player {connectionsKey} has no input state.");
                    continue;
                }
                var cooldownState = playerInputState.PlayerAnimationCooldownState;
                playerController.UpdateAnimation(deltaTime, ref cooldownState);
                // foreach (var animationState in cooldownState.AnimationCooldowns)
                // {
                //     var cooldown = animationState.Value;
                //     Debug.Log($"[UpdatePlayerAnimationCooldowns] Player {connectionsKey} has cooldown {cooldown.ToString()}.");
                // }

                playerInputState.PlayerAnimationCooldownState = cooldownState;
                PropertyStates[connectionsKey] = playerInputState;
            }
        }

        protected override void OnClientProcessStateUpdate(int connectionId, byte[] state, CommandType commandType)
        {
            if (commandType!= CommandType.Input)
            {
                return;
            }
            var playerStates = NetworkCommandExtensions.DeserializePlayerState(state);
            if (playerStates is not PlayerInputState playerInputState)
            {
                Debug.LogError($"Player {playerStates.GetStateType().ToString()} input state is not PlayerInputState.");
                return;
            }
            if (PropertyStates.ContainsKey(connectionId))
            {
                PropertyStates[connectionId] = playerStates;
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerInputPredictionState>();
            var playerInputState = new PlayerInputState(new PlayerGameStateData(),
                new PlayerAnimationCooldownState(GetStateAnimationCooldowns()));
            PropertyStates.TryAdd(connectionId, playerInputState);
            _inputPredictionStates.TryAdd(connectionId, playerPredictableState);
            RpcSetPlayerInputState(connectionId, NetworkCommandExtensions.SerializePlayerState(playerInputState).Item1);
            BindAniEvents(connectionId);
        }

        private MemoryDictionary<AnimationState, CooldownSnapshotData> GetStateAnimationCooldowns()
        {
            var dic = new MemoryDictionary<AnimationState, CooldownSnapshotData>(_animationConfig.AnimationInfos.Count);
            for (int i = 0; i < _animationConfig.AnimationInfos.Count; i++)
            {
                var animationInfo = _animationConfig.AnimationInfos[i];
                if (!animationInfo.showInHud)
                {
                    continue;
                }
                var data = ObjectPoolManager<CooldownSnapshotData>.Instance.Get(_animationConfig.AnimationInfos.Count);
                data.AnimationState = animationInfo.state;
                data.Cooldown = animationInfo.cooldown;
                data.AnimationSpeed = 1;
                if (animationInfo.keyframeData == null || animationInfo.keyframeData.Length == 0)
                {
                    dic.Add(animationInfo.state, data);
                    continue;
                }
                data.AttackWindow = 0;
                data.ResetCooldownWindow = animationInfo.keyframeData[0].resetCooldownWindowTime;
                data.MaxAttackCount = animationInfo.keyframeData.Length;
                data.IsInComboWindow = true;
                dic.Add(animationInfo.state, data);
            }

            return dic;
        }

        [ClientRpc]
        private void RpcSetPlayerInputState(int connectionId, byte[] playerInputState)
        {
            var player = GameSyncManager.GetPlayerConnection(connectionId);
            var syncState = player.GetComponent<PlayerInputPredictionState>();
            var playerState = NetworkCommandExtensions.DeserializePlayerState(playerInputState);
            syncState.InitCurrentState(playerState);
        }

        [Server]
        private void BindAniEvents(int connectionId)
        {
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            var animationCooldowns = playerController.GetNowAnimationCooldownsDict();
            var attackCooldown = animationCooldowns.GetValueOrDefault(AnimationState.Attack);
            if (attackCooldown is KeyframeComboCooldown attackComboCooldown)
            {
                attackComboCooldown.EventStream
                    .Where(x => x == AnimationEvent.OnAttack)
                    .Subscribe(x => HandlePlayerAttack(connectionId))
                    .AddTo(_disposables);
            }
            var rollCooldown = animationCooldowns.GetValueOrDefault(AnimationState.Roll);
            if (rollCooldown is KeyframeCooldown cooldown)
            {
                cooldown.EventStream
                    .Where(x => x == AnimationEvent.OnRollStart)
                    .Subscribe(x => HandlePlayerRoll(connectionId, true))
                    .AddTo(_disposables);
                cooldown.EventStream
                    .Where(x => x == AnimationEvent.OnRollStop)
                    .Subscribe(x => HandlePlayerRoll(connectionId, false))
                    .AddTo(_disposables);
            }
            var skillECooldown = animationCooldowns.GetValueOrDefault(AnimationState.SkillE);
            if (skillECooldown is KeyframeCooldown eCooldown)
            {
                eCooldown.EventStream
                    .Where(x => x == AnimationEvent.OnSkillCastE)
                    .Subscribe(x => HandlePlayerSkill(connectionId, AnimationState.SkillE))
                    .AddTo(_disposables);
            }
            var skillQCooldown = animationCooldowns.GetValueOrDefault(AnimationState.SkillQ);
            if (skillQCooldown is KeyframeCooldown qCooldown)
            {
                qCooldown.EventStream
                    .Where(x => x == AnimationEvent.OnSkillCastQ)
                    .Subscribe(x => HandlePlayerSkill(connectionId, AnimationState.SkillQ))
                    .AddTo(_disposables);
            }
        }

        private void HandlePlayerSkill(int connectionId, AnimationState animState)
        {
            var skillConfigData = _playerSkillSyncSystem.GetSkillConfigData(animState, connectionId);
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            GameSyncManager.EnqueueServerCommand(new SkillCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Skill, CommandAuthority.Server, CommandExecuteType.Immediate),
                SkillConfigId = skillConfigData.id,
                KeyCode = animState,
                IsAutoSelectTarget = true,
                DirectionNormalized = playerController.transform.forward,
            });
        }

        private void HandlePlayerRoll(int connectionId, bool isRollStart)
        {
            GameSyncManager.EnqueueServerCommand(new PropertyInvincibleChangedCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate),
                IsInvincible = isRollStart,
            });
        }

        private void HandlePlayerAttack(int connectionId)
        {
            Debug.Log($"[HandlePlayerAttack] player {connectionId} attack");
            var playerController = GameSyncManager.GetPlayerConnection(connectionId);
            var propertySyncSystem = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            var playerProperty = propertySyncSystem.GetPlayerProperty(connectionId);
            var attackConfigData = new AttackConfigData(playerProperty[PropertyTypeEnum.AttackRadius].CurrentValue, playerProperty[PropertyTypeEnum.AttackAngle].CurrentValue, playerProperty[PropertyTypeEnum.AttackHeight].CurrentValue);
            var defenders = playerController.HandleAttack(new AttackParams(playerController.transform.position,
                playerController.transform.forward, connectionId, playerController.netId, attackConfigData));
            if (defenders.Length > 0)
            {

                GameSyncManager.EnqueueServerCommand(new PropertyAttackCommand
                {
                    Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate),
                    AttackerId = connectionId,
                    TargetIds = defenders,
                });
            }
            var attack = propertySyncSystem.GetPlayerProperty(connectionId, PropertyTypeEnum.Attack);
            var triggerParams = AttackCheckerParameters.CreateParameters(TriggerType.OnAttack,
                AttackRangeType.None, attack);
            GameSyncManager.EnqueueServerCommand(new TriggerCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate),
                TriggerType = TriggerType.OnAttack,
                TriggerData = MemoryPackSerializer.Serialize(triggerParams),
            });
        }

        public override CommandType HandledCommandType => CommandType.Input;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            //UpdatePlayerAnimationAsync(_cts.Token, GameSyncManager.TickSeconds).Forget();
            var header = command.GetHeader();
            if (!PropertyStates.ContainsKey(header.ConnectionId) || PropertyStates[header.ConnectionId] is not PlayerInputState playerInputState)
                return null;
            if (command is InputCommand inputCommand)
            {
                // if (inputCommand.CommandAnimationState == AnimationState.Attack)
                // {
                //     Debug.Log($"[PlayerInputPredictionState] - Simulate {inputCommand.CommandAnimationState} with {inputCommand.InputMovement} input.");
                // }
                //Debug.Log($"[PlayerInputSyncSystem]Player {header.ConnectionId} input command {inputCommand.InputMovement} {inputCommand.InputAnimationStates}");
                var playerSyncSystem = GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
                var playerController = GameSyncManager.GetPlayerConnection(header.ConnectionId);
                if (playerController.IsInSpecialState())
                {
                    Debug.LogWarning($"[playerInputSyncSystem]Player {header.ConnectionId} is in special state, cannot input.");
                    return playerInputState;
                }
                var playerProperty = playerSyncSystem.GetPlayerProperty(header.ConnectionId);
                //验证玩家是否存在或者是否已死亡
                if (playerProperty == null || playerProperty[PropertyTypeEnum.Health].CurrentValue <= 0)
                {
                    Debug.LogWarning($"[playerInputSyncSystem]Player {header.ConnectionId} is not exist or is dead.");
                    
                    return playerInputState;
                }

                var inputStateData = ObjectPoolManager<PlayerInputStateData>.Instance.Get(50);
                inputStateData.InputMovement = inputCommand.InputMovement.ToVector3();
                inputStateData.InputAnimations = inputCommand.InputAnimationStates;
                
                //获取可以执行的动画
                var commandAnimation = playerController.GetCurrentAnimationState(inputStateData);
                inputStateData.Command = commandAnimation;

                inputCommand.CommandAnimationState = commandAnimation;
                var actionType = _animationConfig.GetActionType(inputCommand.CommandAnimationState);
                //Debug.Log($"[PlayerInputSyncSystem]Player {header.ConnectionId} input command {inputCommand.InputMovement} {inputCommand.InputAnimationStates} action type {actionType}");
                if (actionType != ActionType.Interaction && actionType != ActionType.Movement)
                {
                    //Debug.LogWarning($"Player {header.ConnectionId} input animation {inputCommand.CommandAnimationState} is not supported.");
                    return playerInputState;
                }
                
                var playerAnimationCooldowns = playerController.GetNowAnimationCooldownsDict();
                if (playerAnimationCooldowns.Count == 0)
                {
                    Debug.LogWarning($"[playerInputSyncSystem]Player {header.ConnectionId} input animation {inputCommand.CommandAnimationState} is not exist.");
                    return playerInputState;
                }
                var info = _animationConfig.GetAnimationInfo(commandAnimation);
                var skillConfigData = _playerSkillSyncSystem.GetSkillConfigData(inputCommand.CommandAnimationState, header.ConnectionId);
                var cost = skillConfigData.id == 0 ? info.cost : skillConfigData.cost;
                var cooldown = skillConfigData.id == 0 ? info.cooldown : skillConfigData.cooldown;
                //验证冷却时间是否已到
                var cooldownInfo = playerAnimationCooldowns.GetValueOrDefault(commandAnimation);

                //Debug.Log($"[PlayerInputSyncSystem]Player {header.ConnectionId} input animation {inputCommand.CommandAnimationState} cooldown {cooldown} cost {cost}");
                if (cooldown != 0)
                {
                    if (cooldownInfo == null)
                    {
                        Debug.LogWarning($"Player {header.ConnectionId} input animation {commandAnimation} is not registered.");
                        return playerInputState;
                    }

                    if (!cooldownInfo.IsReady())
                    {
                        Debug.LogWarning($"Player {header.ConnectionId} input animation {commandAnimation} is not ready");
                        return playerInputState;
                    }
                }

                if (cost > 0)
                {
                    //验证是否耐力值足够
                    if (!_animationConfig.IsStrengthEnough(inputCommand.CommandAnimationState, playerProperty[PropertyTypeEnum.Strength].CurrentValue, out var newState, GameSyncManager.TickSeconds)&& newState == AnimationState.None)
                    {
                        Debug.LogWarning($"Player {header.ConnectionId} input animation {commandAnimation} cost {info.cost} strength, but strength is {playerProperty[PropertyTypeEnum.Strength].CurrentValue}.");
                        return playerInputState;
                    }

                    if (skillConfigData.id == 0)
                    {
                        var animationCommand = ObjectPoolManager<PropertyServerAnimationCommand>.Instance.Get();
                        animationCommand.Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate);
                        animationCommand.AnimationState = newState == AnimationState.None ? commandAnimation : newState;
                        animationCommand.SkillId = skillConfigData.id;
                        GameSyncManager.EnqueueServerCommand(animationCommand);
                        //ObjectPoolManager<PropertyServerAnimationCommand>.Instance.Return(animationCommand);
                        //Debug.Log($" [playerInputSyncSystem]Player {header.ConnectionId} input animation {commandAnimation} cost {info.cost} strength, now strength is {playerProperty[PropertyTypeEnum.Strength].CurrentValue}.");
                    }


                    // if (skillConfigData.animationState != AnimationState.None)
                    cooldownInfo?.Use();
                    // else if (skillConfigData.id > 0 && skillConfigData.animationState != AnimationState.None)
                    // {
                    //     var animationCommand = ObjectPoolManager<PropertyServerAnimationCommand>.Instance.Get(50);
                    //     animationCommand.Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Property);
                    //     animationCommand.AnimationState = skillConfigData.animationState;
                    //     animationCommand.SkillId = skillConfigData.id;
                    //     GameSyncManager.EnqueueServerCommand(animationCommand);
                    //     //Debug.Log($"[PlayerInputSyncSystem]Player {header.ConnectionId} input skill {skillConfigData.id} animation {skillConfigData.animationState} cooldown {skillConfigData.cooldown} cost {skillConfigData.cost}");
                    //
                    //     cooldownInfo?.Use();
                    // }

                }
                
                var playerGameStateData = playerController.HandleServerMoveAndAnimation(inputStateData);
                var inputMovement = inputCommand.InputMovement.ToVector3();
                // var cooldowns = playerInputState.PlayerAnimationCooldownState.AnimationCooldowns;
                // GetCooldownSnapshotData(header.ConnectionId, cooldowns);
                playerInputState.PlayerGameStateData = playerGameStateData;
                //playerInputState.PlayerAnimationCooldownState.AnimationCooldowns = cooldowns;
                //todo:**必须优化//
                PropertyStates[header.ConnectionId] = playerInputState;
                playerController.RpcHandlePlayerSpecialAction(playerGameStateData.AnimationState);
                //Debug.Log($"[PlayerInputSyncSystem]Player {header.ConnectionId} input animation {inputCommand.CommandAnimationState} cooldown {cooldown} cost {cost} player state {playerGameStateData.AnimationState}");
                if (inputMovement.magnitude > 0.1f && playerGameStateData.AnimationState == AnimationState.Move || playerGameStateData.AnimationState == AnimationState.Sprint)
                {
                    var moveSpeed = playerSyncSystem.GetMoveSpeed(header.ConnectionId);
                    var equipmentSyncSystem = GameSyncManager.GetSyncSystem<PlayerEquipmentSystem>(CommandType.Equipment);
                    if (!equipmentSyncSystem.TryGetPlayerConditionChecker(header.ConnectionId, TriggerType.OnMove, out var moveConditionChecker))
                    {
                        return playerInputState;
                    }
                    var moveCheckerParameters = MoveCheckerParameters.CreateParameters(
                        TriggerType.OnMove, moveSpeed, moveSpeed * inputMovement.magnitude * GameSyncManager.TickSeconds);
                    var triggerCommand = ObjectPoolManager<TriggerCommand>.Instance.Get(50);
                    triggerCommand.Header = GameSyncManager.CreateNetworkCommandHeader(header.ConnectionId, CommandType.Equipment, CommandAuthority.Server, CommandExecuteType.Immediate);
                    triggerCommand.TriggerType = TriggerType.OnMove;
                    triggerCommand.TriggerData = MemoryPackSerializer.Serialize(moveCheckerParameters);
                    GameSyncManager.EnqueueServerCommand(triggerCommand);
                    //Debug.Log($"[PlayerInputSyncSystem]Player {header.ConnectionId} input move {inputCommand.InputMovement} speed {moveSpeed} player state {playerGameStateData.AnimationState}");
                }
                return PropertyStates[header.ConnectionId];
            }

            if (command is SkillLoadOverloadAnimationCommand skillLoadOverloadAnimationCommand)
            {
                var playerController = GameSyncManager.GetPlayerConnection(header.ConnectionId);
                var playerAnimationCooldowns = playerController.GetNowAnimationCooldownsDict();
                if (playerAnimationCooldowns.Count == 0)
                {
                    Debug.LogWarning($"[playerInputSyncSystem]Player {header.ConnectionId} input animation {skillLoadOverloadAnimationCommand.KeyCode} is not exist.");
                    return playerInputState;
                }
                var cooldownInfo = playerAnimationCooldowns.GetValueOrDefault(skillLoadOverloadAnimationCommand.KeyCode);
                if (cooldownInfo == null)
                {
                    Debug.LogWarning($"Player {header.ConnectionId} input animation {skillLoadOverloadAnimationCommand.KeyCode} is not registered.");
                    return playerInputState;
                }
                cooldownInfo.SkillModifyCooldown(skillLoadOverloadAnimationCommand.Cooldowntime);
            }

            if (command is PlayerDeathCommand)
            {
                playerInputState.PlayerGameStateData.Velocity = CompressedVector3.FromVector3( Vector3.zero);
                playerInputState.PlayerGameStateData.AnimationState = AnimationState.Dead;
                return playerInputState;
            }
            
            if (command is PlayerRebornCommand playerRebornCommand)
            {
                playerInputState.PlayerGameStateData.Velocity = CompressedVector3.FromVector3( Vector3.zero);
                playerInputState.PlayerGameStateData.AnimationState = AnimationState.Idle;
                playerInputState.PlayerAnimationCooldownState = playerInputState.PlayerAnimationCooldownState.Reset(playerInputState.PlayerAnimationCooldownState);
                playerInputState.PlayerGameStateData.Position = playerRebornCommand.RebornPosition;
                playerInputState.PlayerGameStateData.Quaternion = CompressedQuaternion.FromQuaternion(Quaternion.identity);
                playerInputState.PlayerGameStateData.PlayerEnvironmentState = PlayerEnvironmentState.OnGround;
                return playerInputState;
            }

            if (command is SkillChangedCommand skillChangedCommand)
            {
                var playerController = GameSyncManager.GetPlayerConnection(header.ConnectionId);
                var playerAnimationCooldowns = playerController.GetNowAnimationCooldownsDict();
                var animation = playerAnimationCooldowns.GetValueOrDefault(skillChangedCommand.AnimationState);
                var skillConfigData = _playerSkillSyncSystem.GetSkillConfigData(skillChangedCommand.AnimationState, header.ConnectionId);
                animation?.SkillModifyCooldown(skillConfigData.cooldown);
                return playerInputState;
            }

            return playerInputState;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _inputPredictionStates[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _disposables.Dispose();
            _disposables.Clear();
            _cts?.Cancel();
            _cts?.Dispose();
            _inputPredictionStates.Clear();
        }
    }
}