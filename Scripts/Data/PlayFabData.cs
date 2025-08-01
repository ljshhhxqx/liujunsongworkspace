﻿using System;
using System.Collections.Generic;

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
    }

    [Serializable]
    public struct PlayerReadOnlyData
    {
        public string PlayerId;
        public string Nickname;
        public string Email;
        public int Level;
        public int Score;
        // public int Experience;
        // public int Coins;
        // public int Gems;
        public int ModifyNameCount;
        public string Status; // 玩家状态
        public int Id;
    }

    public enum PlayerStatus
    {
        Offline,
        Online,
        InGame
    }

    [Serializable]
    //获取所有可邀请玩家信息
    public struct InvitablePlayersData 
    {
         public List<PlayerReadOnlyData> Players;
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
        public List<RoomData> AllRooms;
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
        public List<PlayerReadOnlyData> PlayersInfo;
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