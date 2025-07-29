using System;
using System.Collections.Concurrent;
using System.Threading;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using MemoryPack;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.Interact
{
    public class InteractSystem : ServerNetworkComponent
    {
        private ItemsSpawnerManager _itemsSpawnerManager;
        private readonly ConcurrentQueue<IInteractRequest> _commandQueue = new ConcurrentQueue<IInteractRequest>();
        private CancellationTokenSource _cts = new CancellationTokenSource();

        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            //_gameSyncManager = FindObjectOfType<GameSyncManager>();
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            gameEventManager.Subscribe<GameStartEvent>(OnGameStart);
        }

        private void OnGameStart(GameStartEvent gameStartEvent)
        {
            Debug.Log($"InteractSystem start isClient-{isClient} isServer-{isServer} isLocalPlayer-{isLocalPlayer}");
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
                        
                    }
                }
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

        [Server]
        public void EnqueueCommand<T>(T request) where T : IInteractRequest
        {
            var header = request.GetHeader();
            var validCommand = request.CommandValidResult();
            if (!validCommand.IsValid)
            {
                Debug.LogError($"Invalid command: {header}");
                return;
            }
            _commandQueue.Enqueue(request);
        }

        [Command]
        public void CmdEnqueueCommand(byte[] commandBytes)
        {
            var command = MemoryPackSerializer.Deserialize<IInteractRequest>(commandBytes);
            var header = command.GetHeader();
            var validCommand = command.CommandValidResult();
            if (!validCommand.IsValid)
            {
                Debug.LogError($"Invalid command: {header}");
                return;
            }
            _commandQueue.Enqueue(command);
        }

        [Server]
        public void EnqueueServerCommand(IInteractRequest command)
        {
            var header = command.GetHeader();
            var validCommand = command.CommandValidResult();
            if (!validCommand.IsValid)
            {
                Debug.LogError($"Invalid command: {header}");
                return;
            }
            _commandQueue.Enqueue(command);
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
                default:
                    throw new ArgumentOutOfRangeException();
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
    }
}