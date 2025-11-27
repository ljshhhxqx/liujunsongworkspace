using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using UnityEngine;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class ItemBullet : NetworkAutoInjectHandlerBehaviour
    {
        private IColliderConfig _colliderConfig;
        private HashSet<DynamicObjectData> _hitObjects = new HashSet<DynamicObjectData>();
        private bool _isHandle;
        private Vector3 _direction;
        private float _speed;
        private float _lifeTime;
        private uint _attackId;
        private float _attackPower;
        private uint _spawnerId;
        private InteractSystem _interactSystem;
        private float _criticalRate;
        private float _criticalDamage;
        protected override bool AutoInjectLocalPlayer => false;

        public void Init(Vector3 direction, float speed, float lifeTime, float attackPower, uint spawnerId, float criticalRate, float criticalDamage)
        {
            _direction = direction;
            _speed = speed;
            _lifeTime = lifeTime;
            _isHandle = false;
            _attackPower = attackPower;
            _spawnerId = spawnerId;
            _criticalRate = criticalRate;
            _criticalDamage = criticalDamage;
        }

        protected override void InjectLocalPlayerCallback()
        {
            _interactSystem = FindObjectOfType<InteractSystem>();
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Bullet, gameObject.layer, gameObject.tag);
        }
        
        private void FixedUpdate()
        {
            if (!ServerHandler || _direction == Vector3.zero || _isHandle)
            {
                return;
            }

            GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, _colliderConfig,
                _hitObjects, OnIntersect);

            if (_isHandle)
            {
                NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
                var request = new SceneItemAttackInteractRequest
                {
                    Header = InteractSystem.CreateInteractHeader(0, InteractCategory.SceneToPlayer,
                        transform.position),
                    InteractionType = InteractionType.ItemAttack,
                    SceneItemId = _spawnerId,
                    TargetId = _attackId,
                    AttackPower =_attackPower,
                    CriticalRate = _criticalRate,
                    CriticalDamage = _criticalDamage,
                };
                _interactSystem.EnqueueCommand(request);
                return;
            }

            transform.position += _direction * (_speed * Time.fixedDeltaTime);
            _lifeTime -= Time.deltaTime;
            if (_lifeTime <= 0)
            {
                NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
            }
        }

        private bool OnIntersect(DynamicObjectData hitObject)
        {
            if (_spawnerId== hitObject.NetId)
            {
                return false;
            }
            if (hitObject.Type == ObjectType.Player || hitObject.Type == ObjectType.Collectable && !_isHandle)
            {
                _isHandle = true;
                _attackId = hitObject.NetId;
                return true;
            }

            return false;
        }
    }
}