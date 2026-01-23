using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool;
using Cysharp.Threading.Tasks;
using Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.GameBase;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Network.Server.InGame
{
    public class PlayerInGameManager : SingletonAutoNetMono<PlayerInGameManager>
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
        private readonly SyncHashSet<uint> _playerIsChangedUnion = new SyncHashSet<uint>();
        private readonly SyncGridDictionary _gridPlayers = new SyncGridDictionary();
        private readonly Dictionary<uint, Action<uint>> _playerBornCallbacks = new Dictionary<uint, Action<uint>>();
        private CancellationTokenSource _updateGridsTokenSource = new CancellationTokenSource();
        private IConfigProvider _configProvider;

        private GameConfigData _gameConfigData;


        private GameEventManager _gameEventManager;
        private PlayerBase _playerBasePrefab;
        private Collider _playerCollider;
        private SyncDictionary<Vector3, uint> _playerSpawnPoints = new SyncDictionary<Vector3, uint>();
        private Dictionary<int, PlayerBase> _playerBases = new Dictionary<int, PlayerBase>();
        private IColliderConfig _playerBaseColliderData;
        private IColliderConfig _playerPhysicsData;
        private uint _baseId;
        private GameSyncManager _gameSyncManager;
        private InteractSystem _interactSystem;
        
        // 同步给所有客户端的映射信息列表
        [SyncVar(hook = nameof(OnIsGameStartedChanged))]
        public bool isGameStarted;
        private int _localPlayerId;
        private uint _localPlayerNetId;

        public uint LocalPlayerNetId
        {
            get
            {
                if (_localPlayerNetId == 0)
                {
                    _localPlayerNetId = NetworkClient.localPlayer.netId;
                }
                return _localPlayerNetId;
            }
        }

        public Transform LocalPlayerTransform
        {
            get
            {
                if (!NetworkClient.localPlayer)
                {
                    return null;
                }
                return NetworkClient.localPlayer.transform;
            }
        }

        public int LocalPlayerId
        {
            get
            {
                if (_localPlayerId == 0)
                {
                    if (_playerIdsByNetId.TryGetValue(NetworkClient.localPlayer.netId, out var id))
                    {
                        _localPlayerId = id;
                    }
                    else if (NetworkServer.active)
                    {
                        _localPlayerId = connectionToClient.connectionId;
                    }
                }
                return _localPlayerId;
            }
            set => _localPlayerId = value;
        }

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            RegisterReaderWriter();
            _gameEventManager = gameEventManager;
            _configProvider = configProvider;
            _gameEventManager.Subscribe<GameResourceLoadedEvent>(OnGameResourceLoaded);
        }

        private void OnGameResourceLoaded(GameResourceLoadedEvent gameResourceLoadedEvent)
        {
            _gameConfigData = _configProvider.GetConfig<JsonDataConfig>().GameConfig;
        }


        [Server]
        public void SpawnAllBases(MapType mapType)
        {
            Debug.Log("SpawnAllBases" + mapType);
            var allSpawnPoints = _gameConfigData.gameBaseData.basePositions.First(x => x.mapType == mapType);
            for (var i = 0; i < allSpawnPoints.basePositions.Length; i++)
            {
                var spawnPoint = allSpawnPoints.basePositions[i];
                if (_playerSpawnPoints.ContainsKey(spawnPoint))
                {
                    continue;
                }
                _playerSpawnPoints.Add(spawnPoint, 0);
            }
            var bases = _playerSpawnPoints.Keys.ToArray();
            _playerBasePrefab ??= ResourceManager.Instance.GetResource<PlayerBase>(_gameConfigData.basePrefabName);
            for (int i = 0; i < bases.Length; i++)
            {
                var spawnPoint = bases[i];
                var playerBase = Instantiate(_playerBasePrefab.gameObject, spawnPoint, Quaternion.identity);
                NetworkServer.Spawn(playerBase);
                _playerBases.Add(i, playerBase.GetComponent<PlayerBase>());
            }
        }

        [Server]
        public int[] GetPlayerIdsByTargetType(int selfId, int count, ConditionTargetType targetType)
        {
            switch (targetType)
            {
                case ConditionTargetType.Self:
                    return new int[] { selfId };
                case ConditionTargetType.Enemy:
                    return GetPlayerIdsWithEnemy(selfId, count);
                case ConditionTargetType.Ally:
                    return GetPlayerIdsWithAlly(selfId, count);
                case ConditionTargetType.Player:
                    return GetPlayerIds(selfId, count);
                case ConditionTargetType.EnemyPlayer:
                    return GetPlayerIdsWithEnemy(selfId, count);
                case ConditionTargetType.AllyPlayer:
                    return GetPlayerIdsWithAlly(selfId, count);
                case ConditionTargetType.All:
                    return GetPlayerIds(selfId, count);
                default:
                    return new int[] { selfId };
            }
        }

        public int[] GetPlayerIds(int selfId, int count)
        {
            return _playerIds.Keys.Where(id => id != selfId).Take(count).ToArray();
        }

        private int[] GetPlayerIdsWithEnemy(int selfId, int count)
        {
            var playerIds = _playerIds.Keys;
            var enemyIds = new HashSet<int>();
            foreach (var id in playerIds)
            {
                var unionId = _playerUnionIds.GetValueOrDefault(GetPlayerNetId(id));
                if (unionId == _playerUnionIds.GetValueOrDefault(GetPlayerNetId(id)))
                {
                    enemyIds.Add(id);
                }
            }
            return enemyIds.Take(count).ToArray();
        }

        private int[] GetPlayerIdsWithAlly(int selfId, int count, bool includeSelf = false)
        {
            var playerIds = _playerIds.Keys;
            var allyIds = new HashSet<int>();
            foreach (var id in playerIds)
            {
                var unionId = _playerUnionIds.GetValueOrDefault(GetPlayerNetId(id));
                if (unionId == _playerUnionIds.GetValueOrDefault(GetPlayerNetId(selfId)))
                {
                    if (!includeSelf && id == selfId)
                    {
                        continue;
                    }
                    allyIds.Add(id);
                }
            }
            return allyIds.Take(count).ToArray();
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

        public Vector3 GetPlayerBasePositionById(int id)
        {
            return GetPlayerBasePositionByNetId(_playerNetIds.GetValueOrDefault(id));
        }

        public IColliderConfig PlayerPhysicsData
        {
            get
            {
                if (_playerPhysicsData == null)
                {
                    _playerPhysicsData = GamePhysicsSystem.CreateColliderConfig(_playerCollider);
                }
                return _playerPhysicsData;
            }
        }

        public IColliderConfig PlayerBaseColliderData
        {
            get
            {
                if (_playerBaseColliderData == null && _playerBasePrefab)
                {
                    var playerCollider = _playerBasePrefab.GetComponent<Collider>();
                    _playerBaseColliderData = GamePhysicsSystem.CreateColliderConfig(playerCollider);
                }
                return _playerBaseColliderData;
            }
        }
        
        public Vector3 GetLocalPlayerPosition()
        {
            return _playerPositions.GetValueOrDefault(GetPlayerNetId(LocalPlayerId));
        }

        private async UniTaskVoid UpdateAllPlayerGrids(CancellationToken token)
        {
            while (!token.IsCancellationRequested && isServer && !_gameSyncManager.isGameOver)
            {
                await UniTask.Delay(TimeSpan.FromSeconds(GameSyncManager.TickSeconds), cancellationToken: token);
                foreach (var uid in _playerNetIds.Values)
                {
                    var identity = GameStaticExtensions.GetNetworkIdentity(uid);
                    if (!identity) continue;
            
                    var position = identity.transform.position;
                    //Debug.Log("UpdatePlayerGrids " + position);
                    var deathCountdown = _playerDeathCountdowns.GetValueOrDefault(uid);
                    UpdatePlayerGrid(uid, position);
                    _playerPositions[uid] = position;
                    if (deathCountdown <= 0 && _playerBornCallbacks.TryGetValue(uid, out var callback))
                    {
                        _playerDeathCountdowns.Remove(uid);
                        callback.Invoke(uid);
                        _playerBornCallbacks.Remove(uid);
                        Debug.Log($"[UpdateAllPlayerGrids] Player {uid} born with countdown {deathCountdown}");
                        continue;
                    }

                    if (deathCountdown > 0)
                    {
                        deathCountdown -= GameSyncManager.TickSeconds;
                        _playerDeathCountdowns[uid] = deathCountdown;
                        Debug.Log($"[UpdateAllPlayerGrids] Player {uid} death countdown {deathCountdown}");
                    }
                }
            }
        }

        public bool IsPlayerDead(uint playerId, out float deathCountdown)
        {
            if (_playerDeathCountdowns.TryGetValue(playerId, out deathCountdown))
            {
                return deathCountdown > 0;
            }
            return false;
        }
        
        private void UpdatePlayerGrid(uint id, Vector3 playerPosition)
        {
            var newGrid = MapBoundDefiner.Instance.GetGridPosition(playerPosition);
        
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
                if (!_playerIdsByNetId.TryGetValue(id, out var playerId))
                {
                    continue;
                }
                players.Add(playerId);
            }

            return players.ToArray();
        }

        public bool TryGetPlayerById(uint id, out int playerConnectId)
        {
            if (_playerIdsByNetId.TryGetValue(id, out playerConnectId))
            {
                return true;
            }
            Debug.LogError($"Player uid {id} not found in player list.");
            return false;
        }

        public int GetPlayerId(uint id)
        {
            if (_playerIdsByNetId.TryGetValue(id, out var playerId))
            {
                return playerId;
            }
            Debug.LogWarning($"Player uid {id} not found");
            return -1;
        }

        private void OnIsGameStartedChanged(bool oldIsGameStarted, bool newIsGameStarted)
        {
            if (newIsGameStarted)
            {
                _playerCollider = ResourceManager.Instance.GetResource<Collider>(_gameConfigData.playerPrefabName);
                if (isServer)
                {
                    foreach (var key in _playerNetIds.Keys)
                    {
                        var player = NetworkServer.connections[key];
                        if (player == null) continue;
                        player.identity.transform.position = GetPlayerBasePositionByNetId(player.identity.netId);
                    }
                }
                UpdateAllPlayerGrids(_updateGridsTokenSource.Token).Forget();   
            }
        }

        public void AddPlayer(int connectId, PlayerInGameData playerInGameData)
        {
            var playerIdentity = playerInGameData.networkIdentity;
            if (_playerPhysicsData == null)
            {
                var playerCollider = playerIdentity.GetComponent<Collider>();
                _playerPhysicsData = GamePhysicsSystem.CreateColliderConfig(playerCollider);
            }
            _playerIds.TryAdd(connectId, playerInGameData.player.PlayerId);
            _playerNetIds.TryAdd(connectId, playerInGameData.networkIdentity.netId);
            _playerInGameData.TryAdd(connectId, playerInGameData);
            _playerIdsByNetId.TryAdd(playerInGameData.networkIdentity.netId, connectId);
            Debug.Log($"Player connectionId : {connectId} netId : {playerInGameData.networkIdentity.netId} added");
            var pos = playerInGameData.networkIdentity.transform.position;
            var nearestBase = _gameConfigData.GetNearestBase(GameLoopDataModel.GameSceneName.Value, pos);
            _playerSpawnPoints[nearestBase] = playerInGameData.networkIdentity.netId;
            _playerPositions.TryAdd(playerInGameData.networkIdentity.netId, pos);
            _playerGrids.TryAdd(playerInGameData.networkIdentity.netId,  MapBoundDefiner.Instance.GetGridPosition(pos));
            SetCalculatorConstants(playerIdentity);
            RpcAddPlayer(connectId, playerInGameData, playerInGameData.networkIdentity);
        }

        private void SetCalculatorConstants(NetworkIdentity identity)
        {
            _gameSyncManager??= FindObjectOfType<GameSyncManager>();
            _interactSystem??= FindObjectOfType<InteractSystem>();
            var gameData = _configProvider.GetConfig<JsonDataConfig>().GameConfig;
            var playerData = _configProvider.GetConfig<JsonDataConfig>().PlayerConfig;
            PlayerElementCalculator.SetPlayerElementComponent(_configProvider.GetConfig<ElementAffinityConfig>(), _configProvider.GetConfig<TransitionLevelBaseDamageConfig>(), _configProvider.GetConfig<ElementConfig>());
            PlayerPhysicsCalculator.SetPhysicsDetermineConstant(new PhysicsDetermineConstant
            {
                GroundMinDistance = gameData.groundMinDistance,
                GroundMaxDistance = gameData.groundMaxDistance,
                MaxSlopeAngle = gameData.maxSlopeAngle,
                StairsCheckDistance = gameData.stairsCheckDistance,
                GroundSceneLayer = gameData.groundSceneLayer,
                StairsSceneLayer = gameData.stairSceneLayer,
                RotateSpeed = playerData.RotateSpeed,
                IsServer = isServer,
                MaxDetermineDistance = gameData.maxTraceDistance,
                ViewAngle = gameData.maxViewAngle,
                ObstructionCheckRadius = gameData.obstacleCheckRadius,
                RollForce = playerData.RollForce,
                JumpSpeed = playerData.JumpSpeed,
                SpeedToVelocityRatio = playerData.SpeedToVelocityRatio,
                IsClient = isClient,
            });
            PlayerPropertyCalculator.SetCalculatorConstant(new PropertyCalculatorConstant
            {
                TickRate = GameSyncManager.TickSeconds,
                PropertyConfig =  _configProvider.GetConfig<PropertyConfig>(),
                PlayerConfig = _configProvider.GetConfig<JsonDataConfig>().PlayerConfig,
                IsServer = isServer,
                IsClient = isClient,
                IsLocalPlayer = isLocalPlayer
            });
            PlayerAnimationCalculator.SetAnimationConstant(new AnimationConstant
            {
                MaxGroundDistance = gameData.groundMaxDistance,
                InputThreshold = gameData.inputThreshold,
                AttackComboMaxCount = playerData.AttackComboMaxCount,
                AnimationConfig = _configProvider.GetConfig<AnimationConfig>(),
                IsServer = isServer,
                IsClient = isClient,
                IsLocalPlayer = isLocalPlayer
            });
            PlayerItemCalculator.SetConstant(new PlayerItemConstant
            {
                ItemConfig = _configProvider.GetConfig<ItemConfig>(),
                WeaponConfig = _configProvider.GetConfig<WeaponConfig>(),
                ArmorConfig = _configProvider.GetConfig<ArmorConfig>(),
                PropertyConfig = _configProvider.GetConfig<PropertyConfig>(),
                ConditionConfig = _configProvider.GetConfig<BattleEffectConditionConfig>(),
                ConstantBuffConfig = _configProvider.GetConfig<ConstantBuffConfig>(),
                RandomBuffConfig = _configProvider.GetConfig<RandomBuffConfig>(),
                SkillConfig = _configProvider.GetConfig<SkillConfig>(),
                GameSyncManager = _gameSyncManager,
                IsServer = isServer,
                IsClient = isClient,
                IsLocalPlayer = isLocalPlayer,
                PlayerComponentController = identity.GetComponent<PlayerComponentController>()
            });
            PlayerEquipmentCalculator.SetConstant(new PlayerEquipmentConstant
            {
                ItemConfig = _configProvider.GetConfig<ItemConfig>(),
                SkillConfig = _configProvider.GetConfig<SkillConfig>(),
                IsServer = isServer,
                IsClient = isClient,
                GameSyncManager = _gameSyncManager,
                IsLocalPlayer = isLocalPlayer,
            });
            PlayerShopCalculator.SetConstant(new ShopCalculatorConstant
            {
                ShopConfig = _configProvider.GetConfig<ShopConfig>(),
                ItemConfig = _configProvider.GetConfig<ItemConfig>(),
                PlayerConfigData = playerData,
                IsServer = isServer,
                GameSyncManager = _gameSyncManager,
                IsClient = isClient,
                IsLocalPlayer = isLocalPlayer,
                PlayerInGameManager = Instance
            });
            PlayerSkillCalculator.SetConstant(new SkillCalculatorConstant
            {
                SkillConfig = _configProvider.GetConfig<SkillConfig>(),
                SceneLayerMask = gameData.stairSceneLayer,
                IsServer = isServer,
                GameSyncManager = _gameSyncManager,
                InteractSystem = _interactSystem,
            });
        }

        // [TargetRpc]
        // public void RpcAddPlayer(NetworkIdentity client)
        // {
        //     SetCalculatorConstants(client);
        // }

        [ClientRpc]
        private void RpcAddPlayer(int connectId, PlayerInGameData playerInGameData, NetworkIdentity networkIdentity)
        {
            var playerIdentity = playerInGameData.networkIdentity;
            if (_playerPhysicsData == null)
            {
                var playerCollider = playerIdentity.GetComponent<Collider>();
                _playerPhysicsData = GamePhysicsSystem.CreateColliderConfig(playerCollider);
            }
            var pos = playerInGameData.networkIdentity.transform.position;
            var nearestBase = _gameConfigData.GetNearestBase(GameLoopDataModel.GameSceneName.Value, pos);
            var basePosition = _playerBases.FirstOrDefault(x => x.Value.transform.position == nearestBase);
            if (basePosition.Key != 0)
            {
                var value  = _playerBases[basePosition.Key];
                value.PlayerId = GetPlayerNetId(connectId);
                _playerBases[connectId] = value;
                _playerBases.Remove(basePosition.Key);
            }
            SetCalculatorConstants(networkIdentity);
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
        public void RandomUnion(out int noUnionPlayerId)
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
            var noUnionPlayers = GetPlayerWithNoUnion();
            if (noUnionPlayers == null || noUnionPlayers.Count == 0)
            {
                noUnionPlayerId = 0;
                return;
            }
            var id = GetPlayerWithNoUnion().First();
            noUnionPlayerId = _playerIdsByNetId[id];
        }
        
        public HashSet<uint> GetPlayerWithNoUnion()
        {
            HashSet<uint> noUnionPlayers = null;
            foreach (var player in _unionData.Keys)
            {
                var union = _unionData[player];
                if (union.playerIds.Length == 1)
                {
                    noUnionPlayers = new HashSet<uint>(union.playerIds);
                    break;
                }
            }
            return noUnionPlayers;
        }

        public bool CanExchangeUnion(uint killerNetId, uint deathPlayerNetId)
        {
            if (!IsPlayerInUnion(deathPlayerNetId))
            {
                Debug.Log($"Player {killerNetId} not in union");
                return false;
            }

            if (_playerIsChangedUnion.Contains(killerNetId))
            {
                Debug.Log($"Player {killerNetId} has changed");
                return false;
            }
            
            var exchangeUnionId = _playerUnionIds.GetValueOrDefault(deathPlayerNetId);
            var union = _unionData.GetValueOrDefault(exchangeUnionId);

            if (union.playerIds.Length == 1)
            {
                Debug.Log($"Player {deathPlayerNetId} has no union");
                return false;
            }
            
            var oldUnionId = _playerUnionIds.GetValueOrDefault(killerNetId);
            if (exchangeUnionId == oldUnionId)
            {
                Debug.Log($"Player {killerNetId} and {deathPlayerNetId} are in the same union");
                return false;
            }

            return true;
        }

        public bool TryPlayerExchangeUnion(uint killerNetId, uint deathPlayerNetId, out UnionData exchangeUnion, out UnionData oldUnion)
        {
            exchangeUnion = default;
            oldUnion = default;
            if (!CanExchangeUnion(killerNetId, deathPlayerNetId))
            {
                Debug.Log($"Player {deathPlayerNetId} can not exchange union with {killerNetId}");
                return false;
            }

            var exchangeUnionId = _playerUnionIds.GetValueOrDefault(deathPlayerNetId);
            var oldUnionId = _playerUnionIds.GetValueOrDefault(killerNetId);
            exchangeUnion = _unionData[exchangeUnionId];
            oldUnion = _unionData[oldUnionId];
            var oldUnionPlayerIds = oldUnion.playerIds.ToList();
            var exchangeUnionPlayerIds = exchangeUnion.playerIds.ToList();
            oldUnionPlayerIds.Remove(deathPlayerNetId);
            exchangeUnionPlayerIds.Remove(killerNetId);
            exchangeUnionPlayerIds.Add(killerNetId);
            oldUnionPlayerIds.Add(deathPlayerNetId);
            exchangeUnion = new UnionData
            {
                unionId = exchangeUnionId,
                playerIds = exchangeUnionPlayerIds.ToArray()
            };
            oldUnion = new UnionData
            {
                unionId = oldUnionId,
                playerIds = oldUnionPlayerIds.ToArray()
            };
            _unionData[exchangeUnionId] = exchangeUnion;
            _unionData[oldUnionId] = oldUnion;
            _playerUnionIds[killerNetId] = exchangeUnionId;
            _playerUnionIds[deathPlayerNetId] = oldUnionId;
            _playerIsChangedUnion.Add(killerNetId);
            return true;
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
                        PlayerBaseColliderData))
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
                    if (GamePhysicsSystem.FastCheckItemIntersects(playerPosition, unionBasePosition, playerColliderData,PlayerBaseColliderData))
                    {
                        playerBasePosition = unionBasePosition;
                        return true;
                    }
                }
            }
            playerBasePosition = basePosition;
            return GamePhysicsSystem.FastCheckItemIntersects(playerPosition, basePosition, playerColliderData,PlayerBaseColliderData);
        }

        public bool TryPlayerRecoverHpInBase(int playerId, out bool isPlayerInHisBase)
        {
            var playerNetId = GetPlayerNetId(playerId);
            if (!IsPlayerInOtherPlayerBase(playerNetId, out isPlayerInHisBase) && !isPlayerInHisBase) 
                return false;
            return true;
        }

        [Server]
        public bool TryAddDeathPlayer(uint playerNetId, float countdown, uint killerPlayerId, Action<uint, uint, float> playerDeathCallback, Action<uint> playerBornCallback)
        {
            if (!_playerDeathCountdowns.TryAdd(playerNetId, countdown))
            {
                return false;
            }
            Debug.Log($"[PlayerInGameManager] Add death player {playerNetId}");
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

        public Vector3 GetPlayerPosition(int headerConnectionId)
        {
            return _playerPositions.GetValueOrDefault(GetPlayerNetId(headerConnectionId));
        }

        public bool IsPlayer(uint uid)
        {
            return _playerGrids.ContainsKey(uid);
        }

        public Vector3 GetOtherPlayerNearestPlayer(int headerConnectionId, float distance)
        {
            var playerNetId = GetPlayerNetId(headerConnectionId);
            var playerPosition = _playerPositions.GetValueOrDefault(playerNetId);
            var nearestPlayer = Vector3.zero;
            foreach (var key in _playerPositions.Keys)  
            {
                if (key == playerNetId) continue;
                var otherPlayerPosition = _playerPositions.GetValueOrDefault(key);
                if (Vector3.Distance(playerPosition, otherPlayerPosition) <= distance)
                {
                    if (nearestPlayer == Vector3.zero)
                    {
                        nearestPlayer = otherPlayerPosition;
                    }
                    else
                    {
                        var distanceToNearestPlayer = Vector3.Distance(playerPosition, nearestPlayer);
                        var distanceToOtherPlayer = Vector3.Distance(playerPosition, otherPlayerPosition);
                        if (distanceToOtherPlayer < distanceToNearestPlayer)
                        {
                            nearestPlayer = otherPlayerPosition;
                        }
                    }
                }
            }
            return nearestPlayer;
        }

        public List<int> GetOtherPlayersWithinRange(int headerConnectionId, float distance)
        {
            var playerNetId = GetPlayerNetId(headerConnectionId);
            var playerPosition = _playerPositions.GetValueOrDefault(playerNetId);
            var otherPlayers = new List<int>();
            foreach (var key in _playerPositions.Keys)
            {
                if (key == playerNetId) continue;
                var otherPlayerPosition = _playerPositions.GetValueOrDefault(key);
                if (Vector3.Distance(playerPosition, otherPlayerPosition) <= distance)
                {
                    otherPlayers.Add(GetPlayerId(key));
                }
            }
            return otherPlayers;
        
        }

        public Vector3 GetPositionInPlayerDirection(int headerConnectionId, Vector3 direction, float distance)
        {
            var playerNetId = GetPlayerNetId(headerConnectionId);
            var playerPosition = _playerPositions.GetValueOrDefault(playerNetId);
            var player = NetworkServer.connections[headerConnectionId].identity.transform;
            var newPosition = playerPosition + (direction == Vector3.zero ? player.forward : direction.normalized) * distance;
            return newPosition;
        }

        // public int[] GetHitPlayers(Vector3 position, IColliderConfig colliderConfig)
        // {
        //     var hitPlayers = new List<int>();
        //     foreach (var key in _playerPositions.Keys)
        //     {
        //         var playerPosition = _playerPositions.GetValueOrDefault(key);
        //         if (GamePhysicsSystem.FastCheckItemIntersects(position, playerPosition, colliderConfig, _playerPhysicsData))
        //         {
        //             hitPlayers.Add(GetPlayerId(key));
        //         }
        //     }
        //     return hitPlayers.ToArray();
        // }
    }

    [Serializable]
    public class PlayerInGameData
    {
        public PlayerReadOnlyData player;
        public NetworkIdentity networkIdentity;
        public uint playerNetId;
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