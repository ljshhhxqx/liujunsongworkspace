using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class ItemBullet : NetworkAutoInjectHandlerBehaviour
    {
        private IColliderConfig _colliderConfig;
        private HashSet<DynamicObjectData> _hitObjects = new HashSet<DynamicObjectData>();
        private bool _isHandle;
        
        protected override void Start()
        {
            base.Start();
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Bullet, gameObject.layer, gameObject.tag);
        }
        
        private void FixedUpdate()
        {
            if (!ServerHandler || _isHandle || GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, _colliderConfig, _hitObjects))
            {
                return;
            }

            foreach (var hitObject in _hitObjects)
            {
                if (hitObject.Type == ObjectType.Player || hitObject.Type == ObjectType.Collectable && !_isHandle)
                {
                    _isHandle = true;
                }
            }

        }
    }
}