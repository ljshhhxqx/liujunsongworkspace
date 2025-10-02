using System;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Data
{
    [Serializable]
    public struct PlayerInternalData
    {
        public string PlayerId;
        public string LastLoginTime;
        public int TotalPlayTime; // 以分钟为单位
        public string AccountCreationDate;
        public string CurrentRoomId; // 如果玩家在房间中，存储房间ID
        
        public override string ToString()
        {
            return  $"PlayerId: {PlayerId}, LastLoginTime: {LastLoginTime}, TotalPlayTime: {TotalPlayTime}, AccountCreationDate: {AccountCreationDate}, CurrentRoomId: {CurrentRoomId}";
        }
    }

    [Serializable]
    public struct PlayerReadOnlyData
    {
        public string PlayerId;
        public string Nickname;
        public string Email;
        public int Level;
        public int Score;
        public int Experience;
        public int Coins;
        public int Gems;
        public int ModifyNameCount;
        public string Status; // 玩家状态
        public int Id;
        
        public override string ToString()
        {
            return  $"PlayerId: {PlayerId}, Nickname: {Nickname}, Email: {Email}, Level: {Level}, Score: {Score}, ModifyNameCount: {ModifyNameCount}, Status: {Status}, Gems: {Gems}, Coins: {Coins}, Experience: {Experience} Id: {Id}";
        }
    }
    
    // 好友关系状态
    public enum FriendStatus
    {
        None = 0,       // 无关系
        RequestSent = 1, // 已发出邀请
        RequestReceived = 2,  // 收到邀请
        Friends = 3,    // 已是好友
        Removed = 4,    // 已删除
        Rejected = 5,   // 已拒绝
    }
    
    [Serializable]
    public struct NonFriendOnlinePlayersResult
    {
        public List<PlayerReadOnlyData> players;
        public int count;
        public int totalOnline;
        public string error;
    }
    
    // 好友数据
    [Serializable]
    public struct FriendData
    {
        public int Id;
        public string PlayFabId;
        public string Username;
        public FriendStatus FriendStatus;
        public string LastOnline;
        public PlayerStatus PlayerStatus;
        public string IconUrl;
        public int Level;

        public FriendData(int id, string playFabId, string username, FriendStatus friendStatus, string iconUrl, int level, string lastOnline, PlayerStatus playerStatus = PlayerStatus.Offline)
        {
            Id = id;
            PlayFabId = playFabId;
            Username = username;
            FriendStatus = friendStatus;
            LastOnline = lastOnline;
            PlayerStatus = playerStatus;
            IconUrl = iconUrl;
            Level = level;
        }
    }

    // 好友列表
    [Serializable]
    public struct FriendList
    {
        public List<FriendData> Friends;
    }

    public enum PlayerStatus
    {
        Offline,
        Online,
        InRoom,
        InGame
    }

    [Serializable]
    //可邀请玩家信息(其实是我整个项目核心玩家信息，可能需要做很多额外拓展)
    public struct PlayerInfo 
    {
        public string PlayerId;
        public string AccountId;
        public string Nickname;
        public int Level;
        public bool IsInGame;
    }

    [Serializable]
    //unity本地需要的一些数据
    public struct RoomGlobalInfo 
    {
        public int MaxPlayers;
        public int MinPlayers;
    }

    [Serializable]
    public struct RoomsData 
    {
        public RoomData[] AllRooms;
    }

    [Serializable]
    //房间自定义信息
    public struct RoomCustomInfo
    {
        public int Id;
        public string RoomName;
        // 0: public, 1: local
        public int RoomType;
        public int MaxPlayers;
        // 房间密码，如果为空则不需要密码(在处理邀请时需要判断是否需要密码)
        public string RoomPassword;
        public int MapType;
        // 0:time, 1:score
        public int GameMode;
        public int GameTime;
        public int GameScore;
    }

    public enum PlayerGameStatus
    {
        None,
        Waiting,
        Connecting,
        Connected,
        Gaming,
        End
    }
    public enum PlayerGameDuty
    {
        None,
        Host,
        Server,
        Client
    }

    [Serializable]
    public struct MainGameInfo
    {
        public string roomId;
        public string gameId;
        public string ipAddress;
        public string mapType;
        public int port;
        public GamePlayerInfo[] playersInfo;
    }

    [Serializable]
    public struct GamePlayerInfo
    {
        public string playerId;
        public string playerName;
        public int playerLevel;
        public string playerDuty;
        public string playerStatus;
    }

    [Serializable]
    public struct RoomData
    {
        public int Id;
        // 房间ID，需要服务器生成
        public string RoomId;
        public string CreatorId;
        public string CreatorName;
        public RoomCustomInfo RoomCustomInfo;
        // 0: waiting, 1: gaming
        public int RoomStatus;
        public PlayerReadOnlyData[] PlayersInfo;
        
        public override string ToString()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Id: {Id}");
            sb.AppendLine($"RoomId: {RoomId}");
            sb.AppendLine($"CreatorId: {CreatorId}");
            sb.AppendLine($"CreatorName: {CreatorName}");
            sb.AppendLine($"RoomCustomInfo: {RoomCustomInfo}");
            sb.AppendLine($"RoomStatus: {RoomStatus}");
            foreach (var p in PlayersInfo)
            {
                sb.AppendLine(p.ToString());
            }
            return sb.ToString();
        }
    }
    
    public enum GameMode
    {
        /// <summary>
        /// 按时间结束
        /// </summary>
        Time,
        /// <summary>
        /// 按目标分数结束
        /// </summary>
        Score
    }
}