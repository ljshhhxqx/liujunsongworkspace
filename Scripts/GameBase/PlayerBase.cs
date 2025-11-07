using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Game.Map;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.GameBase
{
    public class PlayerBase : NetworkBehaviour
    {
        [SerializeField]
        private Collider baseCollider;
        public uint PlayerId { get; set; }

        private void Start()
        {
            var colliderData = GamePhysicsSystem.CreateColliderConfig(baseCollider);
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, colliderData, ObjectType.Base, gameObject.layer);
        }

        private void OnDestroy()
        {
            GameObjectContainer.Instance.RemoveDynamicObject(netId);
        }
    }
}
