using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.Client.Player;
using HotUpdate.Scripts.Network.NetworkMes;
using Mirror;
using Network.NetworkMes;
using Tool.Message;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Server.Sync
{
    public class FrameSyncManager : NetworkBehaviour
    {
        private const int BUFFER_FRAMES = 2; // 缓冲帧数
        private readonly Dictionary<uint, List<PlayerInputInfo>> _frameInputs = new Dictionary<uint, List<PlayerInputInfo>>();
        private readonly Dictionary<uint, PlayerControlClient> _players = new Dictionary<uint, PlayerControlClient>();
        private readonly Dictionary<uint, List<AttackData>> _attackDatas = new Dictionary<uint, List<AttackData>>();
        private MirrorNetworkMessageHandler _messageCenter;
        private float _accumulator;  // 用于累积固定更新的时间
        private const float FIXED_TIME_STEP = 0.01f;  // 100fps的固定更新间隔
        [SyncVar]
        private uint _currentFrame;  // 使用SyncVar确保服务器和客户端帧号同步

        [Inject]
        private void Init(MirrorNetworkMessageHandler messageCenter)
        {
            Reader<AttackData>.read = AttackDataExtensions.ReadAttackData;
            Writer<AttackData>.write = AttackDataExtensions.WritePlayerAttackData;
            Reader<PlayerAttackData>.read = AttackDataExtensions.ReadPlayerAttackData;
            Writer<PlayerAttackData>.write = AttackDataExtensions.WriteAttackData;
            _messageCenter = messageCenter;
            _messageCenter.RegisterLocalMessageHandler<PlayerInputMessage>(OnPlayerInputMessage);
            _messageCenter.RegisterLocalMessageHandler<PlayerFrameUpdateMessage>(OnPlayerFrameUpdateMessage);
            _messageCenter.RegisterLocalMessageHandler<PlayerAttackMessage>(OnPlayerAttackMessage);
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
            _currentFrame = 0;  // 服务器启动时初始化帧号
        }

        private void Update()
        {
            if (!isServer) return;

            // 使用时间累加器来确保固定帧率
            _accumulator += Time.deltaTime;
            while (_accumulator >= FIXED_TIME_STEP)
            {
                ProcessFrame();
                ProcessAttack();
                _accumulator -= FIXED_TIME_STEP;
            }
        }

        private void ProcessAttack()
        {
            if (_attackDatas.TryGetValue(_currentFrame, out var attackList))
            {
                _messageCenter.SendToAllClients(new MirrorFrameAttackResultMessage
                {
                    
                });
            }
        }

        private void ProcessFrame()
        {
            if (_frameInputs.TryGetValue(_currentFrame, out var inputs))
            {
                // 广播帧更新
                _messageCenter.SendToAllClients(new MirrorFrameUpdateMessage
                {
                    frame = _currentFrame,
                    playerInputs = inputs
                });

                // 清理已处理的帧数据
                _frameInputs.Remove(_currentFrame);
            }
            _currentFrame++;
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
                    if (NetworkClient.spawned.TryGetValue(input.playerId, out var identity))
                    {
                        player = identity.GetComponent<PlayerControlClient>();
                        _players[input.playerId] = player;
                    }
                    else
                    {
                        Debug.LogWarning($"Player {input.playerId} not found");
                        continue;
                    }
                }
                player?.ExecuteInput(input);
            }
        }

        // 获取当前帧号（客户端可用）
        public uint GetCurrentFrame()
        {
            return _currentFrame;
        }

        // 清理旧的帧数据
        private void CleanupOldFrames()
        {
            var oldFrames = _frameInputs.Keys.Where(frame => frame < _currentFrame - BUFFER_FRAMES);
            foreach (var frame in oldFrames)
            {
                _frameInputs.Remove(frame);
            }
        }

        private void OnDestroy()
        {
            if (_messageCenter)
            {
                _messageCenter.UnregisterLocalMessageHandler<PlayerInputMessage>(OnPlayerInputMessage);
                _messageCenter.UnregisterLocalMessageHandler<PlayerFrameUpdateMessage>(OnPlayerFrameUpdateMessage);
            }
        }
    }

    public static class AttackDataExtensions
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
            var attackerId = reader.ReadUInt();
            var attackDirection = reader.ReadVector3();
            var attackOrigin = reader.ReadVector3();
            return new AttackData
            {
                angle = angle,
                radius = radius,
                minHeight = minHeight,
                attack = attack,
                attackerId = attackerId,
                attackDirection = attackDirection,
                attackOrigin = attackOrigin,
            };
        }

        public static void WritePlayerAttackData(NetworkWriter writer, AttackData value)
        {
            writer.WriteFloat(value.angle);
            writer.WriteFloat(value.radius);
            writer.WriteFloat(value.minHeight);
            writer.WriteFloat(value.attack);
            writer.WriteUInt(value.attackerId);
            writer.WriteVector3(value.attackDirection);
            writer.WriteVector3(value.attackOrigin);
        }
    }
}