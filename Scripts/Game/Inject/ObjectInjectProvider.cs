using System.Collections.Generic;
using AOTScripts.Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Game.Inject
{
    public class ObjectInjectProvider : SingletonAutoMono<ObjectInjectProvider>
    {
        private IObjectInjector _injector;
        
        [Inject]
        private void Init(IObjectInjector injector)
        {
            _injector = injector;
        }

        public void Inject<T>(T target)
        {
            _injector.Inject(target);
        }

        public void InjectWithChildrenWithNoMap(GameObject target)
        {
            _injector.InjectWithChildren(target);
        }
        
        public T Resolve<T>()
        {
            return _injector.Resolve<T>();
        }
        
        public object Resolve(System.Type type)
        {
            return _injector.Resolve(type);
        }

        public void InjectMap(MapType mapType, Object target)
        {
            _injector.InjectMapElement(mapType, target);
        }
        public void InjectMap(MapType mapType, object target)
        {
            _injector.InjectMapElement(mapType, target);
        }

        public void InjectMapGameObject(MapType mapType, GameObject target)
        {
            _injector.InjectMapElementWithChildren(mapType, target);
        }
    }
}