using System.Collections.Generic;
using Data;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerDataManager 
    {
        private readonly List<PlayerInitData> _players = new List<PlayerInitData>();
        public RoomData CurrentRoomData { get; private set; }

        public void TestInitRoomPlayer()
        {
            InitRoomPlayer(new RoomData
            {
                RoomId = "1",
                CreatorName = "Creator1",
                CreatorId =      "1",
                PlayersInfo = new List<PlayerReadOnlyData>
                {
                    new PlayerReadOnlyData
                    {
                        PlayerId = "Creator1", 
                        Email = "Email1",
                        Level = 1,
                        Score = 0,
                        Status = PlayerStatus.InGame.ToString(),
                        Nickname = "Player1",
                    },
                },
                RoomStatus = 1,
                RoomCustomInfo = new RoomCustomInfo()
                {
                    RoomName = "Room1",
                    RoomType = 2,
                    MaxPlayers = 4,
                    RoomPassword = null,
                    MapType = 0,
                    GameMode = 0,
                    GameTime = 180,
                    GameScore = 0,
                }
                // PlayersInfo = new List<PlayerInfo>
                // {
                //     new PlayerInfo
                //     {
                //         PlayerId = "1", 
                //         PlayerName = "Player1",
                //         PlayerIcon = "Player1Icon",
                //         PlayerPosition = new Vector3(0, 0, 0),
                //         PlayerRotation = new Quaternion(0, 0, 0, 0),
                //     },
                // }
            });
        }

        public void InitRoomPlayer(RoomData roomData)
        {
            CurrentRoomData = roomData;
            _players.Clear();
            foreach (var player in roomData.PlayersInfo)
            {
                _players.Add(new PlayerInitData
                {
                    player = player,
                });
            }
        }
        
        public List<PlayerInitData> GetPlayers()
        {
            return _players;
        }
        
        public PlayerInitData GetPlayer(int connectionId)
        {
            foreach (var player in _players)
            {
                if (player.connectionId == connectionId)
                {
                    return player;
                }
            }
            return null;
        }
        
        public void RegisterPlayer(PlayerInitData playerInitData)
        {
            _players.Add(playerInitData);
        }
        
        public void UnregisterPlayer(int connectionId)
        {
            var player = GetPlayer(connectionId);
            if (player != null)
            {
                _players.Remove(player);
            }
        }
        
        public void UpdatePlayerConnectionId(string playerId, int connectionId)
        {
            var player = _players.Find(p => p.player.PlayerId == playerId);
            if (player != null)
            {
                player.connectionId = connectionId;
            }
        }

        // public void InitPlayerProperty(PlayerPropertyComponent playerProperty)
        // {
        //     var player = GetPlayer(playerProperty.PlayerId);
        //     if (player != null)
        //     {
        //         player.PlayerProperty = playerProperty;
        //     }
        //     throw new Exception($"Player not found - {playerProperty.PlayerId}");
        // }

        // public List<PlayerInGameData> GetPlayers()
        // {
        //     return _players;
        // }
        
        // public PlayerInGameData GetPlayer(string playerId)
        // {
        //     foreach (var player in _players)
        //     {
        //         if (player.Player.PlayerId == playerId)
        //         {
        //             return player;
        //         }
        //     } 
        //     return null;
        // }   
        
        // public GameObject GetPlayerGameObject(string playerId)
        // {
        //     var player = GetPlayer(playerId);
        //     return player?.PlayerProperty.gameObject;
        // }
    }

    public class PlayerInitData
    {
        public int connectionId;
        public PlayerReadOnlyData player;
    }
}