using Sirenix.OdinInspector;
using UnityEngine;

namespace HotUpdate.Scripts.Game.GamePlay
{
    public class ModifyCollider : MonoBehaviour
    {
        private BoxCollider _collider;

        [Button("Modify Collider")]
        private void Modify(Vector3 ratio)
        {
            if (_collider == null)
            {
                _collider = GetComponent<BoxCollider>();
            }
            _collider.size = new Vector3(_collider.size.x * ratio.x, _collider.size.y * ratio.y, _collider.size.z * ratio.z);
        }
    }
}