using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.Coroutine;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Data;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Game.Inject
{
    public class DependencyInjectionManager : IObjectInjector
    {
        [Inject] private IObjectResolver _objectResolver;
        private readonly Dictionary<MapType, LifetimeScope> _injectors = new Dictionary<MapType, LifetimeScope>();

        public void Inject(Object target)
        {
            // VContainer依赖注入逻辑
            _objectResolver.Inject(target);
        }
        
        public void Inject<T>(T target)
        {
            // VContainer依赖注入逻辑
            _objectResolver.Inject(target);
        }

        public void InjectWithChildren(GameObject root)
        {
            _objectResolver.InjectGameObject(root);
        }

        public void DelayInject(Object target, float delay)
        {
            DelayInvoker.DelayInvoke(delay, () => Inject(target));
        }

        public void DelayInjectWithChildren(GameObject root, float delay)
        {
            DelayInvoker.DelayInvoke(delay, () => InjectWithChildren(root));
        }

        public T Resolve<T>()
        {
            return _objectResolver.Resolve<T>();
        }

        public object Resolve(Type type)
        {
            return _objectResolver.Resolve(type);
        }

        public bool TryResolve<T>(out T instance)
        {
            var type = typeof(T);
            if (_objectResolver.TryGetRegistration(type, out var registration))
            {
                instance = (T) _objectResolver.Resolve(type);
                return true;
            }

            instance = default;
            return false;
        }

        public bool TryResolve(Type type, out object instance)
        {
            if (_objectResolver.TryGetRegistration(type, out var registration))
            {
                instance = _objectResolver.Resolve(type);
                return true;
            }

            instance = default;
            return false;
        }

        private LifetimeScope GetLifetimeScope(MapType mapType)
        {
            if (!Enum.IsDefined(typeof(MapType), mapType))
            {
                return null;
            }
            if (_injectors.TryGetValue(mapType, out var lifetimeScope) && lifetimeScope)
            {
                return lifetimeScope;
            }

            var lifeScopes = Object.FindObjectsByType<LifetimeScope>(FindObjectsSortMode.None);
            foreach (var scope in lifeScopes)
            {
                if (scope is IMapLifeScope mapLifeScope && mapLifeScope.GetMapType() == mapType)
                {
                    _injectors.AddOrUpdate(mapType, scope);
                    return scope;
                }
            }

            return null;
        }

        public void InjectMapElement<T>(MapType mapType, T target)
        {
            var lifeScope = GetLifetimeScope(mapType);
            if (lifeScope)
            {
                lifeScope.Container.Inject(target);
                return;
            }
            Debug.LogError("LifetimeScope not found for mapType: " + mapType);
        }

        public void InjectMapElementWithChildren(MapType mapType, GameObject target)
        {
            var lifeScope = GetLifetimeScope(mapType);
            if (lifeScope)
            {
                lifeScope.Container.InjectGameObject(target);
                return; 
            }
            Debug.LogError("LifetimeScope not found for mapType: " + mapType);
        }
    }

    interface IObjectInjector
    {
        void Inject<T>(T target);
        void InjectWithChildren(GameObject root);
        void DelayInject(Object target, float delay = 0.1f);
        void DelayInjectWithChildren(GameObject root, float delay = 0.1f);
        T Resolve<T>();
        object Resolve(Type type);
        bool TryResolve<T>(out T instance);
        bool TryResolve(Type type, out object instance);
        void InjectMapElement<T>(MapType mapType, T target);
        void InjectMapElementWithChildren(MapType mapType, GameObject target);
    }
}