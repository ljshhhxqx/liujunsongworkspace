using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Tool.ReactiveProperty;
using UnityEngine;

namespace HotUpdate.Scripts.UI.UIs.UIFollow
{
    public interface IUIController
    {
        void BindToModel(IUIDataModel model);
        void UnBindFromModel(IUIDataModel model);
    }
    
    public interface IUIDataModel
    {
        
    }

    public abstract class ModularUIFollower : MonoBehaviour
    {
        [Header("基础配置")]
        [SerializeField]
        protected UIFollowConfig config;
    
        protected UIFollowInstance UIFollowInstance;
        protected Dictionary<Type, IUIController> UIControllers = new Dictionary<Type, IUIController>();
        protected Dictionary<Type, IUIDataModel> UIDataModels = new Dictionary<Type, IUIDataModel>();
        public HReactiveProperty<uint> SceneId { get; } = new HReactiveProperty<uint>();

        private void Start()
        {
            ObjectInjectProvider.Instance.Inject(this);
            UIFollowInstance = UIFollowSystem.CreateFollowUI(gameObject, config);
            if (!UIFollowInstance)
            {
                Debug.LogError($"Failed to create UI instance for {gameObject.name}");
                return;
            }
            InitializeControllers();
            BindControllersToModels();
            UIFollowInstance.Initialize(transform, config);
        }

        protected abstract void BindControllersToModels();

        protected virtual void InitializeControllers()
        {
            if (!UIFollowInstance || !UIFollowInstance.WorldCanvas) return;
        
            // 获取所有IUIController组件
            var allControllers = UIFollowInstance.WorldCanvas.GetComponentsInChildren<IUIController>(true);
        
            foreach (var controller in allControllers)
            {
                var controllerType = controller.GetType();
                UIControllers.TryAdd(controllerType, controller);
            }
        }
        
        protected virtual void OnDestroy()
        {
            UIFollowInstance?.Dispose();
        
            // 清理所有订阅
            foreach (var controller in UIControllers.Values)
            {
                var type = controller.GetType();
                if (UIDataModels.TryGetValue(type, out var model))
                {
                    controller.UnBindFromModel(model);
                }
            }
        }
        
        protected T GetController<T>() where T : class, IUIController
        {
            var type = typeof(T);
            if (UIControllers.TryGetValue(type, out var controller))
            {
                return controller as T;
            }
            return null;
        }
        protected void UnsubscribeController(IUIController controller)
        {
            // 取消订阅所有模型
            foreach (var model in UIDataModels.Values)
            {
                controller.UnBindFromModel(model);
            }
        }
    }
}