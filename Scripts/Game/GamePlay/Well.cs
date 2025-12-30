using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using UnityEngine;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class Well : NetworkAutoInjectHandlerBehaviour
    {
        private IColliderConfig _colliderConfig;
        protected override bool AutoInjectLocalPlayer => false;
        
        private void Start()
        {
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Well, gameObject.layer, gameObject.tag);
        }
    }
}