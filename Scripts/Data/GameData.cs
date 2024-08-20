using UnityEngine;

namespace Data
{
    public class GameData
    {
    }
    
    public class PlayerBaseData : GameData
    {
        public int UID { get; set; }
        public string Name { get; set; }
        public int ConnectId { get; set; }
    }

    public class PlayerAccountData : PlayerBaseData
    {
        public int Level { get; set; }
        public int Experience { get; set; }
    }

    public class PlayerInGameData : PlayerBaseData
    {
        //[Ignore]
        public Vector3 Position { get; set; }
        //[Ignore]
        public Quaternion Rotation { get; set; }
        //[Ignore]
        public string CurrentAnimation { get; set; }
        //[Ignore]
        public bool IsMoving { get; set; }
        //[Ignore]
        public bool IsJumping { get; set; }
    }
}