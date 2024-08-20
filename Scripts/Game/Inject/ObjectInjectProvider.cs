using UnityEngine;
using VContainer;

namespace Common
{
    public class ObjectInjectProvider : SingletonAutoMono<ObjectInjectProvider>
    {
        private IObjectInjector _injector;
        
        [Inject]
        private void Init(IObjectInjector injector)
        {
            _injector = injector;
        }

        public void Inject(Object target)
        {
            _injector.Inject(target);
        }

        public void InjectWithChildren(GameObject target)
        {
            _injector.InjectWithChildren(target);
        }
    }
}