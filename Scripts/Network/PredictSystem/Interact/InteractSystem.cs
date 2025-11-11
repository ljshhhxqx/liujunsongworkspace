using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Collector.Collects;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using MemoryPack;
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
        private JsonDataConfig _jsonConfig;
        private GameSyncManager _gameSyncManager;
        private PlayerPropertySyncSystem _playerPropertySyncSystem;

        private SyncDictionary<uint, SceneItemInfo> _sceneItems = new SyncDictionary<uint, SceneItemInfo>();
        
        public event Action<uint, SceneItemInfo> SceneItemInfoChanged;
        
        [Inject]
        private void Init(GameEventManager gameEventManager, IConfigProvider configProvider)
        {
            //_gameSyncManager = FindObjectOfType<GameSyncManager>();
            SceneItemWriter();
            _jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            _gameSyncManager = FindObjectOfType<GameSyncManager>();
            _playerPropertySyncSystem = _gameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            gameEventManager.Subscribe<GameStartEvent>(OnGameStart);
        }

        private void OnGameStart(GameStartEvent gameStartEvent)
        {
            //Debug.Log($"InteractSystem start isClient-{isClient} isServer-{isServer} isLocalPlayer-{isLocalPlayer}");
            UpdateInteractRequests(_cts.Token).Forget();
            _bulletPrefab = ResourceManager.Instance.GetResource<GameObject>("Bullet");
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
                    }
                }
            }
        }

        private void HandleSceneItemAttackInteractRequest(SceneItemAttackInteractRequest sceneItemAttackInteractRequest)
        {
            if (!NetworkServer.spawned.TryGetValue(sceneItemAttackInteractRequest.SceneItemId, out var sceneObject)||
                NetworkServer.spawned.TryGetValue(sceneItemAttackInteractRequest.TargetId, out var targetSceneObject))
            {
                Debug.LogError($"Scene item {sceneItemAttackInteractRequest.SceneItemId} or target scene item {sceneItemAttackInteractRequest.TargetId} not found");
                return;
            }

            var attackPower = sceneItemAttackInteractRequest.AttackPower;
            var criticalRate = sceneItemAttackInteractRequest.CriticalRate;
            var criticalDamage = sceneItemAttackInteractRequest.CriticalDamage;
            float defense;

            if (_sceneItems.TryGetValue(sceneItemAttackInteractRequest.TargetId, out var sceneItemInfo))
            {
                if (sceneItemInfo.health <= 0)
                    return;
                defense = sceneItemInfo.defense;
                var damage = _jsonConfig.GetDamage(attackPower, defense, criticalRate, criticalDamage);
                Debug.Log($"Scene item {sceneItemAttackInteractRequest.SceneItemId} attack scene item {sceneItemAttackInteractRequest.TargetId} with damage {damage}");
                sceneItemInfo.health -= damage.Damage;
                if (sceneItemInfo.health <= 0)
                {
                    Debug.Log($"Scene item {sceneItemAttackInteractRequest.TargetId} is dead");
                    SceneItemInfoChanged?.Invoke(sceneItemAttackInteractRequest.TargetId, sceneItemInfo);
                    _sceneItems.Remove(sceneItemAttackInteractRequest.TargetId);
                }
            }
            else
            {
                var connectionId = PlayerInGameManager.Instance.GetPlayerId(sceneItemAttackInteractRequest.TargetId);
                var property = _playerPropertySyncSystem.GetPlayerProperty(connectionId);
                defense = property.GetValueOrDefault(PropertyTypeEnum.Defense).CurrentValue;
                var damage = _jsonConfig.GetDamage(attackPower, defense, criticalRate, criticalDamage);
                Debug.Log($"Scene item {sceneItemAttackInteractRequest.SceneItemId} attack player {sceneItemAttackInteractRequest.TargetId} with damage {damage}");
                var command = new PropertyItemAttackCommand
                {
                    TargetId = connectionId,
                    Header = GameSyncManager.CreateNetworkCommandHeader(0, CommandType.Property, CommandAuthority.Server),
                    Damage = damage.Damage,
                    IsCritical = damage.IsCritical,
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
            if (!NetworkServer.spawned.TryGetValue(sceneToSceneInteractRequest.SceneItemId, out var sceneObject)||
                NetworkServer.spawned.TryGetValue(sceneToSceneInteractRequest.TargetSceneItemId, out var targetSceneObject))
            {
                Debug.LogError($"Scene item {sceneToSceneInteractRequest.SceneItemId} or target scene item {sceneToSceneInteractRequest.TargetSceneItemId} not found");
                return;
            }
            var sceneData = _sceneItems[sceneToSceneInteractRequest.SceneItemId];
        }

        private void HandleSceneToPlayerInteractRequest(SceneToPlayerInteractRequest sceneToPlayerInteractRequest)
        {
            if (!NetworkServer.spawned.TryGetValue(sceneToPlayerInteractRequest.SceneItemId, out var sceneObject)||
                NetworkServer.spawned.TryGetValue(sceneToPlayerInteractRequest.TargetPlayerId, out var targetSceneObject))
            {
                Debug.LogError($"Scene item {sceneToPlayerInteractRequest.SceneItemId} or target scene item {sceneToPlayerInteractRequest.TargetPlayerId} not found");
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
            switch (request.InteractionType)
            {
                case InteractionType.PickupItem:
                    _itemsSpawnerManager.PickerPickupItem(playerNetId, request.SceneItemId);
                    break;
                case InteractionType.PickupChest:
                    _itemsSpawnerManager.PickerPickUpChest(playerNetId, request.SceneItemId);
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
    public struct SceneItemInfo
    {
        public uint sceneItemId;
        public float health;
        public float maxHealth;
        public float attackInterval;
        public float attackRange;
        public float attackDamage;
        [FormerlySerializedAs("defenSe")] [FormerlySerializedAs("defence")] public float defense;
        public float speed;
    }

    public enum CollectBehaviorType
    {
        Aggressive = 1,
        Movable,
        Hidden
    }
}