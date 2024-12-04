using UnityEngine;

namespace HotUpdate.Scripts.Collector
{
    public class CollectCollider : MonoBehaviour
    {
        private const float Size = 1.5f;
        
        void OnValidate()
        {
            // 获取父物体的Collider
            Collider parentCollider = GetComponentInParent<Collider>();
            if (parentCollider != null && !parentCollider.isTrigger)
            {
                // 创建与父物体相同类型的Collider
                Collider newCollider = gameObject.AddComponent(parentCollider.GetType()) as Collider;
                
                // 设置新Collider为Trigger
                if (newCollider != null)
                {
                    newCollider.isTrigger = true;

                    // 复制父物体的中心点
                    newCollider.transform.position = parentCollider.transform.position;

                    // 设置新Collider的尺寸，默认1.5倍父物体Collider的大小
                    if (newCollider is BoxCollider)
                    {
                        BoxCollider boxCollider = newCollider as BoxCollider;
                        boxCollider.size = parentCollider.bounds.size * Size; // 自定义尺寸
                    }
                    else if (newCollider is SphereCollider)
                    {
                        SphereCollider sphereCollider = newCollider as SphereCollider;
                        sphereCollider.radius = parentCollider.bounds.extents.magnitude * Size; // 自定义尺寸
                    }
                }

                // 其他Collider类型可以根据需要添加
            }
        }
    }
}
