using System.Collections.Generic;
using AOTScripts.Data;
using Data;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Network.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerDataManager 
    {
        private readonly List<PlayerInitData> _players = new List<PlayerInitData>();
        private readonly List<PlayerInGameInfo> _savedPlayers = new List<PlayerInGameInfo>();
        public RoomData CurrentRoomData { get; private set; }
        public MainGameInfo MainGameInfo { get; private set; }

        public void InitGamePlayer(MainGameInfo mainGameInfo)
        {
            MainGameInfo = mainGameInfo;
            _savedPlayers.Clear();
            foreach (var player in MainGameInfo.playersInfo)
            {
                _savedPlayers.Add(new PlayerInGameInfo
                {
                    player = player
                });
                PlayFabData.PlayerList.Add(player);
            }
        }

        public void InitRoomPlayer(RoomData roomData)
        {
            CurrentRoomData = roomData;
            _players.Clear();
            foreach (var str in roomData.PlayersInfo)
            {
                var player = str;
                Debug.Log($"InitRoomPlayer: {player.PlayerId}-{player.Nickname}");
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
            if (player.player.PlayerId != null)
            {
                _players.Remove(player);
            }
        }
        
        public void UpdatePlayerConnectionId(string playerId, int connectionId)
        {
            for (int i = 0; i < _players.Count; i++)
            {
                if (_players[i].player.PlayerId == playerId)
                {
                    _players[i].connectionId = connectionId;
                    return;
                }
            }
            Debug.LogError($"Player not found - {playerId}");
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

    public class PlayerInGameInfo
    {
        public int connectionId;
        public GamePlayerInfo player;
    }
}