using System;
using System.Collections.Generic;
using Data;
using Mirror;
using Model;
using VContainer;

namespace Player
{
    public class PlayerManager
    {
        // 依赖注入
        private readonly PlayersGameModelManager playersGameModelManager;
    
        // 玩家数据字典
        private readonly Dictionary<int, PlayerBaseData> players = new Dictionary<int, PlayerBaseData>();
    
        [Inject]
        private PlayerManager(PlayersGameModelManager playersGameModelManager)
        {
            this.playersGameModelManager = playersGameModelManager;
        }

        // private void Awake()
        // {
        //     DontDestroyOnLoad(gameObject);
        // }

        // 为新玩家分配ID和初始化数据
        public void RegisterPlayer(NetworkConnection conn)
        {
            var playerId = conn.connectionId;
            var playerGameObject = conn.identity ? conn.identity.gameObject : null;
            var playerData = new PlayerBaseData
            {
                UID = playerId,
                Name = GetPlayerName(playerId)
            };
        
            players.Add(playerId, playerData);
            playersGameModelManager.AddPlayer(playerId);
        }

        private static string GetPlayerName(int playerId)
        {
            return $"Player-{playerId}";
        }

        // 根据ID获取玩家数据
        public PlayerBaseData GetPlayerData(int playerId)
        {
            return players.TryGetValue(playerId, out var playerData) ? playerData : null;
        }

        // 玩家离开时的处理
        public void UnregisterPlayer(int playerId)
        {
            players.Remove(playerId);
        }
    }
}
