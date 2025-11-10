using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector.Collects
{
    public abstract class CollectBehaviour : NetworkAutoInjectHandlerBehaviour
    {
        protected IColliderConfig ColliderConfig;
        protected GameConfigData GameConfigData;
        protected CollectObjectController CollectObjectController;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            CollectObjectController = GetComponent<CollectObjectController>();
            var jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            GameConfigData = jsonConfig.GameConfig;
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            OnInitialize();
        }
        
        protected abstract void OnInitialize();
    }
}