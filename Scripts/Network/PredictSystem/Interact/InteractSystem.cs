using System;
using System.Collections.Concurrent;
using System.Threading;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.Server.InGame;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.Interact
{
    public class InteractSystem : NetworkBehaviour
    {
        private ItemsSpawnerManager _itemsSpawnerManager;
        private PlayerInGameManager _playerInGameManager;
        private readonly ConcurrentQueue<IInteractRequest> _commandQueue = new ConcurrentQueue<IInteractRequest>();
        private CancellationTokenSource _cts;

        [Inject]
        private void Init(PlayerInGameManager playerInGameManager)
        {
            _playerInGameManager = playerInGameManager;
            //_gameSyncManager = FindObjectOfType<GameSyncManager>();
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            if (isServer)
            {
                _cts = new CancellationTokenSource();
                UpdateInteractRequests(_cts.Token).Forget();
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
                        
                    }
                }
            }
        }

        private void HandlePlayerToSceneRequest(PlayerToSceneRequest playerInteractRequest)
        {
            
        }

        [Command]
        public void EnqueueCommand(byte[] commandBytes)
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

        private void HandleSceneInteractRequest(SceneInteractRequest request)
        {
            var header = request.GetHeader();
            var playerNetId = _playerInGameManager.GetPlayerNetId(header.RequestConnectionId);
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

        private void HandlePlayerInteractRequest(Interact.PlayerInteractRequest request)
        {
        }

        private void HandleEnvironmentInteractRequest(Interact.EnvironmentInteractRequest request)
        {
        }
    }
}