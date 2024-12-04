using HotUpdate.Scripts.Collector;
using Tool.GameEvent;
using UnityEngine;

namespace Tool.Message
{
    //网络协议基类
    public abstract class Message
    {
        public int UID { get; set; }
    }
    
    public class PlayerMovedMessage : Message
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
    
    public class PlayerGravityEffectMessage : Message
    {
        public float VerticalSpeed { get; set; }

        public PlayerGravityEffectMessage(float verticalSpeed)
        {
            VerticalSpeed = verticalSpeed;
        }
    }
    
    public class PlayerRotatedMessage : Message
    {
        public Quaternion Quaternion { get;set; }

        public PlayerRotatedMessage(Quaternion quaternion)
        {
            Quaternion = quaternion;
        }
    }
    
    public class PlayerInputMessage : Message
    {
        public bool IsRunning { get;set; }
        public bool IsJumping { get;set; }
        public bool IsRushing { get;set; }

        public PlayerInputMessage(bool isRunning)
        {
            IsRunning = isRunning;
        }
    }

    public class PlayerTouchedCollectMessage : Message
    {
        public int CollectID { get; set; }
        public CollectType CollectType { get; set; }

        public PlayerTouchedCollectMessage(int collectID, CollectType collectType)
        {
            CollectID = collectID;
            CollectType = collectType;
        }
    }
    

    public class PickerPickUpMessage : Message
    {
        public uint PickerId { get; set; }
        public int ItemId { get; set; }
        
        public PickerPickUpMessage(uint pickerId, int itemId)
        {
            PickerId = pickerId;
            ItemId = itemId;
        }
    }

    public class PlayerCollectChestMessage : Message
    {
        public int CollectID { get; set; }
        public CollectType CollectType { get; set; }

        public PlayerCollectChestMessage(int collectID, CollectType collectType)
        {
            CollectID = collectID;
            CollectType = collectType;
        }
    }
    
    public class GameStartMessage : Message
    {
        public GameInfo GameInfo;
        public GameStartMessage(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }
    
    public class GameWarmupMessage : Message
    {
        public float TimeLeft;

        public GameWarmupMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }
    
    public class CountdownMessage : Message
    {
        public float RemainingTime;
        
        public CountdownMessage(float remainingTime)
        {
            RemainingTime = remainingTime;
        }
    }
    
    public class PickerPickUpChestMessage : Message
    {
        public uint PickerId { get; set; }
        public uint ChestNetId { get; set; }

        public PickerPickUpChestMessage(uint pickerId, uint chestNetId)
        {
            PickerId = pickerId;
            ChestNetId = chestNetId;
        }
    }
}