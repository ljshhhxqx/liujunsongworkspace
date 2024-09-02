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

    public class CollectObjectsEmptyMessage : Message
    {
        public int Round { get; set; }
        public CollectObjectsEmptyMessage(int round)
        {
            Round = round;
        }
    }

    public class GameWarmupMessage : Message
    {
        public float TimeLeft { get; set; }

        public GameWarmupMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }

    public class GameStartMessage : Message
    {
        public string LevelName { get; set; }

        public GameStartMessage(string levelName)
        {
            LevelName = levelName;
        }
    }

    public class GameCountDownMessage : Message
    {
        public float TimeLeft { get; set; }

        public GameCountDownMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }
}