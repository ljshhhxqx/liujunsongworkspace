using System;
using System.Collections.Generic;
using Data;
using HotUpdate.Scripts.Collector;
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
        private readonly SyncDictionary<int, PlayerInGameData> _playerInGameData = new SyncDictionary<int, PlayerInGameData>();
        private readonly SyncDictionary<uint, Vector2Int> _playerGrids = new SyncDictionary<uint, Vector2Int>();
        private readonly SyncDictionary<Vector2Int, HashSet<uint>> _gridPlayers = new SyncDictionary<Vector2Int, HashSet<uint>>();
        private readonly CompositeDisposable _disposables = new CompositeDisposable();
        
        private MapBoundDefiner _mapBoundDefiner;
        
        [SyncVar(hook = nameof(OnIsGameStartedChanged))]
        public bool isGameStarted;

        [Inject]
        private void Init()
        {
            RegisterReaderWriter();
            Observable.EveryUpdate()
                .Where(_ => isGameStarted && isServer)
                .Subscribe(_ => UpdateAllPlayerGrids())
                .AddTo(_disposables);
        }
        
        private void UpdateAllPlayerGrids()
        {
            foreach (var uid in _playerNetIds.Values)
            {
                var identity = NetworkServer.spawned[uid];
                if (!identity) continue;
            
                var position = identity.transform.position;
                UpdatePlayerGridInternal(uid, position);
            }
        }
        private void UpdatePlayerGridInternal(uint id, Vector3 position)
        {
            var newGrid = _mapBoundDefiner.GetGridPosition(position);
        
            if (!_playerGrids.TryGetValue(id, out var currentGrid))
            {
                AddToGrid(newGrid, id);
                return;
            }

            if (currentGrid != newGrid)
            {
                RemoveFromGrid(currentGrid, id);
                AddToGrid(newGrid, id);
            }
        }
        
        private void AddToGrid(Vector2Int grid, uint id)
        {
            if (!_gridPlayers.ContainsKey(grid))
                _gridPlayers[grid] = new HashSet<uint>();
        
            _gridPlayers[grid].Add(id);
            _playerGrids[id] = grid;
        }

        private void RemoveFromGrid(Vector2Int grid, uint id)
        {
            if (_gridPlayers.TryGetValue(grid, out var players))
            {
                players.Remove(id);
                if (players.Count == 0)
                    _gridPlayers.Remove(grid);
            }
            _playerGrids.Remove(id);
        }

        // 获取周围Grid中的玩家
        public HashSet<uint> GetPlayersInGrids(IEnumerable<Vector2Int> grids)
        {
            HashSet<uint> result = new HashSet<uint>();
            foreach (var grid in grids)
            {
                if (_gridPlayers.TryGetValue(grid, out var players))
                {
                    result.UnionWith(players);
                }
            }
            return result;
        }
        
        private void OnIsGameStartedChanged(bool oldIsGameStarted, bool newIsGameStarted)
        {
            _mapBoundDefiner ??= FindObjectOfType<MapBoundDefiner>();
        }

        public void AddPlayer(int connectId, PlayerInGameData playerInGameData)
        {
            _playerIds.Add(connectId, playerInGameData.player.PlayerId);
            _playerNetIds.Add(connectId, playerInGameData.networkIdentity.netId);
            _playerInGameData.Add(connectId, playerInGameData);
        }

        public void RemovePlayer(int connectId)
        {
            _playerIds.Remove(connectId);
            _playerNetIds.Remove(connectId);
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

        private void OnDestroy()
        {
            _disposables.Dispose();
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
}