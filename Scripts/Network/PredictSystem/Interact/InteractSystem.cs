using System;
using System.Collections.Concurrent;
using System.Text;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.Interact
{
    public class InteractSystem : NetworkHandlerBehaviour
    {
        private ItemsSpawnerManager _itemsSpawnerManager;
        private readonly ConcurrentQueue<IInteractRequest> _commandQueue = new ConcurrentQueue<IInteractRequest>();
        private CancellationTokenSource _cts = new CancellationTokenSource();

        private SyncDictionary<uint, SceneItemInfo> _sceneItems = new SyncDictionary<uint, SceneItemInfo>();
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            //_gameSyncManager = FindObjectOfType<GameSyncManager>();
            SceneItemWriter();
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            gameEventManager.Subscribe<GameStartEvent>(OnGameStart);
        }

        private void OnGameStart(GameStartEvent gameStartEvent)
        {
            //Debug.Log($"InteractSystem start isClient-{isClient} isServer-{isServer} isLocalPlayer-{isLocalPlayer}");
            UpdateInteractRequests(_cts.Token).Forget();
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
                    }
                }
            }
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
                defence = defence,
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
            writer.WriteFloat(info.defence);
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
        public float defence;
        public float speed;
    }

    public enum CollectBehaviorType
    {
        Aggressive = 1,
        Movable,
        Hidden
    }
}