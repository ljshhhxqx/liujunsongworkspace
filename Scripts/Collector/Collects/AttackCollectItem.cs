using System.Collections;
using System.Collections.Generic;
using AOTScripts.Data;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class AttackCollectItem : CollectBehaviour
    {
        public struct AttackInfo
        {
            public float Health;
            public float Damage;
            public float AttackRange;
            public float AttackCooldown;
            public bool IsRemoteAttack;
            public float Speed;
            public float LifeTime;
            public float CriticalRate;
            public float CriticalDamage;
        }
        
        private AttackInfo _attackInfo;
        
        private float _lastAttackTime;
        private readonly HashSet<DynamicObjectData> _collectedObjects = new HashSet<DynamicObjectData>();

        private void FixedUpdate()
        {
            if(IsDead || !ServerHandler) return;

            if (!GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, ColliderConfig, _collectedObjects))
            {
                return;
            }
            foreach (var target in _collectedObjects)
            {
                if(target.Type == ObjectType.Collectable || target.Type == ObjectType.Player)
                {
                    if (Time.time >= _lastAttackTime + _attackInfo.AttackCooldown)
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
            if (_attackInfo.IsRemoteAttack)
            {
                var bullet = new SpawnBullet()
                {
                    Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer, transform.position),
                    InteractionType = InteractionType.Bullet,
                    Direction = direction,
                    AttackPower = _attackInfo.Damage,
                    Speed = _attackInfo.Speed,
                    LifeTime = _attackInfo.LifeTime,
                    StartPosition = transform.position,
                    
                    Spawner = netId,
                    CriticalRate = _attackInfo.CriticalRate,
                    CriticalDamageRatio = _attackInfo.CriticalDamage,
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
    
        // private IEnumerator DeathSequence()
        // {
        //     IsDead = true;
        //     // 死亡效果：闪烁、缩放等
        //     GetComponent<Renderer>().material.color = Color.red;
        //
        //     yield return new WaitForSeconds(2f);
        //
        //     // 自爆效果
        //     Explode();
        // }
        
        public void Init(AttackInfo info)
        {
            _attackInfo = info;
            _lastAttackTime = Time.time;
            
        }

        protected override void OnInitialize()
        {
        }
    }
}