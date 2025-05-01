using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Tool.Static;
using Mirror;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
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
        private readonly SyncDictionary<uint, Vector3> _playerPositions = new SyncDictionary<uint, Vector3>();
        private readonly SyncDictionary<uint, Vector3> _playerBornPoints = new SyncDictionary<uint, Vector3>();
        private readonly SyncDictionary<int, UnionData> _unionData = new SyncDictionary<int, UnionData>();
        private readonly SyncDictionary<uint, float> _playerDeathCountdowns = new SyncDictionary<uint, float>();
        private readonly SyncDictionary<uint, int> _playerUnionIds = new SyncDictionary<uint, int>();
        private readonly Dictionary<int, IColliderConfig> _playerPhysicsData = new Dictionary<int, IColliderConfig>();
        private readonly SyncGridDictionary _gridPlayers = new SyncGridDictionary();
        private readonly Dictionary<uint, Action<uint>> _playerBornCallbacks = new Dictionary<uint, Action<uint>>();
        private CancellationTokenSource _updateGridsTokenSource;
        private GameSyncManager _gameSyncManager;
        private MapBoundDefiner _mapBoundDefiner;
        private GameConfigData _gameConfigData;
        
        [SyncVar(hook = nameof(OnIsGameStartedChanged))]
        public bool isGameStarted;

        [Inject]
        private void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            RegisterReaderWriter();
            _gameSyncManager = gameSyncManager;
            _gameConfigData = configProvider.GetConfig<JsonDataConfig>().GameConfig;
            _updateGridsTokenSource = new CancellationTokenSource();
            if (isServer)
            {
                UpdateAllPlayerGrids(_updateGridsTokenSource.Token).Forget();
            }
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
                    var deathCountdown = _playerDeathCountdowns.GetValueOrDefault(uid);
                    if (deathCountdown <= 0)
                    {
                        _playerDeathCountdowns.Remove(uid);
                        _playerBornCallbacks[uid]?.Invoke(uid);
                        _playerBornCallbacks.Remove(uid);
                    }
                    deathCountdown -= _gameSyncManager.TickRate;
                    _playerDeathCountdowns[uid] = deathCountdown;
                    _playerPositions[uid] = position;
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
            _playerPositions.Add(playerInGameData.networkIdentity.netId, playerInGameData.networkIdentity.transform.position);
            _playerBornPoints.Add(playerInGameData.networkIdentity.netId, playerInGameData.networkIdentity.transform.position);
        }

        public void RemovePlayer(int connectId)
        {
            _playerIds.Remove(connectId);
            _playerNetIds.Remove(connectId);
            _playerIdsByNetId.Remove(_playerNetIds.GetValueOrDefault(connectId));
            _playerInGameData.Remove(connectId);
            _playerPhysicsData.Remove(connectId);
            _playerGrids.Remove(_playerNetIds.GetValueOrDefault(connectId));
            _playerBornPoints.Remove(_playerNetIds.GetValueOrDefault(connectId));
            _playerPositions.Remove(_playerNetIds.GetValueOrDefault(connectId));
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

        #region Union

        private int _currentUnionId;
        
        [Server]
        public void RandomUnion()
        {
            var allPlayers = GetAllPlayers();
            var chunkedPlayers = allPlayers.Chunk(_gameConfigData.minUnionPlayerCount);
            foreach (var chunkedPlayer in chunkedPlayers)
            {
                var playerIds = chunkedPlayer as uint[] ?? chunkedPlayer.ToArray();
                var union = new UnionData
                {
                    unionId = ++_currentUnionId,
                    PlayerIds = new HashSet<uint>(playerIds)
                };
                _unionData.Add(union.unionId, union);
                foreach (var playerId in playerIds)
                {
                    _playerUnionIds.TryAdd(playerId, union.unionId);
                }
            }
        }
        
        public HashSet<uint> GetPlayerWithNoUnion()
        {
            var noUnionPlayers = new HashSet<uint>();
            foreach (var player in _playerNetIds.Keys)
            {
                var playerNetId = _playerNetIds[player];
                if (!_playerUnionIds.ContainsKey(playerNetId))
                {
                    noUnionPlayers.Add(playerNetId);
                }
            }
            return noUnionPlayers;
        }

        public bool IsPlayerInUnion(uint playerId)
        {
            foreach (var union in _unionData.Values)
            {
                if (union.PlayerIds.Contains(playerId))
                {
                    return true;
                }
            }
            
            return false;
        }

        public bool TryGetOtherPlayersInUnion(uint playerId, out HashSet<uint> unionPlayerIds)
        {
            var isInUnion = IsPlayerInUnion(playerId);
            if (isInUnion)
            {
                var union = _unionData.Values.First(u => u.PlayerIds.Contains(playerId));
                unionPlayerIds = new HashSet<uint>(union.PlayerIds);
                unionPlayerIds.Remove(playerId);
                return true;
            }
            
            unionPlayerIds = new HashSet<uint>();
            return false;
        }

        public bool IsOtherPlayerAlly(uint playerId, uint otherPlayerId)
        {
            var isPlayerInUnion = IsPlayerInUnion(playerId);
            if (!isPlayerInUnion)
            {
                return false;
            }

            return TryGetOtherPlayersInUnion(playerId, out var unionPlayerIds) && unionPlayerIds.Contains(otherPlayerId);
        }

        #endregion

        #region Base

        public bool IsPlayerInBase(uint playerNetId)
        {
            var playerPosition = _playerPositions.GetValueOrDefault(playerNetId);
            var basePosition = _playerBornPoints.GetValueOrDefault(playerNetId);
            var unionId = _playerUnionIds.GetValueOrDefault(playerNetId);
            if (unionId != 0)
            {
                var union = _unionData.GetValueOrDefault(unionId);
                foreach (var playerId in union.PlayerIds)
                {
                    if (playerId == playerNetId) continue;
                    var unionPlayerPosition = _playerBornPoints.GetValueOrDefault(playerId);
                    if (_gameConfigData.IsWithinBase(unionPlayerPosition, playerPosition))
                    {
                        return true;
                    }
                }
            }
            return _gameConfigData.IsWithinBase(basePosition, playerPosition);
        }

        public bool TryPlayerRecoverHpInBase(uint playerNetId, Action<uint> callback)
        {
            if (!IsPlayerInBase(playerNetId)) 
                return false;
            callback?.Invoke(playerNetId);
            return true;
        }

        [Server]
        public bool TryAddDeathPlayer(uint playerNetId, float countdown, Action<uint> playerDeathCallback, Action<uint> playerBornCallback)
        {
            if (!_playerDeathCountdowns.TryAdd(playerNetId, countdown))
            {
                return false;
            }
            playerDeathCallback?.Invoke(playerNetId);
            _playerBornCallbacks.Add(playerNetId, playerBornCallback);
            return true;
        }

        public bool CanUseShop(uint playerNetId)
        {
            return IsPlayerInBase(playerNetId) || _playerDeathCountdowns.ContainsKey(playerNetId);
        }

        #endregion
    }

    [Serializable]
    public class PlayerInGameData
    {
        public PlayerReadOnlyData player;
        public NetworkIdentity networkIdentity;
    }

    [Serializable]
    public struct UnionData
    {
        public HashSet<uint> PlayerIds;
        public int unionId;
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