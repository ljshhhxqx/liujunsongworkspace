using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Map;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects.Move
{
    public class MoveCollectItem : CollectBehaviour, IPoolable
    {
        [SyncVar]
        private MoveInfo _moveInfo;
        private MovementConfigLink _movementConfigLink;
        private Transform _cachedTransform;
        private HashSet<GameObjectData> _collectedItems = new HashSet<GameObjectData>();
        private Func<Vector3, bool> _checkInsideMap;
        private Func<Vector3, IColliderConfig, bool> _checkObstacle;

        private void FixedUpdate()
        {
            if (!ServerHandler || !IsMoveable)
            {
                return;
            }
            _movementConfigLink.ItemMovement.UpdateMovement(Time.fixedDeltaTime);
        }

        protected override void OnInitialize()
        {
            _checkInsideMap = MapBoundDefiner.Instance.IsWithinMapBounds;
            _checkObstacle = GameObjectContainer.Instance.IsIntersect;
        }

        public void OnSelfSpawn()
        {
            _movementConfigLink.ItemMovement?.ResetMovement();
        }

        public void Init(MoveInfo moveInfo, bool serverHandler, uint id)
        {
            _movementConfigLink ??= new MovementConfigLink();
            _moveInfo = moveInfo;
            NetId = id;
            ServerHandler = serverHandler;
            var config = JsonUtility.FromJson<IMovementConfig>(moveInfo.movementConfig);
            switch (config)
            {
                case BouncingMovementConfig bouncingMovementConfig:
                    _movementConfigLink.MovementConfig = bouncingMovementConfig;
                    _movementConfigLink.ItemMovement = new BouncingMovement(bouncingMovementConfig);
                    break;
                case EvasiveMovementConfig evasiveMovementConfig:
                    _movementConfigLink.MovementConfig = evasiveMovementConfig;
                    _movementConfigLink.ItemMovement = new EvasiveMovement(evasiveMovementConfig);
                    break;
                case PeriodicMovementConfig periodicMovementConfig:
                    _movementConfigLink.MovementConfig = periodicMovementConfig;
                    _movementConfigLink.ItemMovement = new PeriodicMovement(periodicMovementConfig);
                    break;
                default:
                    Debug.LogError($"{nameof(moveInfo.movementConfig)} is not supported");
                    break;
            }
            _movementConfigLink.ItemMovement.Initialize(transform, ColliderConfig, _checkInsideMap, _checkObstacle);
        }

        public void OnSelfDespawn()
        {
            
        }
    }
}