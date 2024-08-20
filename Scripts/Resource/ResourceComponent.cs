using UnityEngine;

namespace Resource
{
    public class ResourceComponent : MonoBehaviour
    {
        private ResourceData _resourceData;
        public ResourceData ResourceData 
        { 
            get => _resourceData;
            set => _resourceData ??= value;
        } 
        
        //[Button]
    }
}