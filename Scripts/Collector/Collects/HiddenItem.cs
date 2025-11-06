using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class HiddenItem : CollectObjectController
    {
        private Renderer itemRenderer;
        private Collider itemCollider;
        private bool isHidden = false;
    
        void Start()
        {
            itemRenderer = GetComponent<Renderer>();
            itemCollider = GetComponent<Collider>();
        
            // 随机决定是否隐藏
            if(Random.Range(0, 100) < 30) // 30%概率隐藏
            {
                HideItem();
            }
        }
    
        public void HideItem()
        {
            isHidden = true;
            itemRenderer.enabled = false;
            // Collider保持启用，仍然可以交互
        }
    
        public void RevealItem()
        {
            isHidden = false;
            itemRenderer.enabled = true;
        }
    }
}