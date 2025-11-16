using System.Collections.Generic;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class AttackCollectItem : CollectBehaviour, IPoolable
    {
        [SyncVar]
        private AttackInfo _attackInfo;
        
        private float _lastAttackTime;
        private readonly HashSet<DynamicObjectData> _collectedObjects = new HashSet<DynamicObjectData>();

        private void FixedUpdate()
        {
            if(IsDead || !ServerHandler || !IsAttackable) return;

            if (!GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, ColliderConfig, _collectedObjects))
            {
                return;
            }
            foreach (var target in _collectedObjects)
            {
                if(target.Type == ObjectType.Collectable || target.Type == ObjectType.Player)
                {
                    if (Time.time >= _lastAttackTime + _attackInfo.attackCooldown)
                    {
                        var direction = (target.Position - transform.position).normalized;
                        Attack(direction, target.NetId);
                        _lastAttackTime = Time.time;
                    }
                }
            }
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
            _lastAttackTime = Time.time;
            
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