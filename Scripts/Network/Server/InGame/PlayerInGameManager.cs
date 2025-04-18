﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerInGameManager : NetworkBehaviour
    {
        private readonly SyncDictionary<int, string> _playerIds = new SyncDictionary<int, string>();
        private readonly SyncDictionary<int, uint> _playerNetIds = new SyncDictionary<int, uint>();
        private readonly SyncDictionary<uint, int> _playerIdsByNetId = new SyncDictionary<uint, int>();
        private readonly SyncDictionary<int, PlayerInGameData> _playerInGameData = new SyncDictionary<int, PlayerInGameData>();
        private readonly SyncDictionary<uint, Vector2Int> _playerGrids = new SyncDictionary<uint, Vector2Int>();
        private readonly Dictionary<int, IColliderConfig> _playerPhysicsData = new Dictionary<int, IColliderConfig>();
        private readonly SyncGridDictionary _gridPlayers = new SyncGridDictionary();
        private CancellationTokenSource _updateGridsTokenSource;
        private GameSyncManager _gameSyncManager;
        private MapBoundDefiner _mapBoundDefiner;
        
        [SyncVar(hook = nameof(OnIsGameStartedChanged))]
        public bool isGameStarted;

        [Inject]
        private void Init(GameSyncManager gameSyncManager)
        {
            RegisterReaderWriter();
            _gameSyncManager = gameSyncManager;
            _updateGridsTokenSource = new CancellationTokenSource();
            UpdateAllPlayerGrids(_updateGridsTokenSource.Token).Forget();
        }
        
        public IColliderConfig GetPlayerPhysicsData(int playerId)
        {
            return _playerPhysicsData.GetValueOrDefault(playerId);
        }
        
        private async UniTaskVoid UpdateAllPlayerGrids(CancellationToken token)
        {
            while (!token.IsCancellationRequested && isGameStarted && isServer)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(_gameSyncManager.TickRate), cancellationToken: token);
                foreach (var uid in _playerNetIds.Values)
                {
                    var identity = NetworkServer.spawned[uid];
                    if (!identity) continue;
            
                    var position = identity.transform.position;
                    UpdatePlayerGrid(uid, position);
                }
            }
        }
        private void UpdatePlayerGrid(uint id, Vector3 playerPosition)
        {
            var newGrid = _mapBoundDefiner.GetGridPosition(playerPosition);
        
            if (!_playerGrids.TryGetValue(id, out var oldGrid))
            {
                if (oldGrid == newGrid) return;
            
                if (_gridPlayers.TryGetValue(oldGrid, out var oldData))
                {
                    var list = new List<uint>(oldData.playerNetIds);
                    list.Remove(netId);
                    _gridPlayers[oldGrid] = new GridData(list);
                }
            }

            // 添加新Grid记录
            if (!_gridPlayers.ContainsKey(newGrid))
            {
                _gridPlayers[newGrid] = new GridData(Array.Empty<uint>());
            }
        
            var newData = new List<uint>(_gridPlayers[newGrid].playerNetIds);
            if (!newData.Contains(netId))
            {
                newData.Add(netId);
                _gridPlayers[newGrid] = new GridData(newData);
            }
        
            _playerGrids[netId] = newGrid;
        }

        // 获取周围Grid中的玩家
        public HashSet<uint> GetPlayersInGrids(IEnumerable<Vector2Int> grids)
        {
            HashSet<uint> result = new HashSet<uint>();
            foreach (var grid in grids)
            {
                if (_gridPlayers.TryGetValue(grid, out var players))
                {
                    result.UnionWith(players.playerNetIds);
                }
            }
            return result;
        }

        public int[] GetPlayersWithNetIds(uint[] netIds)
        {
            var players = new HashSet<int>();
            foreach (var id in netIds)
            {
                players.Add(GetPlayerId(id));
            }

            return players.ToArray();
        }

        public int GetPlayerId(uint id)
        {
            return _playerIdsByNetId.GetValueOrDefault(id);
        }

        private void OnIsGameStartedChanged(bool oldIsGameStarted, bool newIsGameStarted)
        {
            if (newIsGameStarted)
                _mapBoundDefiner = FindObjectOfType<MapBoundDefiner>();
        }

        public void AddPlayer(int connectId, PlayerInGameData playerInGameData)
        {
            var playerIdentity = playerInGameData.networkIdentity;
            var playerCollider = playerIdentity.GetComponent<Collider>();
            _playerPhysicsData.Add(connectId, GamePhysicsSystem.CreateColliderConfig(playerCollider));
            _playerIds.Add(connectId, playerInGameData.player.PlayerId);
            _playerNetIds.Add(connectId, playerInGameData.networkIdentity.netId);
            _playerInGameData.Add(connectId, playerInGameData);
            _playerIdsByNetId.Add(playerInGameData.networkIdentity.netId, connectId);
        }

        public void RemovePlayer(int connectId)
        {
            _playerIds.Remove(connectId);
            _playerNetIds.Remove(connectId);
            _playerIdsByNetId.Remove(_playerNetIds.GetValueOrDefault(connectId));
            _playerInGameData.Remove(connectId);
        }
        
        public IEnumerable<uint> GetAllPlayers()
        {
            return _playerNetIds.Values;
        }
        
        public uint GetPlayerNetId(int connectId)
        {
            return _playerNetIds.GetValueOrDefault(connectId);
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

        private void RegisterReaderWriter()
        {
            Reader<PlayerReadOnlyData>.read = PlayerReadOnlyDataReader;
            Writer<PlayerReadOnlyData>.write = PlayerReadOnlyDataWriter;
            Reader<PlayerInGameData>.read = PlayerInGameDataReader;
            Writer<PlayerInGameData>.write = PlayerInGameDataWriter;
        }
        
        private void PlayerReadOnlyDataWriter(NetworkWriter writer, PlayerReadOnlyData playerReadOnlyData)
        {
            writer.WriteString(playerReadOnlyData.PlayerId);
            writer.WriteString(playerReadOnlyData.Nickname);
            writer.WriteString(playerReadOnlyData.Email);
            writer.WriteInt(playerReadOnlyData.Level);
            writer.WriteInt(playerReadOnlyData.Score);
            writer.WriteInt(playerReadOnlyData.ModifyNameCount);
            writer.WriteString(playerReadOnlyData.Status);
        }
        
        private PlayerReadOnlyData PlayerReadOnlyDataReader(NetworkReader playerReadOnlyData)
        {
            return new PlayerReadOnlyData
            {
                PlayerId = playerReadOnlyData.ReadString(),
                Nickname = playerReadOnlyData.ReadString(),
                Email = playerReadOnlyData.ReadString(),
                Level = playerReadOnlyData.ReadInt(),
                Score = playerReadOnlyData.ReadInt(),
                ModifyNameCount = playerReadOnlyData.ReadInt(),
                Status = playerReadOnlyData.ReadString()
            };
        }
        
        private PlayerInGameData PlayerInGameDataReader(NetworkReader reader)
        {
            return new PlayerInGameData
            {
                player = PlayerReadOnlyDataReader(reader),
                networkIdentity = reader.ReadNetworkIdentity()
            };
        }

        private void PlayerInGameDataWriter(NetworkWriter writer, PlayerInGameData playerInGameData)
        {
            writer.Write(playerInGameData.player);
            writer.Write(playerInGameData.networkIdentity);
        }   
    }

    [Serializable]
    public class PlayerInGameData
    {
        public PlayerReadOnlyData player;
        public NetworkIdentity networkIdentity;
    }
    
    [Serializable]
    public struct GridData
    {
        public uint[] playerNetIds;
    
        public GridData(IEnumerable<uint> ids)
        {
            playerNetIds = ids.ToArray();
        }
    }

    public class SyncGridDictionary : SyncDictionary<Vector2Int, GridData> {}
}