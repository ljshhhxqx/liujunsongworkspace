using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Network.Client.Player;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Server.Sync
{
    public class FrameSyncManager : NetworkBehaviour
    {
        private const int BUFFER_FRAMES = 2; // 缓冲帧数
        private uint _currentFrame;
        private Dictionary<uint, List<PlayerInput>> _frameInputs = new Dictionary<uint, List<PlayerInput>>();
    
        private float _fixedDeltaTime;

        private void Start()
        {
            _fixedDeltaTime = Time.fixedDeltaTime;
            Reader<PlayerInput>.read += ReadInput;
            Writer<PlayerInput>.write += WriteInput;
        }
        
        private void OnDestroy()
        {
            Reader<PlayerInput>.read -= ReadInput;
            Writer<PlayerInput>.write -= WriteInput;
        }

        private PlayerInput ReadInput(NetworkReader reader)
        {
            var input = new PlayerInput
            {
                frame = reader.ReadUInt(),
                playerId = reader.ReadUInt(),
                movement = reader.ReadVector3(),
                isJumpRequested = reader.ReadBool(),
                isRollRequested = reader.ReadBool(),
                isAttackRequested = reader.ReadBool(),
                isSprinting = reader.ReadBool()
            };
            return input;
        }

        private void WriteInput(NetworkWriter writer,PlayerInput input)
        {
            writer.Write(input.frame);
            writer.Write(input.playerId);
            writer.Write(input.movement);
            writer.Write(input.isJumpRequested);
            writer.Write(input.isRollRequested);
            writer.Write(input.isAttackRequested);
            writer.Write(input.isSprinting);
        }

        private void FixedUpdate()
        {
            if (isServer)
            {
                ProcessFrame();
            }
        }

        // 服务器处理帧
        private void ProcessFrame()
        {
            if (_frameInputs.TryGetValue(_currentFrame, out var inputs))
            {
                // 广播确认帧
                RpcExecuteFrame(_currentFrame, inputs.ToArray());
                _currentFrame++;
            }
        }

        // 客户端发送输入
        [Command]
        public void CmdSendInput(PlayerInput input)
        {
            if (!_frameInputs.ContainsKey(input.frame))
            {
                _frameInputs[input.frame] = new List<PlayerInput>();
            }
            _frameInputs[input.frame].Add(input);
        }

        // 在所有客户端执行帧
        [ClientRpc]
        private void RpcExecuteFrame(uint frame, PlayerInput[] inputs)
        {
            foreach (var input in inputs)
            {
                var player = NetworkClient.spawned[input.playerId];
                player.GetComponent<PlayerControlClient>().ExecuteInput(input);
            }
        }
    }
    
    [Serializable]
    // 玩家输入结构
    public struct PlayerInput : NetworkMessage
    {
        public uint frame;
        public uint playerId;
        public Vector3 movement;
        public bool isJumpRequested;
        public bool isRollRequested;
        public bool isAttackRequested;
        public bool isSprinting;
    }

}