using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using Data;
using HotUpdate.Scripts.Network.Client.Player;
using Mirror;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerInGameManager 
    {
        private readonly List<PlayerInGameData> _players = new List<PlayerInGameData>();
        
        public void InitRoomPlayer(RoomData roomData)
        {
            _players.Clear();
            foreach (var player in roomData.PlayersInfo)
            {
                _players.Add(new PlayerInGameData
                {
                    Player = player,
                });
            }
        }

        public void InitPlayerProperty(PlayerPropertyComponent playerProperty)
        {
            var player = GetPlayer(playerProperty.PlayerId);
            if (player != null)
            {
                player.PlayerProperty = playerProperty;
            }
            throw new System.Exception($"Player not found - {playerProperty.PlayerId}");
        }

        public List<PlayerInGameData> GetPlayers()
        {
            return _players;
        }
        
        public void AddPlayer(PlayerInGameData player)
        {
            _players.Add(player);
        }
        
        public void RemovePlayer(int connectionId)
        {
            _players.RemoveAll(x => x.ConnectionId == connectionId);
        }
        
        public void ClearPlayers()
        {
            _players.Clear();
        }
        
        public PlayerInGameData GetPlayer(string playerId)
        {
            foreach (var player in _players)
            {
                if (player.Player.PlayerId == playerId)
                {
                    return player;
                }
            }
            return null;
        }   
    }

    public class PlayerInGameData
    {
        public int ConnectionId { get; set; }
        public PlayerReadOnlyData Player { get; set; }
        public PlayerPropertyComponent PlayerProperty { get; set; }
    }
}