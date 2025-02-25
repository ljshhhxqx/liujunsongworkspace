using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.InteractSystem
{
    public class InteractSystem : MonoBehaviour
    {
        private GameSyncManager _gameSyncManager;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private PlayerInGameManager _playerInGameManager;
        private readonly Queue<IInteractRequest> _commandQueue = new Queue<IInteractRequest>();

        [Inject]
        private void Init(PlayerInGameManager playerInGameManager)
        {
            _playerInGameManager = playerInGameManager;
            _gameSyncManager = FindObjectOfType<GameSyncManager>();
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
        }

        [Command]
        public void EnqueueCommand(IInteractRequest command)
        {
            _commandQueue.Enqueue(command);
        }

        [Server]
        public void ProcessCommands()
        {
            while (_commandQueue.Count > 0)
            {
                var command = _commandQueue.Dequeue();
                if (!command.IsValid())
                {
                    continue;
                }
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
                }
            }
        }

        private void HandleSceneInteractRequest(SceneInteractRequest request)
        {
            var header = request.GetHeader();
            var playerNetId = _playerInGameManager.GetPlayerNetId(header.RequestConnectionId);
            switch (request.InteractionType)
            {
                case InteractionType.PickupItem:
                    _itemsSpawnerManager.PickerPickupItem(request.SceneItemId, playerNetId);
                    break;
                case InteractionType.PickupChest:
                    _itemsSpawnerManager.PickerPickUpChest(request.SceneItemId, playerNetId);
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
    }
}