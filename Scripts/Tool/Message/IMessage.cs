using System.Collections.Generic;
using System.Threading;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config;
using Network.NetworkMes;
using Tool.GameEvent;
using UnityEngine;

namespace Tool.Message
{
    //网络协议基类
    public interface IMessage
    {
        
    }
    
    public struct UniqueMessage
    {
        public IMessage Message;
        public long messageId;
        private int _globalMessageCounter;

        public UniqueMessage(IMessage message)
        {
            _globalMessageCounter = 0;
            Message = message;
            messageId = 0;
            messageId = GenerateUniqueMessageId();
        }

        private long GenerateUniqueMessageId()
        {
            // 结合消息类型和自增ID
            var typeHash = (long)Message.GetHashCode() << 32;
            return typeHash | (long)Interlocked.Increment(ref _globalMessageCounter);
        }

    }
    
    public struct PlayerMovedMessage : IMessage
    {
        public Vector3 PreviousPosition { get;  set; }
        public Vector3 Movement { get;  set; }
        public float VerticalSpeed { get; set; }
        
        public PlayerMovedMessage(Vector3 previousPosition, Vector3 movement, float verticalSpeed)
        {
            PreviousPosition = previousPosition;
            Movement = movement;
            VerticalSpeed = verticalSpeed;
        }
    }
    
    public struct PlayerGravityEffectMessage : IMessage
    {
        public float VerticalSpeed { get; set; }

        public PlayerGravityEffectMessage(float verticalSpeed)
        {
            VerticalSpeed = verticalSpeed;
        }
    }
    
    public struct PlayerRotatedMessage : IMessage
    {
        public Quaternion Quaternion { get;set; }

        public PlayerRotatedMessage(Quaternion quaternion)
        {
            Quaternion = quaternion;
        }
    }
    
    public struct PlayerInputMessage : IMessage
    {
        public PlayerInputInfo PlayerInputInfo;

        public PlayerInputMessage(PlayerInputInfo playerInputInfo)
        {
            PlayerInputInfo = playerInputInfo;
        }
    }

    public struct PlayerFrameUpdateMessage : IMessage
    {
        public List<PlayerInputInfo> PlayerInputInfos;
        public uint Frame;
        
        public PlayerFrameUpdateMessage(uint frame, List<PlayerInputInfo> playerInputInfos)
        {
            Frame = frame;
            PlayerInputInfos = playerInputInfos;
        }
    }

    public struct PlayerAttackMessage : IMessage
    {
        public AttackData PlayerAttackData;
        public uint Frame;

        public PlayerAttackMessage(AttackData playerAttackData, uint frame)
        {
            PlayerAttackData = playerAttackData;
            Frame = frame;
        }
    }

    public struct PlayerDamageResultMessage : IMessage
    {
        public List<DamageResult> DamageResults;
        public uint Frame;

        public PlayerDamageResultMessage(uint frame, List<DamageResult> damageResults)
        {
            Frame = frame;
            DamageResults = damageResults;
        }
    }

    public struct PlayerTouchedCollectMessage : IMessage
    {
        public int CollectID { get; set; }
        public int CollectConfigId { get; set; }

        public PlayerTouchedCollectMessage(int collectID, int collectConfigId)
        {
            CollectID = collectID;
            CollectConfigId = collectConfigId;
        }
    }
    

    public struct PickerPickUpMessage : IMessage
    {
        public uint PickerId { get; set; }
        public uint ItemId { get; set; }
        
        public PickerPickUpMessage(uint pickerId, uint itemId)
        {
            PickerId = pickerId;
            ItemId = itemId;
        }
    }

    public struct PlayerCollectChestMessage : IMessage
    {
        public int CollectID { get; set; }
        public int CollectConfigId { get; set; }

        public PlayerCollectChestMessage(int collectID, int collectConfigId)
        {
            CollectID = collectID;
            CollectConfigId = collectConfigId;
        }
    }
    
    public struct GameStartMessage : IMessage
    {
        public GameInfo GameInfo;
        public GameStartMessage(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }
    
    public struct GameWarmupMessage : IMessage
    {
        public float TimeLeft;

        public GameWarmupMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }
    
    public struct CountdownMessage : IMessage
    {
        public float RemainingTime;
        
        public CountdownMessage(float remainingTime)
        {
            RemainingTime = remainingTime;
        }
    }
    
    public struct PickerPickUpChestMessage : IMessage
    {
        public uint PickerId { get; private set; }
        public uint ChestNetId { get; private set; }

        public PickerPickUpChestMessage(uint pickerId, uint chestNetId)
        {
            PickerId = pickerId;
            ChestNetId = chestNetId;
        }
    }
    
    public struct PlayerInputInfoMessage : IMessage
    {
        public InputData Input;
        public int ConnectionId;
        public PlayerInputInfoMessage( int connectionId, InputData input)
        {
            this.Input = input;
            this.ConnectionId = connectionId;
        }
    }   
    
    public struct PlayerStateMessage : IMessage
    {
        public ServerState State;
        public PlayerStateMessage(ServerState state)
        {
            this.State = state;
        }
    }
}