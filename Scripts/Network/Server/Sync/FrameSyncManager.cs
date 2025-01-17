using System;
using System.Collections.Generic;
using System.Linq;
using DG.Tweening;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.NetworkMes;
using Mirror;
using Network.NetworkMes;
using Tool.Message;
using UnityEngine;
using VContainer;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Server.Sync
{
    public class FrameSyncManager : NetworkBehaviour
    {
        private const int BUFFER_FRAMES = 2; // 缓冲帧数
        private double _stateUpdateInterval;  // 状态更新间隔
        private readonly float _inputLagTolerance = 0.5f;    // 输入延迟容忍度
        private readonly Dictionary<int, Queue<InputData>> _playerInputs = new Dictionary<int, Queue<InputData>>();
        private readonly Dictionary<int, float> _lastInputTimes = new Dictionary<int, float>();
        private readonly Dictionary<uint, List<PlayerInputInfo>> _frameInputs = new Dictionary<uint, List<PlayerInputInfo>>();
        private readonly Dictionary<int, PlayerControlClient> _players = new Dictionary<int, PlayerControlClient>();
        private readonly Dictionary<uint, List<AttackData>> _attackDatas = new Dictionary<uint, List<AttackData>>();
        private readonly Dictionary<int, int> _lastProcessedInputs = new Dictionary<int, int>();  // 记录每个玩家最后处理的输入序号
        private MirrorNetworkMessageHandler _messageCenter;
        private JsonDataConfig _jsonConfig;
        private double _accumulator;  // 用于累积固定更新的时间
        private const double SyncFps = 30;  // 最大更新间隔
        private float _lastStateUpdateTime;
        private int _stateSequence;

        [Inject]
        private void Init(MirrorNetworkMessageHandler messageCenter, IConfigProvider configProvider)
        {
            Reader<AttackData>.read = AttackDataExtensions.ReadAttackData;
            Writer<AttackData>.write = AttackDataExtensions.WritePlayerAttackData;
            Reader<PlayerAttackData>.read = AttackDataExtensions.ReadPlayerAttackData;
            Writer<PlayerAttackData>.write = AttackDataExtensions.WriteAttackData;
            _stateUpdateInterval = 1 / SyncFps;
            _messageCenter = messageCenter;
            _jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            _messageCenter.RegisterLocalMessageHandler<PlayerInputInfoMessage>(OnPlayerInputInfoMessage);
            _messageCenter.RegisterLocalMessageHandler<PlayerInputMessage>(OnPlayerInputMessage);
            _messageCenter.RegisterLocalMessageHandler<PlayerAttackMessage>(OnPlayerAttackMessage);
            _messageCenter.RegisterLocalMessageHandler<PlayerDamageResultMessage>(OnPlayerDamageResultMessage);
        }

        private void OnPlayerInputInfoMessage(PlayerInputInfoMessage message)
        {
            // 更新最后输入时间
            _lastInputTimes[message.ConnectionId] = Time.time;

            // 将输入加入队列
            if (!_playerInputs.ContainsKey(message.ConnectionId))
            {
                _playerInputs[message.ConnectionId] = new Queue<InputData>();
            }
            _playerInputs[message.ConnectionId].Enqueue(message.Input);
            if (!_lastProcessedInputs.ContainsKey(message.ConnectionId))
            {
                _lastProcessedInputs[message.ConnectionId] = 0;
            }
            _lastProcessedInputs[message.ConnectionId] = message.Input.sequence;
        }

        private GameObject GetPlayer(int playerId)
        {
            if (NetworkServer.connections.TryGetValue(playerId, out var identity))
            {
                return identity.identity.gameObject;
            }
            Debug.LogWarning($"Player {playerId} not found");
            return null;
        }

        private void OnPlayerDamageResultMessage(PlayerDamageResultMessage message)
        {
            foreach (var result in message.DamageResults)
            {
                var spawnedPlayer = GetPlayer(result.targetId);
                if (spawnedPlayer)
                {
                    var damageJudgement = spawnedPlayer.GetComponent<PlayerDamageJudgement>();
                    damageJudgement?.TakeDamage(result);
                }
            }
            Debug.Log($"Damage results in frame {message.Frame} : {message.DamageResults.Count}");
        }

        private void OnPlayerAttackMessage(PlayerAttackMessage message)
        {
            if (!_attackDatas.ContainsKey(message.Frame))
            {
                _attackDatas[message.Frame] = new List<AttackData>();
            }
            _attackDatas[message.Frame].Add(message.PlayerAttackData);
        }

        public override void OnStartServer()
        {
            base.OnStartServer();
            _stateUpdateInterval = 1 / SyncFps;
        }

        private void Update()
        {
            if (!isServer) return;

            while (Time.time - _lastStateUpdateTime >= _stateUpdateInterval)
            {
                ProcessInputs();
                BroadcastGameState();
                ProcessPlayerRecovery();
                _lastStateUpdateTime = Time.time;
            }
        }
        
        private void ProcessInputs()
        {
            var currentTime = Time.time;
            var frameInputs = new Dictionary<int, InputData>();
            // 处理所有玩家的输入队列
            foreach (var (connectionId, inputQueue) in _playerInputs)
            {
                // 检查玩家是否超时
                if (currentTime - _lastInputTimes[connectionId] > _inputLagTolerance)
                {
                    continue;
                }

                // 处理该玩家的所有待处理输入
                while (inputQueue.Count > 0)
                {
                    var input = inputQueue.Dequeue();
                    frameInputs.TryAdd(connectionId, input);
                    if (_players.TryGetValue(connectionId, out var controller))
                    {
                        ActionType actionType = _jsonConfig.GetActionType(input.command);

                        // 服务器端处理逻辑
                        switch (actionType)
                        {
                            case ActionType.Movement:
                                // 服务器也执行移动，作为权威状态
                                controller.ExecutePlayerLocalInput(input);
                                break;

                            case ActionType.Interaction:
                                // 服务器验证并执行交互动作
                                controller.ExecuteServerAction(input);
                                break;
                        }
                    }
                }
            }

            if (frameInputs.Count > 0)
            {
                foreach (var kvp in frameInputs)
                {
                    RpcReceiveFrameInputs(kvp.Key, kvp.Value);
                }
            }
        } 
        
        [ClientRpc]
        private void RpcReceiveFrameInputs(int connectionId, InputData input)
        {
            if (_players.TryGetValue(connectionId, out var controller))
            {
                var actionType = _jsonConfig.GetActionType(input.command);
                switch (actionType)
                {
                    case ActionType.Movement:
                        if (connectionId == NetworkClient.connection.connectionId)
                            break;
                        controller.ExecutePlayerLocalInput(input);
                        break;
                    case ActionType.Animation:
                        controller.ExecuteAnimationInput(input);
                        break;
                    case ActionType.Interaction:
                        controller.ExecuteServerAction(input);
                        break;
                }
            }
        }
        
        private void BroadcastGameState()
        {
            // 为每个玩家创建并发送状态
            foreach (var kvp in _players)
            {
                int connectionId = kvp.Key;
                var controller = kvp.Value;

                var state = new ServerState
                {
                    lastProcessedInput = _lastProcessedInputs.GetValueOrDefault(connectionId, 0),
                    timestamp = Time.time,
                    position = controller.transform.position,
                    velocity = controller.GetComponent<Rigidbody>().velocity,
                    rotation = controller.transform.rotation,
                    actionType = _jsonConfig.GetActionType(controller.CurrentRequestAnimationState),
                    command = controller.CurrentRequestAnimationState
                };

                RpcUpdateState(connectionId, state);
            }

            _stateSequence++;
        }
        
        [ClientRpc]
        private void RpcUpdateState(int connectionId, ServerState state)
        {
            if (!_players.TryGetValue(connectionId, out var controller))
                return;

            // 本地玩家进行状态和解
            if (controller.isLocalPlayer)
            {
                controller.ReconcileState(state);
            }
            // 其他玩家直接更新状态
            else
            {
                var rb = controller.GetComponent<Rigidbody>();
                controller.transform.DOMove(state.position, (float)_stateUpdateInterval).SetEase(Ease.Linear);
                //controller.transform.DORotateQuaternion(state.rotation, (float)_stateUpdateInterval).SetEase(Ease.Linear);
                DOTween.To(() => rb.velocity, x => rb.velocity = x, state.velocity, (float)_stateUpdateInterval).SetEase(Ease.Linear);
            }
        }

        private void ProcessPlayerRecovery()
        {
            foreach (var player in _players.Values)
            {
                var playerComponent = player.GetComponent<PlayerPropertyComponent>();
                var animationComponent = player.GetComponent<PlayerAnimationComponent>();
                if (playerComponent && animationComponent)
                {
                    var isSprinting = animationComponent.NowAnimationState == AnimationState.Sprint;
                    var healthRecovery = playerComponent.GetPropertyValue(PropertyTypeEnum.HealthRecovery);
                    var strengthRecovery = playerComponent.GetPropertyValue(PropertyTypeEnum.StrengthRecovery);
                    var sprintCost = _jsonConfig.GetPlayerAnimationCost(AnimationState.Sprint);
                    strengthRecovery -= isSprinting ? sprintCost : 0;
                    playerComponent.IncreaseProperty(PropertyTypeEnum.Health, BuffIncreaseType.Current, healthRecovery * (float)_stateUpdateInterval);
                    playerComponent.IncreaseProperty(PropertyTypeEnum.Strength, BuffIncreaseType.Current, strengthRecovery * (float)_stateUpdateInterval);
                }
            }
        }

        private DamageResult GetDamageResult(AttackData attackData)
        {
            foreach (var player in _players.Values)
            {
                if (player.netId == attackData.attackerId) continue;

                var targetPos = player.transform.position;
                if (!IsInAttackRange(targetPos, attackData)) continue;
                var playerDamage = player.GetComponent<PlayerDamageJudgement>();
                if (playerDamage)
                {
                    var propertyComponent = player.GetComponent<PlayerPropertyComponent>();
                    var remainingHp = propertyComponent.GetPropertyValue(PropertyTypeEnum.Health);
                    var defense = propertyComponent.GetPropertyValue(PropertyTypeEnum.Defense);
                    var damage = _jsonConfig.GetDamage(attackData.attack, defense, attackData.criticalRate, attackData.criticalDamageRatio);
                    return new DamageResult
                    {
                        targetId = player.connectionToClient.connectionId,
                        damageAmount = damage,
                        isDead = remainingHp <= 0
                    };
                }
            }
            return default;
        }

        private bool IsInAttackRange(Vector3 targetPos, AttackData attackData)
        {
            // 计算目标是否在攻击范围内
            var toTarget = targetPos - attackData.attackOrigin;
            var distance = new Vector2(toTarget.x, toTarget.z).magnitude;
        
            // 检查距离
            if (distance > attackData.radius) return false;
        
            // 检查高度
            if (targetPos.y < attackData.minHeight) return false;
        
            // 检查角度
            var angle = Vector3.Angle(attackData.attackDirection, toTarget);
            return angle <= attackData.angle * 0.5f;
        }

        private void OnPlayerInputMessage(PlayerInputMessage message)
        {
            if (!_frameInputs.ContainsKey(message.PlayerInputInfo.frame))
            {
                _frameInputs[message.PlayerInputInfo.frame] = new List<PlayerInputInfo>();
            }
            _frameInputs[message.PlayerInputInfo.frame].Add(message.PlayerInputInfo);
        }

        private void OnPlayerFrameUpdateMessage(PlayerFrameUpdateMessage message)
        {
            foreach (var input in message.PlayerInputInfos)
            {
                if (!_players.TryGetValue(input.playerId, out var player))
                {
                    var spawnedPlayer = GetPlayer(input.playerId);
                    if (spawnedPlayer)
                    {
                        player = spawnedPlayer.GetComponent<PlayerControlClient>();
                        _players[input.playerId] = player;
                    }
                }
                player?.ExecuteInput(input);
            }
        }

        private void OnDestroy()
        {
            if (_messageCenter)
            {
                _messageCenter.UnregisterLocalMessageHandler<PlayerInputMessage>(OnPlayerInputMessage);
                _messageCenter.UnregisterLocalMessageHandler<PlayerFrameUpdateMessage>(OnPlayerFrameUpdateMessage);
                _messageCenter.UnregisterLocalMessageHandler<PlayerAttackMessage>(OnPlayerAttackMessage);
                _messageCenter.UnregisterLocalMessageHandler<PlayerDamageResultMessage>(OnPlayerDamageResultMessage);
            }
        }
    }

    internal static class AttackDataExtensions
    {
        public static void WriteAttackData(NetworkWriter writer, PlayerAttackData value)
        {
            writer.WriteFloat(value.attackAngle);
            writer.WriteFloat(value.attackRadius);
            writer.WriteFloat(value.minAttackHeight);
        }

        public static PlayerAttackData ReadPlayerAttackData(NetworkReader reader)
        {
            var angle = reader.ReadFloat();
            var radius = reader.ReadFloat();
            var minHeight = reader.ReadFloat();
            return new PlayerAttackData
            {
                attackAngle = angle,
                attackRadius = radius,
                minAttackHeight = minHeight,
            };
        }

        public static AttackData ReadAttackData(NetworkReader reader)
        {
            var angle = reader.ReadFloat();
            var radius = reader.ReadFloat();
            var minHeight = reader.ReadFloat();
            var attack = reader.ReadFloat();    
            var attackerId = reader.ReadInt();
            var attackDirection = reader.ReadVector3();
            var attackOrigin = reader.ReadVector3();
            var criticalRate = reader.ReadFloat();
            var criticalDamageRatio = reader.ReadFloat();
            return new AttackData
            {
                angle = angle,
                radius = radius,
                minHeight = minHeight,
                attack = attack,
                attackerId = attackerId,
                attackDirection = attackDirection,
                attackOrigin = attackOrigin,
                criticalRate = criticalRate,
                criticalDamageRatio = criticalDamageRatio
            };
        }

        public static void WritePlayerAttackData(NetworkWriter writer, AttackData value)
        {
            writer.WriteFloat(value.angle);
            writer.WriteFloat(value.radius);
            writer.WriteFloat(value.minHeight);
            writer.WriteFloat(value.attack);
            writer.WriteInt(value.attackerId);
            writer.WriteVector3(value.attackDirection);
            writer.WriteVector3(value.attackOrigin);
            writer.WriteFloat(value.criticalRate);
            writer.WriteFloat(value.criticalDamageRatio);
        }
    }
}

// private void ProcessAttack()
// {
//     if (_attackDatas.TryGetValue(_currentFrame, out var attackList))
//     {
//         var damageResults = new List<DamageResult>();
//         foreach (var attackData in attackList)
//         {
//             damageResults.Add(GetDamageResult(attackData));
//         }
//         _messageCenter.SendToAllClients(new MirrorFrameAttackResultMessage
//         {
//             frame = _currentFrame,
//             damageResults = damageResults
//         });
//         Debug.Log($"Attacks in frame {_currentFrame} : {attackList?.Count ?? 0}");
//     }
// }