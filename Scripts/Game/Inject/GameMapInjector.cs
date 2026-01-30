using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;
using VContainer.Unity;

namespace HotUpdate.Scripts.Game.Inject
{
    public class GameMapInjector : IInjector
    {
        private readonly IObjectResolver _mainResolver;
        private readonly LifetimeScope _mapScope;
        public MapType MapType { get; private set; }

        [Inject]
        public GameMapInjector(LifetimeScope mapScope)
        {
            _mapScope = mapScope;
            MapType = (MapType)Enum.Parse(typeof(MapType), SceneManager.GetActiveScene().name);
            // 获取父容器（主容器）
            _mainResolver = mapScope.Parent.Container;
        }

        public void Inject<T>(T objectToInject, bool includeMainScope = true, bool includeMapScope = true)
        {
            try
            {
                if (includeMainScope && _mainResolver != null)
                {
                    _mainResolver.Inject(objectToInject);
                }
        
                if (includeMapScope)
                {
                    _mapScope.Container.Inject(objectToInject);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to inject dependencies for {objectToInject.GetType().Name}: {e}");
                throw;
            }
        }

        public void InjectGameObject(GameObject gameObject, bool includeMainScope = true, bool includeMapScope = true)
        {
            try
            {
                if (includeMainScope && _mainResolver != null)
                {
                    _mainResolver.InjectGameObject(gameObject);
                }
        
                if (includeMapScope)
                {
                    _mapScope.Container.InjectGameObject(gameObject);
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to inject dependencies for {gameObject.name}: {e}");
                throw;
            }
        }
    }
}