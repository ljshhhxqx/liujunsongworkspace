using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.GameBase;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Tool.Static;
using Mirror;
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
        private readonly SyncDictionary<uint, Vector3> _playerPositions = new SyncDictionary<uint, Vector3>();
        private readonly SyncDictionary<int, UnionData> _unionData = new SyncDictionary<int, UnionData>();
        private readonly SyncDictionary<uint, float> _playerDeathCountdowns = new SyncDictionary<uint, float>();
        private readonly SyncDictionary<uint, int> _playerUnionIds = new SyncDictionary<uint, int>();
        private readonly SyncGridDictionary _gridPlayers = new SyncGridDictionary();
        private readonly Dictionary<uint, Action<uint>> _playerBornCallbacks = new Dictionary<uint, Action<uint>>();
        private CancellationTokenSource _updateGridsTokenSource;
        private GameSyncManager _gameSyncManager;
        private MapBoundDefiner _mapBoundDefiner;
        private GameConfigData _gameConfigData;
        
        private PlayerBase _playerBasePrefab;
        private SyncDictionary<Vector3, uint> _playerSpawnPoints = new SyncDictionary<Vector3, uint>();
        private Dictionary<int, PlayerBase> _playerBases = new Dictionary<int, PlayerBase>();
        private IColliderConfig _playerBaseColliderData;
        private IColliderConfig _playerPhysicsData;
        private uint _baseId;
        
        [SyncVar(hook = nameof(OnIsGameStartedChanged))]
        public bool isGameStarted;

        [Inject]
        private void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            RegisterReaderWriter();
            _gameSyncManager = gameSyncManager;
            _gameConfigData = configProvider.GetConfig<JsonDataConfig>().GameConfig;
            _updateGridsTokenSource = new CancellationTokenSource();
            _playerBasePrefab = ResourceManager.Instance.GetResource<PlayerBase>(_gameConfigData.basePrefabName);
            if (isServer)
            {
                UpdateAllPlayerGrids(_updateGridsTokenSource.Token).Forget();
            }
        }

        [Server]
        public void SpawnAllBases()
        {
            var allSpawnPoints = _gameConfigData.gameBaseData.basePositions;
            for (var i = 0; i < allSpawnPoints.Length; i++)
            {
                var spawnPoint = allSpawnPoints[i];
                _playerSpawnPoints.Add(spawnPoint, 0);
            }
            _playerBaseColliderData = GamePhysicsSystem.CreateColliderConfig(_playerBasePrefab.GetComponent<Collider>());
            var bases = _playerSpawnPoints.Keys.ToArray();
            SpawnAllBasesRpc(bases);
        }

        [ClientRpc]
        public void SpawnAllBasesRpc(Vector3[] spawnPoints)
        {
            SpawnPlayerBases(spawnPoints);
        }

        private void SpawnPlayerBases(Vector3[] spawnPoints)
        {
            for (int i = 0; i < spawnPoints.Length; i++)
            {
                var spawnPoint = spawnPoints[i];
                var playerBase = Instantiate(_playerBasePrefab, spawnPoint, Quaternion.identity);
                _playerBases.Add(i, playerBase);
            }
            _playerBaseColliderData = GamePhysicsSystem.CreateColliderConfig(_playerBasePrefab.GetComponent<Collider>());
        }

        private Vector3 GetPlayerBasePositionByNetId(uint id)
        {
            foreach (var vKey in _playerSpawnPoints.Keys)
            {
                if (_playerSpawnPoints[vKey] == id)
                {
                    return vKey;
                }
            }
            return default;
        }

        public IColliderConfig GetPlayerPhysicsData() => _playerPhysicsData;
        
        public IColliderConfig GetPlayerBaseColliderData() => _playerBaseColliderData;
        
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
            if (_playerPhysicsData == null)
            {
                var playerCollider = playerIdentity.GetComponent<Collider>();
                _playerBaseColliderData = GamePhysicsSystem.CreateColliderConfig(playerCollider);
            }
            _playerIds.Add(connectId, playerInGameData.player.PlayerId);
            _playerNetIds.Add(connectId, playerInGameData.networkIdentity.netId);
            _playerInGameData.Add(connectId, playerInGameData);
            _playerIdsByNetId.Add(playerInGameData.networkIdentity.netId, connectId);
            var pos = playerInGameData.networkIdentity.transform.position;
            var nearestBase = _gameConfigData.GetNearestBase(pos);
            _playerSpawnPoints[nearestBase] = playerInGameData.networkIdentity.netId;
            _playerPositions.Add(playerInGameData.networkIdentity.netId, pos);
            _playerGrids.Add(playerInGameData.networkIdentity.netId, _mapBoundDefiner.GetGridPosition(pos));
            RpcAddPlayer(connectId, playerInGameData);
        }

        [ClientRpc]
        private void RpcAddPlayer(int connectId, PlayerInGameData playerInGameData)
        {
            var playerIdentity = playerInGameData.networkIdentity;
            if (_playerPhysicsData == null)
            {
                var playerCollider = playerIdentity.GetComponent<Collider>();
                _playerBaseColliderData = GamePhysicsSystem.CreateColliderConfig(playerCollider);
            }
            var pos = playerInGameData.networkIdentity.transform.position;
            var nearestBase = _gameConfigData.GetNearestBase(pos);
            var index = 0;
            foreach (var key in _playerBases.Keys)
            {
                var basePosition = _playerBases[key].transform.position;
                if (nearestBase == basePosition)
                {
                    var value  = _playerBases[key];
                    value.PlayerId = GetPlayerNetId(connectId);
                    _playerBases[connectId] = value;
                    _playerBases.Remove(index);
                }
                index++;
            }
        }

        public void RemovePlayer(int connectId)
        {
            _playerIds.Remove(connectId);
            _playerNetIds.Remove(connectId);
            _playerIdsByNetId.Remove(_playerNetIds.GetValueOrDefault(connectId));
            _playerInGameData.Remove(connectId);
            _playerGrids.Remove(_playerNetIds.GetValueOrDefault(connectId));
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
        
        public Vector3 GetPlayerRebornPoint(uint playerNetId)
        {
            foreach (var v3 in _playerSpawnPoints.Keys)
            {
                if (_playerSpawnPoints[v3] == playerNetId)
                {
                    return v3;
                }
            }
            return Vector3.zero;
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
                    playerIds = playerIds,
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
                if (union.playerIds.Contains(playerId))
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
                var union = _unionData.Values.First(u => u.playerIds.Contains(playerId));
                unionPlayerIds = new HashSet<uint>(union.playerIds);
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

        public bool IsPlayerInOtherPlayerBase(uint playerNetId, out bool isPlayerInHisBase)
        {
            if (IsPlayerInHisBase(playerNetId, out var playerBasePosition))
            {
                isPlayerInHisBase = true;
                return false;
            }
            isPlayerInHisBase = false;
            var playerPosition = _playerPositions.GetValueOrDefault(playerNetId);
            var playerColliderData = _playerPhysicsData;

            foreach (var key in _playerSpawnPoints.Keys)
            {
                if (playerBasePosition == key)
                {
                    continue;
                }

                if (GamePhysicsSystem.FastCheckItemIntersects(playerPosition, key, playerColliderData,
                        _playerBaseColliderData))
                {
                    return true;
                }
            }
            return false;
        }

        public bool IsPlayerInHisBase(uint playerNetId, out Vector3 playerBasePosition)
        {
            var playerPosition = _playerPositions.GetValueOrDefault(playerNetId);
            var playerColliderData = _playerPhysicsData;
            var basePosition = GetPlayerBasePositionByNetId(playerNetId);
            var unionId = _playerUnionIds.GetValueOrDefault(playerNetId);
            if (unionId != 0)
            {
                var union = _unionData.GetValueOrDefault(unionId);
                foreach (var playerId in union.playerIds)
                {
                    if (playerId == playerNetId) continue;
                    var unionBasePosition = GetPlayerBasePositionByNetId(playerId);
                    if (GamePhysicsSystem.FastCheckItemIntersects(playerPosition, unionBasePosition, playerColliderData,_playerBaseColliderData))
                    {
                        playerBasePosition = unionBasePosition;
                        return true;
                    }
                }
            }
            playerBasePosition = basePosition;
            return GamePhysicsSystem.FastCheckItemIntersects(playerPosition, basePosition, playerColliderData,_playerBaseColliderData);
        }

        public bool TryPlayerRecoverHpInBase(int playerId, out bool isPlayerInHisBase)
        {
            var playerNetId = GetPlayerNetId(playerId);
            if (!IsPlayerInOtherPlayerBase(playerNetId, out isPlayerInHisBase) && !isPlayerInHisBase) 
                return false;
            return true;
        }

        [Server]
        public bool TryAddDeathPlayer(uint playerNetId, float countdown, int killerPlayerId, Action<uint, int, float> playerDeathCallback, Action<uint> playerBornCallback)
        {
            if (!_playerDeathCountdowns.TryAdd(playerNetId, countdown))
            {
                return false;
            }
            playerDeathCallback?.Invoke(playerNetId,killerPlayerId, countdown);
            _playerBornCallbacks.Add(playerNetId, playerBornCallback);
            return true;
        }

        // public bool CanUseShop(uint playerNetId)
        // {
        //     return IsPlayerInHisBase(playerNetId) || _playerDeathCountdowns.ContainsKey(playerNetId);
        // }

        public void Clear()
        {
            _playerIds.Clear();
            _playerNetIds.Clear();
            _playerIdsByNetId.Clear();
            _playerInGameData.Clear();
            _playerGrids.Clear();
            _playerPositions.Clear();
            _playerDeathCountdowns.Clear();
            _playerUnionIds.Clear();
            _unionData.Clear();
            _gridPlayers.Clear();
            _playerBornCallbacks.Clear();
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
        public uint[] playerIds;
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