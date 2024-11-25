using System;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HotUpdate.Scripts.Game.Inject
{
    public class GameMapInjector
    {
        private readonly IObjectResolver _mainResolver;
        private readonly LifetimeScope _mapScope;

        [Inject]
        public GameMapInjector(LifetimeScope mapScope)
        {
            _mapScope = mapScope;
            // 获取父容器（主容器）
            _mainResolver = mapScope.Parent.Container;
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