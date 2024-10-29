using Mirror;
using Tool.GameEvent;
using UnityEngine;

namespace Network.NetworkMes
{
    public struct MirrorNetworkMessage : NetworkMessage
    {
        
    }

    public struct PlayerConnectMessage : NetworkMessage
    {
        public string UID { get; private set; }
        public int ConnectionID { get; private set; }
        public string Name { get; private set; }
        
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
    
    public struct CountdownMessage : NetworkMessage
    {
        public float RemainingTime { get; private set; }
        
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

    public struct GameStartMessage : NetworkMessage
    {
        public GameInfo GameInfo { get; private set; }
        public GameStartMessage(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }

    public struct GameWarmupMessage : NetworkMessage
    {
        public float TimeLeft { get; set; }

        public GameWarmupMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }
}