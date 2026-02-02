using AOTScripts.Data;
using AOTScripts.Tool.Coroutine;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Effect;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Tool.GameEvent;
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
        protected GameEventManager GameEventManager;
        protected uint NetId;
        [SyncVar] protected int CurrentControlSkillType;
        protected IObjectResolver ObjectResolver;
        protected override bool AutoInjectLocalPlayer => false;
        public SubjectedStateType CurrentSubjectedStateType => (SubjectedStateType)CurrentControlSkillType;

        protected float NowSpeed(float currentSpeed)
        {
            return currentSpeed * (CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsSlowdown)  ? 0.3f : 1f);
        }

        protected bool IsMoveable => !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsFrozen) && !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsStunned)
                                 && !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsStoned) && !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsBlowup);
        protected bool IsAttackable => !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsFrozen) && !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsStunned)
            && !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsStoned) && !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsBlowup) && !CurrentSubjectedStateType.HasAnyState(SubjectedStateType.IsBlinded);
        
        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, IObjectResolver objectResolver)
        {
            MaterialTransparencyControllers = GetComponentsInChildren<MaterialTransparencyController>();
            CollectObjectController = GetComponent<CollectObjectController>();
            var jsonConfig = configProvider.GetConfig<JsonDataConfig>();
            GameConfigData = jsonConfig.GameConfig;
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            InteractSystem = FindObjectOfType<InteractSystem>();
            InteractSystem.SceneItemInfoChanged += OnSceneItemInfoChanged;
            InteractSystem.ItemControlSkillChanged += OnItemControlSkillChanged;
            GameEventManager = gameEventManager;
            ObjectResolver = objectResolver;
            OnInitialize();
        }

        private void OnItemControlSkillChanged(uint id, float duration, ControlSkillType skillType)
        {
            if (id != NetId)
            {
                return;
            }
            CurrentControlSkillType = (int)skillType;
            DelayInvoker.DelayInvoke(duration, ReverseControlSkillType);
            RpcOnItemControlSkillChanged(duration, skillType);
        }

        private void ReverseControlSkillType()
        {
            CurrentControlSkillType = 0;
        }

        [ClientRpc]
        private void RpcOnItemControlSkillChanged(float duration, ControlSkillType skillType)
        {
            //todo：飘字+特效
            DelayInvoker.DelayInvoke(duration, StopControlSkillEffect);
            EffectPlayer.Instance.PlayEffect(ParticlesType.AttackDebuff, transform.position, transform);
            GameEventManager.Publish(new FollowTargetTextEvent(transform.position, EnumHeaderParser.GetHeader(skillType)));
        }

        private static void StopControlSkillEffect()
        {
            EffectPlayer.Instance.StopEffect(ParticlesType.AttackDebuff);
        }

        protected void SetColor(Color color)
        {
            OriginalColor = color;
            foreach (var materialTransparencyController in MaterialTransparencyControllers)
            {
                materialTransparencyController?.SetColor(color);
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
            if (id != NetId || !ServerHandler)
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
                    if (!gameObject.activeInHierarchy)
                    {
                        return;
                    }
                    var request = new ItemExplodeRequest
                    {
                        Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer, transform.position),
                        InteractionType = InteractionType.ItemExplode,
                        SceneItemId = NetId,
                        AttackPower = info.attackDamage,
                        Radius = explodeRange,
                    };
                    InteractSystem.EnqueueCommand(request);
                    NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
                    CollectObjectController.RpcOnDeath();
                });
            }
        }

        private void OnDisable()
        {
            foreach (var materialTransparencyController in MaterialTransparencyControllers)
            {
                if (materialTransparencyController.gameObject && materialTransparencyController.gameObject.activeInHierarchy)
                {
                    materialTransparencyController.SetColor(OriginalColor);
                    materialTransparencyController.SetEnabled(true);
                    materialTransparencyController.RestoreOriginalMaterials();
                }
            }
        }

        protected abstract void OnInitialize();
    }
}