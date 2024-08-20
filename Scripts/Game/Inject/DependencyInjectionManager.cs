using System;
using Tool.Coroutine;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

namespace Common
{
    public class DependencyInjectionManager : IObjectInjector
    {
        [Inject] private IObjectResolver _objectResolver;

        public void Inject(Object target)
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
    }

    interface IObjectInjector
    {
        void Inject(Object target);
        void InjectWithChildren(GameObject root);
        void DelayInject(Object target, float delay = 0.1f);
        void DelayInjectWithChildren(GameObject root, float delay = 0.1f);
        T Resolve<T>();
        object Resolve(Type type);
        bool TryResolve<T>(out T instance);
        bool TryResolve(Type type, out object instance);
    }
}