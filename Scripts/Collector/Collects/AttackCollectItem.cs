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
        }
        
        [SerializeField] private float health = 3;
        [SerializeField] private float damage = 1;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float attackCooldown = 2f;
    
        private const string PlayerTag = "Player";
        private const string CollectableTag = "CollectItem";
        
        private float _lastAttackTime;
        private bool _isDead = false;
        private InteractSystem _interactSystem;
        private readonly HashSet<DynamicObjectData> _collectedObjects = new HashSet<DynamicObjectData>();

        private void FixedUpdate()
        {
            if(_isDead || !ServerHandler) return;

            if (!GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, ColliderConfig, _collectedObjects))
            {
                return;
            }
            foreach (var target in _collectedObjects)
            {
                if(target.Tag.Equals(PlayerTag) || target.Tag.Equals(CollectableTag))
                {
                    if (Time.time >= _lastAttackTime + attackCooldown)
                    {
                        Attack();
                        _lastAttackTime = Time.time;
                    }
                }
            }
        }
        
        private void Attack()
        {
            var request = new SceneToPlayerInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer,
                    transform.position),
                InteractionType = InteractionType.ItemAttack,
                SceneItemId = netId,
            };
            _interactSystem.EnqueueCommand(request);
        }
    
        private IEnumerator DeathSequence()
        {
            _isDead = true;
            // 死亡效果：闪烁、缩放等
            GetComponent<Renderer>().material.color = Color.red;
        
            yield return new WaitForSeconds(2f);
        
            // 自爆效果
            Explode();
        }
    
        private void Explode()
        {
            var request = new SceneToPlayerInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer,
                    transform.position),
                InteractionType = InteractionType.ItemExplode,
                SceneItemId = netId,
            };
            _interactSystem.EnqueueCommand(request);
        }
        
        public void InitInfo(AttackInfo info)
        {
            health = info.Health;
            damage = info.Damage;
            attackRange = info.AttackRange;
            attackCooldown = info.AttackCooldown;
        }

        protected override void OnInitialize()
        {
            _interactSystem = FindObjectOfType<InteractSystem>();
        }
    }
}