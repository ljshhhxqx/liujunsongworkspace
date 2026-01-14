using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Collector.Collects;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Effect;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.ObjectPool;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.Interact
{
    public class InteractSystem : NetworkHandlerBehaviour
    {
        private ItemsSpawnerManager _itemsSpawnerManager;
        private readonly ConcurrentQueue<IInteractRequest> _commandQueue = new ConcurrentQueue<IInteractRequest>();
        private CancellationTokenSource _cts = new CancellationTokenSource();
        private GameObject _bulletPrefab;
        private GameObject _wellPrefab;
        private GameObject _trainPrefab;
        private MapElementData _mapElementData;
        private GameObject _rocketPrefab;
        private JsonDataConfig _jsonConfig;
        private GameSyncManager _gameSyncManager;
        private GameEventManager _gameEventManager;
        private PlayerPropertySyncSystem _playerPropertySyncSystem;
        private List<PlayerPropertySyncSystem.SkillBuffManagerData> _activeBuffs = new List<PlayerPropertySyncSystem.SkillBuffManagerData>();
        private SyncDictionary<uint, SceneItemInfo> _sceneItems = new SyncDictionary<uint, SceneItemInfo>();
        private HashSet<DynamicObjectData> _dynamicObjects = new HashSet<DynamicObjectData>();

        [SyncVar] public int currentTrainId;
        [SyncVar] public int currentWellId;
        
        public event Action<uint, SceneItemInfo> SceneItemInfoChanged;
        public event Action<uint, float, ControlSkillType> ItemControlSkillChanged;
        
        public bool IsItemCanPickup(uint sceneItemId)
        {
            if (!_sceneItems.TryGetValue(sceneItemId, out var sceneItemInfo))
            {
                Debug.Log($"Scene item {sceneItemId} not found");
                return false;
                
            }
            //Debug.Log($"Scene item {sceneItemId}  heath is {sceneItemInfo.health}");
            return sceneItemInfo.health <= 1;
        }
        
        [Inject]
        private void Init(GameEventManager gameEventManager, IConfigProvider configProvider)
        {
            _gameEventManager = gameEventManager;
            //_gameSyncManager = FindObjectOfType<GameSyncManager>();
            SceneItemWriter();
            _jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            _gameSyncManager = FindObjectOfType<GameSyncManager>();
            _gameEventManager.Subscribe<GameStartEvent>(OnGameStart);
            _gameEventManager.Subscribe<PlayerAttackItemEvent>(OnPlayerAttackItem);
            _gameEventManager.Subscribe<PlayerSkillItemEvent>(OnSkillItem);
            _gameEventManager.Subscribe<SceneItemInfoChanged>(OnItemSpawned);
            _gameEventManager.Subscribe<StartGameWellEvent>(OnStartGameWell);
            _gameEventManager.Subscribe<StartGameTrainEvent>(OnStartGameTrain);
            _sceneItems.OnChange += OnSceneItemsChanged;
        }

        private void OnStartGameTrain(StartGameTrainEvent startGameTrainEvent)
        {
            currentTrainId = startGameTrainEvent.TrainId;
        }

        private void OnStartGameWell(StartGameWellEvent startGameWellEvent)
        {
            currentWellId = startGameWellEvent.WellId;
            NetworkGameObjectPoolManager.Instance.Spawn(_wellPrefab, startGameWellEvent.SpawnPosition,
                Quaternion.identity);
        }

        private void OnSceneItemsChanged(SyncIDictionary<uint, SceneItemInfo>.Operation type, uint uid, SceneItemInfo info)
        {
            if (!_sceneItems.TryGetValue(uid, out var value))
            {
                return;
            }
            SceneItemInfoChanged?.Invoke(uid, value);
            _gameEventManager.Publish(new SceneItemInfoChangedEvent(uid, value, type));
            Debug.Log($"[OnSceneItemsChanged] {type} scene item {uid} info - {value}");
        }

        public HashSet<DynamicObjectData> GetHitObjectDatas(uint uid, Vector3 position, IColliderConfig config)
        {
            var result = new HashSet<DynamicObjectData>();
            var cache = new HashSet<DynamicObjectData>();
            if (GameObjectContainer.Instance.DynamicObjectIntersects(uid, position, config, result))
            {
                foreach (var objectData in result)
                {
                    if (PlayerInGameManager.Instance.IsPlayer(objectData.NetId))
                    {
                        cache.Add(objectData);
                    }
                    else if (_sceneItems.TryGetValue(objectData.NetId, out var sceneItemInfo))
                    {
                        if (sceneItemInfo.health > 1 && sceneItemInfo.maxHealth > 1)
                        {
                            cache.Add(objectData);
                        }
                    }
                }
            }
            return cache;
        }

        public Vector3 GetNearestObject(uint uid, float distance)
        {
            var data = GameObjectContainer.Instance.GetDynamicObjectData(uid);
            foreach (var key in _sceneItems.Keys)
            {
                if (_sceneItems[key].maxHealth > 1)
                {
                    var position = GameObjectContainer.Instance.GetDynamicObjectData(key).Position;
                    if (Vector3.Distance(position, data.Position) < distance)
                    {
                        return position;
                    }
                }
            }
            return Vector3.zero;
        }

        private void OnItemSpawned(SceneItemInfoChanged sceneItemInfoChanged)
        {
            if (!_sceneItems.TryGetValue(sceneItemInfoChanged.ItemId, out var sceneItemInfo))
            {
                sceneItemInfo = new SceneItemInfo
                {
                    health = sceneItemInfoChanged.SceneItemInfo.health,
                    defense = sceneItemInfoChanged.SceneItemInfo.defense,
                    speed = sceneItemInfoChanged.SceneItemInfo.speed,
                    attackDamage = sceneItemInfoChanged.SceneItemInfo.attackDamage,
                    attackRange = sceneItemInfoChanged.SceneItemInfo.attackRange,
                    attackInterval = sceneItemInfoChanged.SceneItemInfo.attackInterval,
                    maxHealth = sceneItemInfoChanged.SceneItemInfo.maxHealth,
                    sceneItemId = sceneItemInfoChanged.ItemId,
                    Position = sceneItemInfoChanged.Position,
                    criticalRate = sceneItemInfoChanged.SceneItemInfo.criticalRate,
                    criticalDamageRatio = sceneItemInfoChanged.SceneItemInfo.criticalDamageRatio,
                };
                Debug.Log($"Add scene item {sceneItemInfoChanged.ItemId} to scene items");
                _sceneItems.Add(sceneItemInfoChanged.ItemId, sceneItemInfo);
            }
        }

        private void OnSkillItem(PlayerSkillItemEvent playerSkillItemEvent)
        {
            var effectData = playerSkillItemEvent.SkillHitExtraEffectData;
            var memoryProperty = playerSkillItemEvent.PlayerState.MemoryProperty;
            if (_sceneItems.TryGetValue(playerSkillItemEvent.DefenderId, out var sceneItemInfo) && sceneItemInfo.health > 1)
            {
                float value;
                if (effectData.baseValue < 1)
                {
                    value = memoryProperty.GetValueOrDefault(effectData.buffProperty).CurrentValue * (effectData.baseValue + effectData.extraRatio);
                }
                else
                {
                    value = effectData.baseValue + memoryProperty.GetValueOrDefault(effectData.buffProperty).CurrentValue * effectData.extraRatio;
                }
                if (effectData.effectProperty == PropertyTypeEnum.Health)
                {
                    var damageResult = _jsonConfig.GetDamage(value, sceneItemInfo.defense, memoryProperty.GetValueOrDefault(PropertyTypeEnum.CriticalRate).CurrentValue, memoryProperty.GetValueOrDefault(PropertyTypeEnum.CriticalDamageRatio).CurrentValue);
                    Debug.Log($"[OnSkillItem] Player {playerSkillItemEvent.PlayerId} attack scene item {playerSkillItemEvent.DefenderId} with damage {damageResult.Damage}");
                    sceneItemInfo.health -= damageResult.Damage;
                    sceneItemInfo.health = Mathf.Max(sceneItemInfo.health, 0);
                }
                else if (effectData.effectProperty is PropertyTypeEnum.Defense or PropertyTypeEnum.Attack or PropertyTypeEnum.CriticalRate 
                         or PropertyTypeEnum.CriticalDamageRatio or PropertyTypeEnum.AttackRadius or PropertyTypeEnum.Speed || effectData.isBuffMaxProperty)
                {
                    sceneItemInfo = sceneItemInfo.UpdateItemInfo(effectData.effectProperty, effectData.isBuffMaxProperty, value);
                    var buff = new PlayerPropertySyncSystem.SkillBuffManagerData
                    {
                        playerId = playerSkillItemEvent.DefenderId,
                        value = value,
                        duration = effectData.duration,
                        operationType = effectData.operation,
                        increaseType = effectData.buffIncreaseType,
                        currentTime = effectData.duration,
                        propertyType = effectData.effectProperty,
                        skillType = effectData.controlSkillType,
                    };
                    _activeBuffs.Add(buff);
                }
                _sceneItems[playerSkillItemEvent.DefenderId] = sceneItemInfo;
                SceneItemInfoChanged?.Invoke(playerSkillItemEvent.DefenderId, sceneItemInfo);
                if (sceneItemInfo.health <= 0)
                {
                    Debug.Log($"[OnSkillItem] Scene item {playerSkillItemEvent.DefenderId} is dead");
                    _sceneItems.Remove(playerSkillItemEvent.DefenderId);
                }

                if (effectData.controlSkillType!= ControlSkillType.None)
                {
                    ItemControlSkillChanged?.Invoke(playerSkillItemEvent.DefenderId, playerSkillItemEvent.SkillHitExtraEffectData.duration, playerSkillItemEvent.SkillHitExtraEffectData.controlSkillType);
                }
            }
            else
            {
                Debug.LogError($"[OnSkillItem] Scene item {playerSkillItemEvent.DefenderId} not found");
            }
        }

        private void HandleItemSkillBuffMove(PlayerPropertySyncSystem.SkillBuffManagerData buff, int index)
        {
            if (_sceneItems.TryGetValue(buff.playerId, out var sceneItemInfo))
            {
                sceneItemInfo = sceneItemInfo.UpdateItemInfo(buff.propertyType, buff.isMaxProperty, -buff.value);
                _sceneItems[buff.playerId] = sceneItemInfo;
                _activeBuffs.RemoveAt(index);
            }
        }

        private void OnPlayerAttackItem(PlayerAttackItemEvent playerAttackItemEvent)
        {
            var criticalRate = playerAttackItemEvent.AttackerState.MemoryProperty.GetValueOrDefault(PropertyTypeEnum.CriticalRate).CurrentValue;
            var criticalDamage = playerAttackItemEvent.AttackerState.MemoryProperty.GetValueOrDefault(PropertyTypeEnum.CriticalDamageRatio).CurrentValue;
            var attackPower = playerAttackItemEvent.AttackerState.MemoryProperty.GetValueOrDefault(PropertyTypeEnum.Attack).CurrentValue;
            foreach (var sceneItemId in playerAttackItemEvent.DefenderIds)
            {
                if (_sceneItems.TryGetValue(sceneItemId, out var sceneItemInfo))
                {
                    var damageResult = _jsonConfig.GetDamage(attackPower, sceneItemInfo.defense, criticalRate, criticalDamage);
                    var health = sceneItemInfo.health;
                    health -= damageResult.Damage;
                    sceneItemInfo.health = Mathf.Max(0, health);
                    Debug.Log($"[OnPlayerAttackItem] Player {playerAttackItemEvent.AttackerId} attack scene item {sceneItemId} with damage {damageResult.Damage},now health {health} - max health {sceneItemInfo.maxHealth}");
                    _sceneItems[sceneItemId] = sceneItemInfo;
                    SceneItemInfoChanged?.Invoke(sceneItemId, sceneItemInfo);
                    var identity = GameStaticExtensions.GetNetworkIdentity(sceneItemId);
                    if (identity)
                    {
                        var collectObjectController = identity.GetComponent<IEffectPlayer>();
                        collectObjectController.RpcPlayEffect(ParticlesType.HitEffect);
                    }

                    if (sceneItemInfo.health == 0)
                    {
                        Debug.Log($"[OnPlayerAttackItem] Scene item {sceneItemId} is dead");
                        _sceneItems.Remove(sceneItemId);
                    }
                }
            }
        }

        private void OnGameStart(GameStartEvent gameStartEvent)
        {
            _playerPropertySyncSystem = _gameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            _bulletPrefab = ResourceManager.Instance.GetResource<GameObject>("Bullet");
            _wellPrefab = ResourceManager.Instance.GetResource<GameObject>("Well");
            _trainPrefab = ResourceManager.Instance.GetResource<GameObject>("Train");
            _rocketPrefab = ResourceManager.Instance.GetResource<GameObject>("RocketSpace");
            if (!ServerHandler)
            {
                return;
            }
            //Debug.Log($"InteractSystem start isClient-{isClient} isServer-{isServer} isLocalPlayer-{isLocalPlayer}");
            UpdateInteractRequests(_cts.Token).Forget();
            UpdateBuffs(GameSyncManager.ServerUpdateInterval);
            switch (GameLoopDataModel.GameSceneName.Value)
            {
                case MapType.Rocket:
                    var position = _mapElementData.spawnRockerPosition;
                    NetworkGameObjectPoolManager.Instance.Spawn(_rocketPrefab, position, Quaternion.identity);
                    break;
                case MapType.WestWild:
                    position = _mapElementData.spawnTrainPosition;
                    NetworkGameObjectPoolManager.Instance.Spawn(_trainPrefab, position, Quaternion.identity);
                    break;
            }
        }

        private void UpdateBuffs(float serverUpdateInterval)
        {
            if (_activeBuffs.Count == 0)
            {
                for (int i = 0; i < _activeBuffs.Count; i++)
                {
                    _activeBuffs[i] = _activeBuffs[i].Update(serverUpdateInterval);
                    if (_activeBuffs[i].currentTime <= 0)
                    {
                        HandleItemSkillBuffMove(_activeBuffs[i], i);
                        _activeBuffs.RemoveAt(i);
                    }
                }
            }
        }

        private async UniTaskVoid UpdateInteractRequests(CancellationToken cts)
        {
            while (!_cts.IsCancellationRequested)
            {
                await UniTask.WaitUntil(() => !_commandQueue.IsEmpty, 
                    cancellationToken: cts);
                while (_commandQueue.TryDequeue(out var command))
                {
                    switch (command)
                    {
                        case SceneInteractRequest sceneInteractRequest:
                            Debug.Log($"UpdateInteractRequests - {(InteractionType)sceneInteractRequest.InteractionType}-{sceneInteractRequest.SceneItemId}");
                            HandleSceneInteractRequest(sceneInteractRequest);
                            break;
                        case PlayerInteractRequest playerInteractRequest:
                            HandlePlayerInteractRequest(playerInteractRequest);
                            break;
                        case EnvironmentInteractRequest environmentInteractRequest:
                            HandleEnvironmentInteractRequest(environmentInteractRequest);
                            break;
                        case PlayerToSceneRequest playerInteractRequest:
                            HandlePlayerToSceneRequest(playerInteractRequest);
                            break;
                        case PlayerChangeUnionRequest playerKillPlayerRequest:
                            HandlePlayerChangeUnion(playerKillPlayerRequest);
                            break;
                        case SceneToPlayerInteractRequest sceneToPlayerInteractRequest:
                            HandleSceneToPlayerInteractRequest(sceneToPlayerInteractRequest);
                            break;
                        case SceneToSceneInteractRequest sceneToSceneInteractRequest:
                            HandleSceneToSceneInteractRequest(sceneToSceneInteractRequest);
                            break;
                        case SpawnBullet spawnBullet:
                            HandleSpawnBullet(spawnBullet);
                            break;
                        case SceneItemAttackInteractRequest sceneItemAttackInteractRequest:
                            HandleSceneItemAttackInteractRequest(sceneItemAttackInteractRequest);
                            break;
                        case ItemExplodeRequest itemExplodeRequest:
                            HandleItemExplodeRequest(itemExplodeRequest);
                            break;
                    }
                }
            }
        }

        private void HandleItemExplodeRequest(ItemExplodeRequest itemExplodeRequest)
        {
            if (!NetworkServer.spawned.TryGetValue(itemExplodeRequest.SceneItemId, out var sceneObject))
            {
                Debug.LogError($"Scene item {itemExplodeRequest.SceneItemId} not found");
                return;
            }

            var colliderConfig = GamePhysicsSystem.CreateColliderConfig(ColliderType.Sphere, Vector3.zero, Vector3.zero,
                itemExplodeRequest.Radius);
            float defense;
            if (GameObjectContainer.Instance.DynamicObjectIntersects(itemExplodeRequest.SceneItemId,sceneObject.transform.position, colliderConfig, _dynamicObjects))
            {
                foreach (var hitObjectData in _dynamicObjects)
                {
                    if (!NetworkServer.spawned.TryGetValue(hitObjectData.NetId, out var hitObject))
                    {
                        Debug.LogError($"Scene item {itemExplodeRequest.SceneItemId} not found");
                        continue;
                    }
                    if (_sceneItems.TryGetValue(hitObjectData.NetId, out var sceneItemInfo))
                    {
                        defense = sceneItemInfo.defense;
                        var damage = _jsonConfig.GetDamage(itemExplodeRequest.AttackPower, defense, 1f, 2f);
                        Debug.Log($"[HandleItemExplodeRequest] Scene item {itemExplodeRequest.SceneItemId} attack scene item {hitObjectData.NetId} with damage {damage}");
                        sceneItemInfo.health -= damage.Damage;
                        sceneItemInfo.health = Mathf.Max(sceneItemInfo.health, 0);
                        SceneItemInfoChanged?.Invoke(hitObjectData.NetId, sceneItemInfo);
                        _sceneItems[hitObjectData.NetId] = sceneItemInfo;
                        if (sceneItemInfo.health == 0)
                        {
                            Debug.Log($"[HandleItemExplodeRequest] Scene item {hitObjectData.NetId} is dead");
                            _sceneItems.Remove(hitObjectData.NetId);
                        }
                    }
                    else if (PlayerInGameManager.Instance.TryGetPlayerById(hitObjectData.NetId, out var connectionId))
                    {
                        var property = _playerPropertySyncSystem.GetPlayerProperty(connectionId);
                        defense = property.GetValueOrDefault(PropertyTypeEnum.Defense).CurrentValue;
                        var damage = _jsonConfig.GetDamage(itemExplodeRequest.AttackPower, defense, 1, 2);
                        Debug.Log($"[HandleItemExplodeRequest] Scene item {hitObjectData.NetId} attack player {hitObjectData.NetId} with damage {damage}");
                        var command = new PropertyItemAttackCommand
                        {
                            TargetId = hitObjectData.NetId,
                            Header = GameSyncManager.CreateNetworkCommandHeader(0, CommandType.Property, CommandAuthority.Server),
                            Damage = damage,
                            AttackerId = itemExplodeRequest.SceneItemId, 
                        };
                        _gameSyncManager.EnqueueServerCommand(command);
                    }
                    
                }
            }
        }

        private void HandleSceneItemAttackInteractRequest(SceneItemAttackInteractRequest sceneItemAttackInteractRequest)
        {
            if (!NetworkServer.spawned.TryGetValue(sceneItemAttackInteractRequest.SceneItemId, out var sceneObject))
            {
                Debug.LogError($"[HandleSceneItemAttackInteractRequest] Scene item {sceneItemAttackInteractRequest.SceneItemId} not found");
                return;
            }

            if (!NetworkServer.spawned.TryGetValue(sceneItemAttackInteractRequest.TargetId, out var targetSceneObject))
            {
                Debug.LogError($"[HandleSceneItemAttackInteractRequest] Target Scene item {sceneItemAttackInteractRequest.TargetId} not found");
                return;
            }

            var attackPower = sceneItemAttackInteractRequest.AttackPower;
            var criticalRate = sceneItemAttackInteractRequest.CriticalRate;
            var criticalDamage = sceneItemAttackInteractRequest.CriticalDamage;
            float defense;

            if (_sceneItems.TryGetValue(sceneItemAttackInteractRequest.TargetId, out var sceneItemInfo))
            {
                if (sceneItemInfo.health == 0)
                    return;
                defense = sceneItemInfo.defense;
                var damage = _jsonConfig.GetDamage(attackPower, defense, criticalRate, criticalDamage);
                Debug.Log($"[HandleSceneItemAttackInteractRequest] Scene item {sceneItemAttackInteractRequest.SceneItemId} attack scene item {sceneItemAttackInteractRequest.TargetId} with damage {damage.Damage}");
                sceneItemInfo.health -= damage.Damage;
                sceneItemInfo.health = Mathf.Max(0, sceneItemInfo.health);
                _sceneItems[sceneItemInfo.sceneItemId] = sceneItemInfo;
                if (sceneItemInfo.health == 0)
                {
                    Debug.Log($"[HandleSceneItemAttackInteractRequest] Scene item {sceneItemAttackInteractRequest.TargetId} is dead");
                    SceneItemInfoChanged?.Invoke(sceneItemAttackInteractRequest.TargetId, sceneItemInfo);
                    _sceneItems.Remove(sceneItemAttackInteractRequest.TargetId);
                }
            }
            else if (PlayerInGameManager.Instance.TryGetPlayerById(sceneItemAttackInteractRequest.TargetId, out var connectionId))
            {
                var property = _playerPropertySyncSystem.GetPlayerProperty(connectionId);
                defense = property.GetValueOrDefault(PropertyTypeEnum.Defense).CurrentValue;
                var damage = _jsonConfig.GetDamage(attackPower, defense, criticalRate, criticalDamage);
                Debug.Log($"[HandleSceneItemAttackInteractRequest] Scene item {sceneItemAttackInteractRequest.SceneItemId} attack player {sceneItemAttackInteractRequest.TargetId} with damage {damage.Damage}");
                var command = new PropertyItemAttackCommand
                {
                    TargetId = sceneItemAttackInteractRequest.TargetId,
                    Header = GameSyncManager.CreateNetworkCommandHeader(0, CommandType.Property, CommandAuthority.Server),
                    Damage = damage,
                    AttackerId = sceneItemAttackInteractRequest.SceneItemId, 
                };
                _gameSyncManager.EnqueueServerCommand(command);
            }
            
        }

        private void HandleSpawnBullet(SpawnBullet spawnBullet)
        {
             var go = NetworkGameObjectPoolManager.Instance.Spawn(_bulletPrefab, position: spawnBullet.StartPosition + spawnBullet.Direction.ToVector3() * 0.5f, rotation: Quaternion.identity);
             var bullet = go.GetComponent<ItemBullet>();
             bullet.Init(spawnBullet.Direction, spawnBullet.Speed, spawnBullet.LifeTime, spawnBullet.AttackPower, spawnBullet.Spawner, spawnBullet.CriticalRate, spawnBullet.CriticalDamageRatio);
        }

        private void HandleSceneToSceneInteractRequest(SceneToSceneInteractRequest sceneToSceneInteractRequest)
        {
            if (!NetworkServer.spawned.TryGetValue(sceneToSceneInteractRequest.SceneItemId, out var sceneObject))
            {
                Debug.LogError($"Scene item {sceneToSceneInteractRequest.SceneItemId} not found");
                return;
            }

            if (!NetworkServer.spawned.TryGetValue(sceneToSceneInteractRequest.TargetSceneItemId, out var targetSceneObject))
            {
                Debug.LogError($"Target Scene item {sceneToSceneInteractRequest.TargetSceneItemId} not found");
                return;
            }
            var sceneData = _sceneItems[sceneToSceneInteractRequest.SceneItemId];
        }

        private void HandleSceneToPlayerInteractRequest(SceneToPlayerInteractRequest sceneToPlayerInteractRequest)
        {
            if (!NetworkServer.spawned.TryGetValue(sceneToPlayerInteractRequest.SceneItemId, out var sceneObject))
            {
                Debug.LogError($"Scene item {sceneToPlayerInteractRequest.SceneItemId} not found");
                return;
            }

            if (!NetworkServer.spawned.TryGetValue(sceneToPlayerInteractRequest.TargetPlayerId, out var targetSceneObject))
            {
                Debug.LogError($"Target Scene item {sceneToPlayerInteractRequest.TargetPlayerId} not found");
                return;
            }
        }

        private void HandlePlayerChangeUnion(PlayerChangeUnionRequest playerChangeUnionRequest)
        {
            var changedResult = PlayerInGameManager.Instance.TryPlayerExchangeUnion(playerChangeUnionRequest.KillerPlayerId,
                playerChangeUnionRequest.DeadPlayerId, out _, out _);
            if (changedResult)
            {
                Debug.Log($"Player {playerChangeUnionRequest.KillerPlayerId} changed union with player {playerChangeUnionRequest.DeadPlayerId}");
            }
            else
            {
                Debug.Log($"Player {playerChangeUnionRequest.KillerPlayerId} failed to change union with player {playerChangeUnionRequest.DeadPlayerId}");
            }
        }

        private void HandlePlayerToSceneRequest(PlayerToSceneRequest playerInteractRequest)
        {
            
        }

        public static InteractHeader CreateInteractHeader(int? connectionId, InteractCategory category, CompressedVector3 position = default, CommandAuthority authority = CommandAuthority.Server)
        {
            int? noSequence = null;
            var connectionIdValue = connectionId.GetValueOrDefault();
            var header = ObjectPoolManager<InteractHeader>.Instance.Get(35);
            header.Clear();
            header.CommandId = HybridIdGenerator.GenerateCommandId(authority == CommandAuthority.Server, CommandType.Interact, 0, ref noSequence);
            header.RequestConnectionId = connectionIdValue;
            header.Tick = GameSyncManager.CurrentTick;
            header.Category = category;
            header.Position = position;
            header.Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            header.Authority = authority;
            ObjectPoolManager<InteractHeader>.Instance.Return(header);
            return header;
        }

        public void EnqueueCommand<T>(T request) where T : IInteractRequest
        {
            if (ServerHandler)
            {
                var header = request.GetHeader();
                var validCommand = request.CommandValidResult();
                if (!validCommand.IsValid)
                {
                    StringBuilder sb = new StringBuilder();
                    sb.AppendLine($"Invalid command: {header}");
                    foreach (var error in validCommand.Errors)
                    {
                        sb.AppendLine(error);
                    }
                    Debug.LogError(sb.ToString());
                    return;
                }
                _commandQueue.Enqueue(request);
            }
        }

        private void HandleSceneInteractRequest(SceneInteractRequest request)
        {
            var header = request.GetHeader();
            var playerNetId = PlayerInGameManager.Instance.GetPlayerNetId(header.RequestConnectionId);
            switch ((InteractionType)request.InteractionType)
            {
                case InteractionType.PickupItem:
                    // if (!IsItemCanPickup(request.SceneItemId))
                    // {
                    //     Debug.LogError($"Can't pickup item {request.SceneItemId}");
                    //     return;
                    // }
                    Debug.Log($"Player {playerNetId} pickup item {request.SceneItemId}");
                    _itemsSpawnerManager.PickerPickupItem(playerNetId, request.SceneItemId);
                    break;
                case InteractionType.PickupChest:
                    _itemsSpawnerManager.PickerPickUpChest(playerNetId, request.SceneItemId);
                    break;
                case InteractionType.TouchRocket:
                    Debug.Log($"Player {playerNetId} touch rocket {request.SceneItemId}");
                    _gameEventManager.Publish(new TakeTrainEvent(currentTrainId, playerNetId));
                    break;
                case InteractionType.TouchWell:
                    _gameEventManager.Publish(new PlayerTouchWellEvent(playerNetId, request.SceneItemId));
                    break;
                case InteractionType.TouchTrainDeath:
                    _gameEventManager.Publish(new TrainAttackPlayerEvent(currentTrainId, playerNetId));
                    break;
                case InteractionType.TouchTrain:
                    Debug.Log($"Player {playerNetId} touch train {request.SceneItemId}");
                    _gameEventManager.Publish(new TakeTrainEvent(currentTrainId, playerNetId));
                    break;
            }
        }

        private void HandlePlayerInteractRequest(PlayerInteractRequest request)
        {
        }

        private void HandleEnvironmentInteractRequest(EnvironmentInteractRequest request)
        {
        }

        public void Clear()
        {
            _cts.Cancel();
            _commandQueue.Clear();
        }

        private void SceneItemWriter()
        {
            Reader<SceneItemInfo>.read = ReadSceneItem;
            Writer<SceneItemInfo>.write = WriteSceneItem;
        }

        private SceneItemInfo ReadSceneItem(NetworkReader reader)
        {
            var sceneItemId = reader.ReadInt();
            var health = reader.ReadFloat();
            var maxHealth = reader.ReadFloat();
            var attackInterval = reader.ReadFloat();
            var attackRange = reader.ReadFloat();
            var attackDamage = reader.ReadFloat();
            var defence = reader.ReadFloat();
            var speed = reader.ReadFloat();
            return new SceneItemInfo
            {
                sceneItemId = (uint) sceneItemId,
                health = health,
                maxHealth = maxHealth,
                attackInterval = attackInterval,
                attackRange = attackRange,
                attackDamage = attackDamage,
                defense = defence,
                speed = speed
            };
        }

        private void WriteSceneItem(NetworkWriter writer, SceneItemInfo info)
        {
            writer.WriteUInt(info.sceneItemId);
            writer.WriteFloat(info.health);
            writer.WriteFloat(info.maxHealth);
            writer.WriteFloat(info.attackInterval);
            writer.WriteFloat(info.attackRange);
            writer.WriteFloat(info.attackDamage);
            writer.WriteFloat(info.defense);
            writer.WriteFloat(info.speed);
        }
    }

    [Serializable]
    public struct SceneItemInfo : IEqualityComparer<SceneItemInfo>
    {
        public uint sceneItemId;
        public float health;
        public float maxHealth;
        public float attackInterval;
        public float attackRange;
        public float attackDamage; 
        public float defense;
        public float speed;
        public float criticalRate;
        public float criticalDamageRatio;
        public CompressedVector3 Position;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SceneItemInfo: {sceneItemId}");
            sb.AppendLine($"Health: {health}/{maxHealth}");
            sb.AppendLine($"AttackInterval: {attackInterval}");
            sb.AppendLine($"AttackRange: {attackRange}");
            sb.AppendLine($"AttackDamage: {attackDamage}");
            sb.AppendLine($"Defense: {defense}");
            sb.AppendLine($"Speed: {speed}");
            sb.AppendLine($"Position: {Position}");
            sb.AppendLine($"CriticalRate: {criticalRate}");
            sb.AppendLine($"CriticalDamageRatio: {criticalDamageRatio}");
            return sb.ToString();
        }

        public bool Equals(SceneItemInfo x, SceneItemInfo y)
        {
            return x.sceneItemId == y.sceneItemId && x.health.Equals(y.health) && x.maxHealth.Equals(y.maxHealth) 
                   && x.attackInterval.Equals(y.attackInterval) && x.attackRange.Equals(y.attackRange) && x.attackDamage.Equals(y.attackDamage) 
                   && x.defense.Equals(y.defense) && x.speed.Equals(y.speed) && x.Position.Equals(y.Position) && x.criticalRate.Equals(y.criticalRate) && x.criticalDamageRatio.Equals(y.criticalDamageRatio);
        }

        public int GetHashCode(SceneItemInfo obj)
        {
            var hashCode = new HashCode();
            hashCode.Add(obj.sceneItemId);
            hashCode.Add(obj.health);
            hashCode.Add(obj.maxHealth);
            hashCode.Add(obj.attackInterval);
            hashCode.Add(obj.attackRange);
            hashCode.Add(obj.attackDamage);
            hashCode.Add(obj.defense);
            hashCode.Add(obj.speed);
            hashCode.Add(obj.Position);
            hashCode.Add(obj.criticalRate);
            hashCode.Add(obj.criticalDamageRatio);
            return hashCode.ToHashCode();
        }

        public SceneItemInfo UpdateItemInfo(PropertyTypeEnum propertyType, bool isMax, float value)
        {
            var info = this;
            switch (propertyType)
            {
                case PropertyTypeEnum.Health:
                    if (isMax)
                    {
                        info.maxHealth += value;
                        break;
                    }
                    health += value;
                    break;
                case PropertyTypeEnum.Defense:
                    info.attackInterval += value;
                    break;
                case PropertyTypeEnum.Speed:
                    info.speed += value;
                    break;
                case PropertyTypeEnum.AttackSpeed:
                    info.attackInterval += value;
                    break;
                case PropertyTypeEnum.AttackRadius:
                    info.attackRange += value;
                    break;
                case PropertyTypeEnum.Attack:
                    info.attackDamage += value;
                    break;
                case PropertyTypeEnum.CriticalRate:
                    info.criticalRate += value;
                    break;
                case PropertyTypeEnum.CriticalDamageRatio:
                    info.criticalDamageRatio += value;
                    break;
                default:
                    Debug.Log($"Property type {propertyType} is not supported to modify scene item info");
                    break;
            }

            return info;
        }
    }

    public enum CollectBehaviorType
    {
        Aggressive = 1,
        Movable,
        Hidden
    }
}