using Mirror;
using UnityEngine;

namespace Network.NetworkMes
{
    public struct NetMes : NetworkMessage
    {
        public int UID { get; set; }
        public int ConnectionID { get; set; }
        public string Name { get; set; }
        
        public NetMes(int uid, int connectionID, string name)
        {            
            UID = uid;
            ConnectionID = connectionID;
            Name = name;
        }
    }
    
    public struct PlayerMoveMes : NetworkMessage
    {
        public Vector3 PreviousPosition { get; set; }
        public Vector3 Movement { get; set; }
        public float CurrentSpeed { get; set; }
        
        public PlayerMoveMes(Vector3 previousPosition, Vector3 movement, float currentSpeed)
        {
            PreviousPosition = previousPosition;
            Movement = movement;
            CurrentSpeed = currentSpeed;
        }
    }
    
    public struct PlayerRotationMes : NetworkMessage
    {
        public Quaternion Rotation{ get; set; }

        public PlayerRotationMes(Quaternion rotation)
        {
            Rotation = rotation;
        }
    }

    public struct PlayerJumpedMes : NetworkMessage
    {
        public bool isJumping;
    }
}