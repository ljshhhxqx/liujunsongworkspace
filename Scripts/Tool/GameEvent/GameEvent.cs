using System;
using Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config;
using Mirror;
using UnityEngine;

namespace Tool.GameEvent
{
    /// <summary>
    /// GameEvent适合处理单端通信(比如客户端-客户端)
    /// </summary>
    public abstract class GameEvent
    {
    }

    public class GameResourceLoadedEvent : GameEvent
    {
        
    }
    
    public class GameMessageListeningEvent : GameEvent
    {
        
    }

    public class GameSceneLoadedEvent : GameEvent
    {
        public string SceneName { get; private set; }

        public GameSceneLoadedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    public class GameReadyEvent : GameEvent
    {
        public GameInfo GameInfo { get; private set; }
        public GameReadyEvent(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }

    public class GameSceneLoadingEvent : GameEvent
    {
        public string SceneName { get; private set; }
        public string Progress { get; private set; }

        public GameSceneLoadingEvent(string sceneName, string progress)
        {
            SceneName = sceneName;
            Progress = progress;
        }
    }

    public class GameSceneResourcesLoadedEvent : GameEvent
    {
        public string SceneName { get; private set; }

        public GameSceneResourcesLoadedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    public class PlayerLoginEvent : GameEvent
    {
        public string PlayerId { get; private set; }

        public PlayerLoginEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }
    
    public class PlayerLogoutEvent : GameEvent
    {
        public string PlayerId { get; private set; }

        public PlayerLogoutEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }

    public class PlayerSpawnedEvent : GameEvent
    {
        public Transform Target { get; private set; }

        public PlayerSpawnedEvent(Transform target)
        {
            Target = target;
        }
    }

    public class PlayerJumpEvent : GameEvent
    {
    }
    
    public class PlayerSpeedChangeEvent : GameEvent
    {
        public float Speed { get; private set; }

        public PlayerSpeedChangeEvent(float speed)
        {
            Speed = speed;
        }
    }

    public class PlayerVerticalSpeedChangeEvent : GameEvent
    {
        public float VerticalSpeed { get; private set; }

        public PlayerVerticalSpeedChangeEvent(float verticalSpeed)
        {
            VerticalSpeed = verticalSpeed;
        }
    }

    public class GameInteractableEffect : GameEvent
    {
        public GameObject Picker { get; private set; }
        public IPickable CollectObject { get; private set; }
        public bool IsEnter { get; private set; }
        
        public GameInteractableEffect(GameObject picker, IPickable collectObject, bool isEnter)
        {
            Picker = picker;
            CollectObject = collectObject;
            IsEnter = isEnter;
        }
    }
    
    [Serializable]
    public struct GameInfo
    {
        public string SceneName;
        public GameMode GameMode;
        public int GameScore;
        public int GameTime;
        public int PlayerCount;
    }

    public static class GameEventExtensions
    {
        public static void RegisterGameEventWriteRead()
        {
            Reader<GameInfo>.read = ReadWeatherInfo;
            Writer<GameInfo>.write = WriteWeatherInfo;
        }
        private static GameInfo ReadWeatherInfo(NetworkReader reader)
        {
            return new GameInfo
            {
                SceneName = reader.ReadString(),
                GameMode = (GameMode)reader.ReadByte(),
                GameScore = reader.ReadInt(),
                GameTime = reader.ReadInt(),
                PlayerCount = reader.ReadInt()
            };
        }
        
        public static void WriteWeatherInfo(this NetworkWriter writer, GameInfo gameInfo)
        {
            writer.WriteString(gameInfo.SceneName);
            writer.WriteByte((byte)gameInfo.GameMode);
            writer.WriteInt(gameInfo.GameScore);
            writer.WriteInt(gameInfo.GameTime);
            writer.WriteInt(gameInfo.PlayerCount);
        }
    }
}