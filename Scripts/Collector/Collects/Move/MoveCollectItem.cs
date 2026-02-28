using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Tool.Coroutine;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector.Collects.Move
{
    public class MoveCollectItem : CollectBehaviour, IPoolable
    {
        [SyncVar]
        public MoveInfo _moveInfo;
        private MovementConfigLink _movementConfigLink;
        private Transform _cachedTransform;
        private GameSyncManager _gameSyncManager;
        private HashSet<GameObjectData> _collectedItems = new HashSet<GameObjectData>();
        private Func<Vector3, bool> _checkInsideMap;
        private Func<Vector3, IColliderConfig, bool> _checkObstacle;


        public override void OnStartServer()
        {
            base.OnStartServer();
            Debug.Log($"[MoveItem] {name} Initialize");
            RepeatedTask.Instance.StartRepeatingTask(StartMove, Time.fixedDeltaTime);
        }
        
        private void StartMove()
        {
            if (_moveInfo == default || !ServerHandler || !IsMoveable || !_gameSyncManager)
            {
                return;
            }
            if (_gameSyncManager.isGameOver)
            {
                return;
            }
            _movementConfigLink.ItemMovement.UpdateMovement(Time.fixedDeltaTime);
        }

        protected override void OnInitialize()
        {
        }

        public void OnSelfSpawn()
        {
        }

        public void Init(MoveInfo moveInfo, bool serverHandler, uint id)
        {
            _movementConfigLink.ItemMovement?.ResetMovement();
            Debug.Log($"[MoveItem] {name} Initialize");
            _gameSyncManager ??= FindObjectOfType<GameSyncManager>();
            _checkInsideMap = MapBoundDefiner.Instance.IsWithinMapBounds;
            _checkObstacle = GameObjectContainer.Instance.IsIntersect;
            _movementConfigLink ??= new MovementConfigLink();
            NetId = id;
            ServerHandler = serverHandler;
            if (moveInfo.bouncingConfig != default)
            {
                _movementConfigLink.MovementConfig = moveInfo.bouncingConfig;
                _movementConfigLink.ItemMovement = new BouncingMovement(moveInfo.bouncingConfig);
            }
            else if (moveInfo.evasiveConfig != default)
            {
                _movementConfigLink.MovementConfig = moveInfo.evasiveConfig;
                _movementConfigLink.ItemMovement = new EvasiveMovement(moveInfo.evasiveConfig);
            }
            else
            {
                _movementConfigLink.MovementConfig = moveInfo.periodicConfig;
                _movementConfigLink.ItemMovement = new PeriodicMovement(moveInfo.periodicConfig);
            }
            _movementConfigLink.ItemMovement.Initialize(transform, ColliderConfig, _checkInsideMap, _checkObstacle);
            _moveInfo = moveInfo;
        }

        public void OnSelfDespawn()
        {
            
        }

        private void OnDestroy()
        {
            RepeatedTask.Instance.StopRepeatingTask(StartMove);
        }
    }
}