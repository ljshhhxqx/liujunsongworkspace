using System.Collections.Generic;
using HotUpdate.Scripts.Collector.Effect;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class AttackCollectItem : CollectBehaviour, IPoolable
    {
        [SyncVar]
        private AttackInfo _attackInfo;
        private CollectEffectController _collectEffectController;
        private float _lastAttackTime;
        private readonly HashSet<DynamicObjectData> _collectedObjects = new HashSet<DynamicObjectData>();
        private AttackMainEffect _attackMainEffect;

        private void FixedUpdate()
        {
            if (IsDead || !ServerHandler || !IsAttackable || Time.time < _lastAttackTime + _attackInfo.attackCooldown)
            {
                //Debug.LogError($"{name} is dead or not attackable or attack cooldown not over yet");
                return;
            }
            

            GameObjectContainer.Instance.DynamicObjectIntersects(NetId, transform.position, ColliderConfig,
                _collectedObjects, OnInteract);
        }

        private bool OnInteract(DynamicObjectData target)
        {
            if (target.Type == ObjectType.Player)
            {
                _lastAttackTime = Time.time;
                var direction = (target.Position - transform.position).normalized;
                Attack(direction, target.NetId);
                return true;
            }
            return false;
        }

        private void Attack(Vector3 direction, uint targetNetId)
        {
            if (_attackInfo.isRemoteAttack)
            {
                var bullet = new SpawnBullet()
                {
                    Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer, transform.position),
                    InteractionType = InteractionType.Bullet,
                    Direction = direction,
                    AttackPower = _attackInfo.damage,
                    Speed = _attackInfo.speed,
                    LifeTime = _attackInfo.lifeTime,
                    StartPosition = transform.position,
                    Spawner = NetId,
                    CriticalRate = _attackInfo.criticalRate,
                    CriticalDamageRatio = _attackInfo.criticalDamage,
                };
                InteractSystem.EnqueueCommand(bullet);
                Debug.Log($"{NetId} is Remote Attack and send bullet to {targetNetId} - direction: {direction} - attackPower: {_attackInfo.damage} - speed: {_attackInfo.speed} - lifeTime: {_attackInfo.lifeTime} - criticalRate: {_attackInfo.criticalRate} - criticalDamage: {_attackInfo.criticalDamage}");
                return;
            }
            var request = new SceneItemAttackInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer,
                    transform.position),
                InteractionType = InteractionType.ItemAttack,
                SceneItemId = NetId,
                TargetId = targetNetId,
                AttackPower = _attackInfo.damage,
                CriticalRate = _attackInfo.criticalRate,
                CriticalDamage = _attackInfo.criticalDamage,
            };
            Debug.Log($"{NetId} is Local Attack and send request to {targetNetId} - attackPower: {_attackInfo.damage} - criticalRate: {_attackInfo.criticalRate} - criticalDamage: {_attackInfo.criticalDamage}");
            InteractSystem.EnqueueCommand(request);
        }
        
        public void Init(AttackInfo info, bool serverHandler, uint id, bool clientHandler, Transform player)
        {
            _attackInfo = info;
            NetId = id;
            ServerHandler = serverHandler;
            if (serverHandler)
            {
                GameEventManager.Publish(new SceneItemInfoChanged(NetId, transform.position, new SceneItemInfo
                {
                    health = _attackInfo.health,
                    attackDamage = _attackInfo.damage,
                    defense = _attackInfo.defense,
                    sceneItemId = NetId,
                    attackRange = _attackInfo.attackRange,
                    attackInterval = _attackInfo.attackCooldown,
                    maxHealth = _attackInfo.health,
                }));
            }
            if (clientHandler)
            {
                _attackMainEffect ??= GetComponentInChildren<AttackMainEffect>();
                GameEventManager.Publish(new SceneItemSpawnedEvent(NetId, gameObject, true, player));
                if (!TryGetComponent<CollectEffectController>(out _collectEffectController))
                {
                    _collectEffectController = _attackMainEffect.gameObject.AddComponent<CollectEffectController>();
                }
                _collectEffectController.SetAttackParameters(_attackInfo.damage, _attackInfo.attackCooldown);
            }
        }

        protected override void OnInitialize()
        {
        }

        public void OnSelfSpawn()
        {
            
        }

        public void OnSelfDespawn()
        {
            _lastAttackTime = 0;
            _attackInfo = default;
            _collectedObjects.Clear();
        }
    }
}