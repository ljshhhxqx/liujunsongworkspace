using System.Collections.Generic;
using System.Linq;
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
        [SyncVar]
        private uint _currentFrame;  // 使用SyncVar确保服务器和客户端帧号同步
        private readonly Dictionary<uint, List<PlayerInputInfo>> _frameInputs = new Dictionary<uint, List<PlayerInputInfo>>();
        private readonly Dictionary<uint, PlayerControlClient> _players = new Dictionary<uint, PlayerControlClient>();
        private MirrorNetworkMessageHandler _messageCenter;
        private float _accumulator;  // 用于累积固定更新的时间
        private const float FIXED_TIME_STEP = 0.01f;  // 100fps的固定更新间隔

        [Inject]
        private void Init(MirrorNetworkMessageHandler messageCenter)
        {
            _messageCenter = messageCenter;
            _messageCenter.RegisterLocalMessageHandler<PlayerInputMessage>(OnPlayerInputMessage);
            _messageCenter.RegisterLocalMessageHandler<PlayerFrameUpdateMessage>(OnPlayerFrameUpdateMessage);
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
                _accumulator -= FIXED_TIME_STEP;
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

                // 增加帧号
                _currentFrame++;
            }
            else
            {
                // 如果当前帧没有输入，也需要推进帧号
                // 可以选择发送空帧或者使用上一帧的输入
                _currentFrame++;
            }
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

}