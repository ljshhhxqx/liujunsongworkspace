using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Data.State;
using AOTScripts.Tool;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.UI;
using Mirror;
using UI.UIBase;
using UnityEngine;
using PlayerPredictablePropertyState = HotUpdate.Scripts.Network.State.PlayerPredictablePropertyState;

namespace HotUpdate.Scripts.Tool.GameEvent
{
    /// <summary>
    /// GameEvent适合处理单端通信(比如客户端-客户端)
    /// </summary>
    public interface IGameEvent
    {
    }

    public struct GameResourceLoadedEvent : IGameEvent
    {
        
    }
    
    public struct GameMessageListeningEvent : IGameEvent
    {
        
    }

    public struct GameFunctionUIShowEvent : IGameEvent
    {
        public UIType UIType { get;  private set; }
        
        public GameFunctionUIShowEvent(UIType uiType)
        {
            UIType = uiType;
        }
    }

    public struct GameSceneLoadedEvent : IGameEvent
    {
        public string SceneName { get; private set; }

        public GameSceneLoadedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    public struct PlayerTracesEvent : IGameEvent
    {
        public uint PlayerId { get; private set; }
        public Vector3 Position { get; private set; }
        public MinimapTargetType MinimapTargetType { get; private set; }

        public PlayerTracesEvent(uint playerId, Vector3 position, MinimapTargetType minimapTargetType)
        {
            PlayerId = playerId;
            Position = position;
            MinimapTargetType = minimapTargetType;
        }
        
    }

    public struct TargetShowEvent : IGameEvent
    {
        public Transform Target { get; private set; }
        public Transform Player { get; private set; }
        public uint TargetId { get; private set; }

        public TargetShowEvent(Transform target, Transform player, uint targetId)
        {
            Target = target;
            Player = player;
            TargetId = targetId;
        }
    }

    public struct GameReadyEvent : IGameEvent
    {
        public GameInfo GameInfo { get; private set; }
        public GameReadyEvent(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }
    
    public struct GameStartEvent : IGameEvent
    {
        // public GameInfo GameInfo { get; private set; }
        // public GameStartEvent(GameInfo gameInfo)
        // {
        //     GameInfo = gameInfo;
        // }
    }

    public struct GameSceneLoadingEvent : IGameEvent
    {
        public string SceneName { get; private set; }
        public string Progress { get; private set; }

        public GameSceneLoadingEvent(string sceneName, string progress)
        {
            SceneName = sceneName;
            Progress = progress;
        }
    }
    
    public struct AddBuffToAllPlayerEvent : IGameEvent
    {
        public int CurrentRound { get; private set; }

        public AddBuffToAllPlayerEvent(int currentRound)
        {
            CurrentRound = currentRound;
        }
    }
    
    public struct AddDeBuffToLowScorePlayerEvent : IGameEvent
    {
        public int CurrentRound { get; private set; }

        public AddDeBuffToLowScorePlayerEvent(int currentRound)
        {
            CurrentRound = currentRound;
        }
    }

    public struct AllPlayerGetSpeedEvent : IGameEvent
    {
        
    }

    public struct PlayerConnectEvent : IGameEvent
    {
        public int ConnectionId { get; private set; }
        public uint PlayerNetId { get; private set; }

        public PlayerConnectEvent(int connectionId, uint playerNetId)
        {
            ConnectionId = connectionId;
            PlayerNetId = playerNetId;
        }
    }
    
    public struct PlayerDisconnectEvent : IGameEvent
    {
        public int ConnectionId { get; private set; }
        public uint PlayerNetId { get; private set; }

        public PlayerDisconnectEvent(int connectionId, uint playerNetId)
        {
            ConnectionId = connectionId;
            PlayerNetId = playerNetId;
        
        }
    }

    public struct GameSceneResourcesLoadedEvent : IGameEvent
    {
        public string SceneName { get; private set; }

        public GameSceneResourcesLoadedEvent(string sceneName)
        {
            SceneName = sceneName;
        }
    }

    public struct PlayerLoginEvent : IGameEvent
    {
        public string PlayerId { get; private set; }

        public PlayerLoginEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }
    
    public struct PlayerLogoutEvent : IGameEvent
    {
        public string PlayerId { get; private set; }

        public PlayerLogoutEvent(string playerId)
        {
            PlayerId = playerId;
        }
    }
    
    public struct PlayerListenMessageEvent : IGameEvent
    {
    }
    
    public struct PlayerUnListenMessageEvent : IGameEvent
    {
    }

    public struct ItemSpawnedEvent : IGameEvent
    {
        public uint ItemId { get; private set; }
        public Vector3 Position { get; private set; }
        public SceneItemInfo SceneItemInfo { get; private set; }
        
        public ItemSpawnedEvent(uint itemId, Vector3 position, SceneItemInfo sceneItemInfo)
        {
            ItemId = itemId;
            Position = position;
            SceneItemInfo = sceneItemInfo;
        }
    }

    public struct FollowTargetTextEvent : IGameEvent
    {
        public Vector3 Position { get; private set; }
        public string Text { get; private set; }

        public FollowTargetTextEvent(Vector3 position, string text)
        {
            Position = position;
            Text = text;
        }
    }

    public struct PlayerSpawnedEvent : IGameEvent
    {
        public Transform Target { get; private set; }

        public PlayerSpawnedEvent(Transform target)
        {
            Target = target;
        }
    }

    public struct PlayerSkillItemEvent : IGameEvent
    {
        public uint PlayerId { get; private set; }
        public PlayerPredictablePropertyState PlayerState { get; private set; }
        public SkillHitExtraEffectData SkillHitExtraEffectData { get; private set; }
        public uint DefenderId { get; private set; }
        
        public PlayerSkillItemEvent(uint playerId, PlayerPredictablePropertyState playerState, SkillHitExtraEffectData skillHitExtraEffectData, uint defenderId)
        {
            PlayerId = playerId;
            PlayerState = playerState;
            SkillHitExtraEffectData = skillHitExtraEffectData;
            DefenderId = defenderId;
        }
    }

    public struct PlayerAttackItemEvent : IGameEvent
    {
        public uint AttackerId { get; private set; }
        public uint[] DefenderIds { get; private set; }
        public PlayerPredictablePropertyState AttackerState { get; private set; }

        public PlayerAttackItemEvent(uint attackerId, uint[] defenderIds, PlayerPredictablePropertyState attackerState)
        {
            AttackerId = attackerId;
            DefenderIds = defenderIds;
            AttackerState = attackerState;
        }
    }

    public struct GameInteractableEffect : IGameEvent
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
        public MapType SceneName;
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
                SceneName = (MapType)reader.ReadInt(),
                GameMode = (GameMode)reader.ReadByte(),
                GameScore = reader.ReadInt(),
                GameTime = reader.ReadInt(),
                PlayerCount = reader.ReadInt()
            };
        }
        
        public static void WriteWeatherInfo(this NetworkWriter writer, GameInfo gameInfo)
        {
            writer.WriteInt((int)gameInfo.SceneName);
            writer.WriteByte((byte)gameInfo.GameMode);
            writer.WriteInt(gameInfo.GameScore);
            writer.WriteInt(gameInfo.GameTime);
            writer.WriteInt(gameInfo.PlayerCount);
        }
    }
}