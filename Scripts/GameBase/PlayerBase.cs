using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.GameBase
{
    public class PlayerBase : MonoBehaviour
    {
        [SerializeField]
        private Collider baseCollider;
        public uint PlayerId { get; set; }
    }
}
