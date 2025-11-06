using HotUpdate.Scripts.Collector;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Game.Map
{
    public class GameStaticObject : MonoBehaviour
    {
        [SerializeField]
        private int id;
        public int Id => id;

        public Vector3 Position
        {
            get;
            private set;
        }

        public IColliderConfig ColliderConfig
        {
            get;
            private set;
        
        }

        public void ModifyId(int staticId)
        {
            id = staticId;
        }

        [Inject]
        public void Init()
        {
            Position = transform.position;
            var goCollider = GetComponent<Collider>();
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(goCollider);
        }
    }
}