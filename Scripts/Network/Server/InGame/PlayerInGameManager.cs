using System;
using System.Collections.Generic;
using Data;
using HotUpdate.Scripts.Network.Client.Player;
using Mirror;
using Network.Data;
using Network.NetworkMes;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerInGameManager : NetworkBehaviour
    {
        private readonly SyncDictionary<int, string> _playerIds = new SyncDictionary<int, string>();
        private readonly SyncDictionary<int, PlayerInGameData> _playerInGameData = new SyncDictionary<int, PlayerInGameData>();
        
        public void AddPlayer(int connectId, PlayerInGameData playerInGameData)
        {
            _playerInGameData.Add(connectId, playerInGameData);
        }

        public void RemovePlayer(int connectId)
        {
            _playerInGameData.Remove(connectId);
        }

        public bool IsPlayerGetTargetScore(int targetScore)
        {
            // foreach (var player in _playerInGameData)
            // {
            //     var score = player.Value.PlayerProperty.GetProperty(PropertyTypeEnum.Score);
            //     if (score.Value.Value >= targetScore)
            //     {
            //         return true;
            //     }
            // }
            return false;
        }

        public PlayerInGameData GetPlayer(int playerId)
        {
            return _playerInGameData.GetValueOrDefault(playerId);
        }   
        
        public T GetPlayerComponent<T>(int playerId) where T : Component
        {
            return GetPlayer(playerId)?.networkIdentity.GetComponent<T>();
        }

        // public PlayerPropertyComponent GetPlayerPropertyComponent(int connectionId)
        // {
        //     return GetPlayer(connectionId)?.PlayerProperty;
        // }
        
        // public PlayerPropertyComponent GetPlayerPropertyComponent(uint networkId)
        // {
        //     foreach (var player in _playerInGameData)
        //     {
        //         if (player.Value.PlayerProperty.netId == networkId)
        //         {
        //             return player.Value.PlayerProperty;
        //         }
        //     }
        //     return null;
        // }

        // public PlayerPropertyComponent GetSelfPlayerPropertyComponent()
        // {
        //     return GetPlayerPropertyComponent(NetworkClient.connection.connectionId);
        // }
    }

    [Serializable]
    public class PlayerInGameData
    {
        public PlayerReadOnlyData player;
        public NetworkIdentity networkIdentity;
    }
}