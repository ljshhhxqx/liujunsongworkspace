using Mirror;
using UnityEngine;

namespace Network.NetworkMes
{
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

    public struct PlayerDisconnectMessage : NetworkMessage
    {
        public int ConnectionID { get; private set; }
        
        public PlayerDisconnectMessage(int connectionID)
        {
            ConnectionID = connectionID;
        }
    }
}