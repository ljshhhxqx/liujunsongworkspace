using System.Collections;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class AttackCollectItem : CollectObjectController
    {
        [SerializeField] private int health = 3;
        [SerializeField] private int damage = 1;
        [SerializeField] private float attackRange = 3f;
        [SerializeField] private float attackCooldown = 2f;
    
        private float _lastAttackTime;
        private bool _isDead = false;
    
        private readonly Collider[] _colliders = new Collider[10];

        private void FixedUpdate()
        {
            if(_isDead) return;
        
            // 搜索攻击目标（玩家或其他攻击型物品）
            Physics.OverlapSphereNonAlloc(transform.position, attackRange, _colliders);
            foreach(var target in _colliders)
            {
                if(target.CompareTag("Player") || target.CompareTag("CollectItem") && target.gameObject != this.gameObject)
                {
                    if(Time.time >= _lastAttackTime + attackCooldown)
                    {
                        //Attack(target.gameObject);
                        _lastAttackTime = Time.time;
                    }
                }
            }
        }
    
        public void TakeDamage(int damageAmount)
        {
            health -= damageAmount;
            if(health <= 0)
            {
                StartCoroutine(DeathSequence());
            }
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
            // 爆炸伤害范围内的玩家
            // Collider[] players = Physics.OverlapSphere(transform.position, 2f);
            // foreach(var player in players)
            // {
            //     if(player.CompareTag("Player"))
            //     {
            //         player.GetComponent<PlayerHealth>().TakeDamage(damage);
            //     }
            // }
            //
            // // 爆炸特效
            // Instantiate(explosionEffect, transform.position, Quaternion.identity);
            //
            // // 变成可拾取状态
            // GetComponent<Item>().MakeCollectible();
        }
    }
}