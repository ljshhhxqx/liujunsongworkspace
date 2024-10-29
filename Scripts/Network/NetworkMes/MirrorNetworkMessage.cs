using System;
using Mirror;
using Tool.GameEvent;

namespace Network.NetworkMes
{
    // public struct MirrorNetworkMessage : NetworkMessage
    // {
    //     
    // }

    [Serializable]
    public struct PlayerConnectMessage : NetworkMessage
    {
        public string UID;
        public int ConnectionID;
        public string Name;

        public PlayerConnectMessage(string uid, int connectionID, string name)
        {            
            UID = uid;
            ConnectionID = connectionID;
            Name = name;
        }
    }

    // public struct PlayerDisconnectMessage : NetworkMessage
    // {
    //     public int ConnectionID { get; private set; }
    //     
    //     public PlayerDisconnectMessage(int connectionID)
    //     {
    //         ConnectionID = connectionID;
    //     }
    // }
    
    [Serializable]
    public struct CountdownMessage : NetworkMessage
    {
        public float RemainingTime;
        
        public CountdownMessage(float remainingTime)
        {
            RemainingTime = remainingTime;
        }
    }
    
    // public struct GameReadyMessage : NetworkMessage
    // {
    //     public string MapName { get; private set; }
    //     public GameReadyMessage(string mapName)
    //     {
    //         MapName = mapName;
    //     }
    // }

    [Serializable]
    public struct GameStartMessage : NetworkMessage
    {
        public GameInfo GameInfo;
        public GameStartMessage(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }

    [Serializable]
    public struct GameWarmupMessage : NetworkMessage
    {
        public float TimeLeft;

        public GameWarmupMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }
}