using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using UnityEngine;
using VContainer;
using AnimationState = AOTScripts.Data.AnimationState;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class Well : NetworkAutoInjectHandlerBehaviour
    {
        private IColliderConfig _colliderConfig;
        private MapElementData _collectData;
        private GameEventManager _gameEventManager;
        private GameSyncManager _gameSyncManager;
        private IAnimationCooldown _animationCooldown;
        private PlayerInGameManager _playerInGameManager;
        protected override bool AutoInjectLocalPlayer => true;
        [SyncVar]
        private float _currentCd;
        [SyncVar]
        private float _currentCount;
        
        [Inject]
        private void Init(GameEventManager gameEventManager, IObjectResolver objectResolver, IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
            _gameEventManager = gameEventManager;
            _collectData = configProvider.GetConfig<JsonDataConfig>().CollectData.mapElementData;
            _animationCooldown = new AnimationCooldown(AnimationState.None, _collectData.wellCd, 1);
            _currentCount = _collectData.wellCount;
            _playerInGameManager = playerInGameManager;
            _gameSyncManager = objectResolver.Resolve<GameSyncManager>();
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Well, gameObject.layer, gameObject.tag);
            
            _gameEventManager.Subscribe<PlayerTouchWellEvent>(OnPlayerTouchWell);
        }

        public bool CanTouchWell()
        {
            return _currentCd <= 0 && _currentCount > 0;
        }

        private void FixedUpdate()
        {
            if (!ServerHandler || _animationCooldown.IsReady())
            {
                return;
            }
            _currentCd = _animationCooldown.Update(Time.fixedDeltaTime);
        }

        private void OnPlayerTouchWell(PlayerTouchWellEvent playerTouchWellEvent)
        {
            if (!ServerHandler || !_animationCooldown.IsReady())
            {
                return;
            }
            var playerConnectionId = _playerInGameManager.GetPlayerId(playerTouchWellEvent.PlayerId);
            var playerTouchObjectCommand = new PlayerTouchObjectCommand();
            playerTouchObjectCommand.Header = GameSyncManager.CreateNetworkCommandHeader(playerConnectionId, CommandType.Property);
            playerTouchObjectCommand.ObjectType = (int)ObjectType.Well;
            _gameSyncManager.EnqueueServerCommand(playerTouchObjectCommand);
            _animationCooldown.Use();
            _currentCount--;
            if (_currentCount <= 0)
            {
                NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
            }
        }
    }
}