using System;
using AOTScripts.Tool.Coroutine;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Effect;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using Mirror;
using UnityEngine;
using VContainer;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Collector.Collects
{
    public abstract class CollectBehaviour : NetworkAutoInjectHandlerBehaviour
    {
        protected Color OriginalColor;
        protected SceneItemInfo SceneItemInfo;
        protected IColliderConfig ColliderConfig;
        protected GameConfigData GameConfigData;
        protected CollectObjectController CollectObjectController;
        protected InteractSystem InteractSystem;
        protected bool IsDead;
        protected MaterialTransparencyController[] MaterialTransparencyControllers;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            MaterialTransparencyControllers = GetComponentsInChildren<MaterialTransparencyController>();
            CollectObjectController = GetComponent<CollectObjectController>();
            var jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            GameConfigData = jsonConfig.GameConfig;
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            InteractSystem = FindObjectOfType<InteractSystem>();
            InteractSystem.SceneItemInfoChanged += OnSceneItemInfoChanged;
            OnInitialize();
        }

        protected void SetColor(Color color)
        {
            OriginalColor = color;
            foreach (var materialTransparencyController in MaterialTransparencyControllers)
            {
                materialTransparencyController.SetColor(color);
            }
        }
        
        protected void SetAlpha(float alpha)
        {
            foreach (var materialTransparencyController in MaterialTransparencyControllers)
            {
                materialTransparencyController.SetColor(alpha: alpha);
            }
        }
        
        protected void SetEnabled(bool isEnabled)
        {
            foreach (var materialTransparencyController in MaterialTransparencyControllers)
            {
                materialTransparencyController.SetEnabled(isEnabled);
            }
            
        }

        protected virtual void OnSceneItemInfoChanged(uint id, SceneItemInfo info)
        {
            if (id != netId || !ServerHandler)
            {
                return;
            }
            SceneItemInfo = info;
            if (SceneItemInfo.health <= 0)
            {
                IsDead = true;
                var explodeRange = Random.Range(1f, 2.5f);
                DelayInvoker.DelayInvoke(1.9f, () =>
                {
                    var request = new ItemExplodeRequest
                    {
                        Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer, transform.position),
                        InteractionType = InteractionType.ItemExplode,
                        SceneItemId = netId,
                        AttackPower = info.attackDamage,
                        Radius = explodeRange,
                    };
                    InteractSystem.EnqueueCommand(request);
                    RpcOnDeath();
                });
            }
        }

        private void OnDisable()
        {
            foreach (var materialTransparencyController in MaterialTransparencyControllers)
            {
                materialTransparencyController.SetColor(OriginalColor);
                materialTransparencyController.SetEnabled(true);
                materialTransparencyController.RestoreOriginalMaterials();
            }
        }

        [ClientRpc]
        protected virtual void RpcOnDeath()
        {
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Explode, transform.position, transform);
            EffectPlayer.Instance.PlayEffect(ParticlesType.Explode, transform.position, transform);
            NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
        }

        protected abstract void OnInitialize();
    }
}