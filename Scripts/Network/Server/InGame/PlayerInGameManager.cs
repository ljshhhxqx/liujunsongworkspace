using System;
using AOTScripts.Tool.ECS;
using Data;
using HotUpdate.Scripts.Network.Client.Player;
using Mirror;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerInGameManager : ServerNetworkComponent
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

        public PlayerInGameData GetPlayerData(int connectId)
        {
            _playerInGameData.TryGetValue(connectId, out var playerInGameData);
            return playerInGameData;
        }

        public bool IsPlayerGetTargetScore(int targetScore)
        {
            foreach (var player in _playerInGameData)
            {
                var score = player.Value.PlayerProperty.GetProperty(PropertyTypeEnum.Score);
                if (score.Value.Value >= targetScore)
                {
                    return true;
                }
            }
            return false;
        }

        public PlayerInGameData GetPlayer(int playerId)
        {
            foreach (var player in _playerInGameData)
            {
                if (player.Value.PlayerProperty.ConnectionID == playerId)
                {
                    return player.Value;
                }
            }
            return null;
        }   

        public PlayerPropertyComponent GetPlayerPropertyComponent(int connectionId)
        {
            return GetPlayer(connectionId)?.PlayerProperty;
        }

        public PlayerPropertyComponent GetSelfPlayerPropertyComponent()
        {
            return GetPlayerPropertyComponent(NetworkClient.connection.connectionId);
        }

        public void InitPlayerProperty(PlayerPropertyComponent playerProperty)
        {
            var player = GetPlayer(playerProperty.ConnectionID);
            if (player != null)
            {
                player.PlayerProperty = playerProperty;
            }
            throw new Exception($"Player not found - {playerProperty.PlayerId}");
        }
    }

    [Serializable]
    public class PlayerInGameData
    {
        public PlayerReadOnlyData Player { get; set; }
        public PlayerPropertyComponent PlayerProperty { get; set; }
    }
}