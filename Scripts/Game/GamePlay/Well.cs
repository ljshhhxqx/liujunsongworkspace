using System.Collections.Generic;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class Well : NetworkAutoInjectHandlerBehaviour
    {
        private GameEventManager _gameEventManager;
        private IColliderConfig _colliderConfig;
        private HashSet<DynamicObjectData> _dynamicObjects = new HashSet<DynamicObjectData>();
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Well, gameObject.layer, gameObject.tag);
        }

        private void FixedUpdate()
        {
            if (!ServerHandler)
            {
                return;
            }
            
            GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, _colliderConfig, _dynamicObjects, OnDynamicObjectIntersects);
        }

        private bool OnDynamicObjectIntersects(DynamicObjectData dynamicObjectData)
        {
            if (dynamicObjectData.Type == ObjectType.Player)
            {
                _gameEventManager.Publish(new PlayerTouchWellEvent(dynamicObjectData.NetId));
            }

            return false;
        }
    }
}