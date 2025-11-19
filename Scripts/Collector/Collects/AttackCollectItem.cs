using System.Collections.Generic;
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
        
        private float _nextAttackTime;
        private readonly HashSet<DynamicObjectData> _collectedObjects = new HashSet<DynamicObjectData>();

        private void FixedUpdate()
        {
            if(IsDead || !ServerHandler || !IsAttackable || Time.time < _nextAttackTime) return;

            GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, ColliderConfig,
                _collectedObjects, OnInteract);
        }

        private bool OnInteract(DynamicObjectData target)
        {
            if (target.Type == ObjectType.Collectable || target.Type == ObjectType.Player)
            {
                var direction = (target.Position - transform.position).normalized;
                Attack(direction, target.NetId);
                _nextAttackTime = Time.time + _attackInfo.attackCooldown;
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
                    Spawner = netId,
                    CriticalRate = _attackInfo.criticalRate,
                    CriticalDamageRatio = _attackInfo.criticalDamage,
                };
                InteractSystem.EnqueueCommand(bullet);
                return;
            }
            var request = new SceneItemAttackInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer,
                    transform.position),
                InteractionType = InteractionType.ItemAttack,
                SceneItemId = netId,
                TargetId = targetNetId,
            };
            InteractSystem.EnqueueCommand(request);
        }
        
        public void Init(AttackInfo info)
        {
            _attackInfo = info;
            _nextAttackTime = Time.time;
            if (ServerHandler)
            {
                GameEventManager.Publish(new ItemSpawnedEvent(netId, transform.position, new SceneItemInfo
                {
                    health = _attackInfo.health,
                    attackDamage = _attackInfo.damage,
                    defense = _attackInfo.defense,
                    sceneItemId = netId,
                    attackRange = _attackInfo.attackRange,
                    attackInterval = _attackInfo.attackCooldown,
                    maxHealth = _attackInfo.health,
                }));
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
            _nextAttackTime = 0;
            _attackInfo = default;
            _collectedObjects.Clear();
        }
    }
}