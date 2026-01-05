using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class Well : NetworkAutoInjectHandlerBehaviour
    {
        private IColliderConfig _colliderConfig;
        private GameEventManager _gameEventManager;
        private GameSyncManager _gameSyncManager;
        protected override bool AutoInjectLocalPlayer => false;
        
        [Inject]
        private void Init(GameEventManager gameEventManager, IObjectResolver objectResolver)
        {
            _gameEventManager = gameEventManager;
            _gameSyncManager = objectResolver.Resolve<GameSyncManager>();
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Well, gameObject.layer, gameObject.tag);
            
            _gameEventManager.Subscribe<PlayerTouchWellEvent>(OnPlayerTouchWell);
        }

        private void OnPlayerTouchWell(PlayerTouchWellEvent playerTouchWellEvent)
        {
            var playerConnectionId = PlayerInGameManager.Instance.GetPlayerId(playerTouchWellEvent.PlayerId);
            var takeTrainCommand = new PlayerTouchObjectCommand();
            takeTrainCommand.Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Property);
            takeTrainCommand.ObjectType = ObjectType.Well;
            _gameSyncManager.EnqueueServerCommand(takeTrainCommand);
        }
    }
}