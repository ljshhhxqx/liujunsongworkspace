using AOTScripts.Tool.Coroutine;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using Mirror;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector.Collects
{
    public abstract class CollectBehaviour : NetworkAutoInjectHandlerBehaviour
    {
        protected Color OriginalColor;
        protected SceneItemInfo SceneItemInfo;
        protected Renderer Renderer;
        protected IColliderConfig ColliderConfig;
        protected GameConfigData GameConfigData;
        protected CollectObjectController CollectObjectController;
        protected InteractSystem InteractSystem;
        protected bool IsDead;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            Renderer = GetComponent<Renderer>();
            OriginalColor = Renderer.material.color;
            CollectObjectController = GetComponent<CollectObjectController>();
            var jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            GameConfigData = jsonConfig.GameConfig;
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            InteractSystem = FindObjectOfType<InteractSystem>();
            InteractSystem.SceneItemInfoChanged += OnSceneItemInfoChanged;
            OnInitialize();
        }

        protected virtual void OnSceneItemInfoChanged(uint id, SceneItemInfo info)
        {
            if (id != netId)
            {
                return;
            }
            SceneItemInfo = info;
            if (SceneItemInfo.health <= 0)
            {
                IsDead = true;
                Renderer.material.color = Color.red;
                DelayInvoker.DelayInvoke(1.9f, RpcOnDeath);
            }
        }

        [ClientRpc]
        protected virtual void RpcOnDeath()
        {
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Explode, transform.position,transform);
            
            var request = new SceneToPlayerInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer,
                    transform.position),
                InteractionType = InteractionType.ItemExplode,
                SceneItemId = netId,
            };
            InteractSystem.EnqueueCommand(request);
            NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
        }

        protected abstract void OnInitialize();
    }
}