using System.Collections.Generic;
using Data;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerDataManager 
    {
        private readonly List<PlayerInitData> _players = new List<PlayerInitData>();
        public RoomData CurrentRoomData { get; private set; }

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