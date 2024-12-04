using System;
using Mirror;
using Tool.GameEvent;

namespace Network.NetworkMes
{
    public struct MirrorNetworkMessage : NetworkMessage
    {
        
    }

    [Serializable]
    public struct MirrorPlayerConnectMessage : NetworkMessage
    {
        public string UID;
        public int ConnectionID;
        public string Name;

        public MirrorPlayerConnectMessage(string uid, int connectionID, string name)
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
    public struct MirrorCountdownMessage : NetworkMessage
    {
        public float RemainingTime;
        
        public MirrorCountdownMessage(float remainingTime)
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
    public struct MirrorGameStartMessage : NetworkMessage
    {
        public GameInfo GameInfo;
        public MirrorGameStartMessage(GameInfo gameInfo)
        {
            GameInfo = gameInfo;
        }
    }

    [Serializable]
    public struct MirrorGameWarmupMessage : NetworkMessage
    {
        public float TimeLeft;

        public MirrorGameWarmupMessage(float timeLeft)
        {
            TimeLeft = timeLeft;
        }
    }

    [Serializable]
    public struct MirrorPickerPickUpCollectMessage : NetworkMessage
    {
        public uint PickerID;
        public int ItemID;

        public MirrorPickerPickUpCollectMessage(uint pickerID, int itemID)
        {
            PickerID = pickerID;
            ItemID = itemID;
        }
    }

    [Serializable]
    public struct MirrorPickerPickUpChestMessage : NetworkMessage
    {
        public uint PickerID;
        public uint ChestID;

        public MirrorPickerPickUpChestMessage(uint pickerID, uint chestID)
        {
            PickerID = pickerID;
            ChestID = chestID;
        }
    }
}