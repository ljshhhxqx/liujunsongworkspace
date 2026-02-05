using System.Collections.Generic;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Tool.ObjectPool;
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
        private bool _destroyed;
        private NetworkGameObjectPoolManager _networkGameObjectPoolManager;
        protected override bool AutoInjectLocalPlayer => false;

        public void Init(Vector3 direction, float speed, float lifeTime, float attackPower, uint spawnerId, float criticalRate, float criticalDamage)
        {
            _direction = direction.normalized;
            _speed = speed;
            _lifeTime = lifeTime;
            _isHandle = false;
            _attackPower = attackPower;
            _spawnerId = spawnerId;
            _criticalRate = criticalRate;
            _criticalDamage = criticalDamage;
            _destroyed = false;
            _interactSystem ??= FindObjectOfType<InteractSystem>();
            _networkGameObjectPoolManager ??= FindObjectOfType<NetworkGameObjectPoolManager>();
            _colliderConfig ??= GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Bullet, gameObject.layer, gameObject.tag);
        }
        
        private void FixedUpdate()
        {
            if (!ServerHandler || _direction == Vector3.zero || _isHandle || _destroyed) 
            {
                return;
            }
            if (_lifeTime <= 0)
            {
                _destroyed = true;
                _isHandle = false;
                _networkGameObjectPoolManager.Despawn(gameObject);
                Debug.Log($"[ItemBullet] Destroyed - {netId}");
                return;
            }
            GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, _colliderConfig,
                _hitObjects, OnIntersect);
            _lifeTime -= Time.fixedDeltaTime;
            transform.Translate(_direction * (_speed * Time.fixedDeltaTime));
        }

        private bool OnIntersect(DynamicObjectData hitObject)
        {
            if (_destroyed)
            {
                return false;
            }
            if (_spawnerId == hitObject.NetId || hitObject.NetId == 0)
            {
                return false;
            }
            if ((hitObject.Type == ObjectType.Player) && !_isHandle)
            {
                _destroyed = true;
                Debug.Log("[OnIntersect] ItemBullet hit " + hitObject.NetId);
                _isHandle = true;
                _attackId = hitObject.NetId;
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
                Debug.Log($"[ItemBullet] Send SceneItemAttackInteractRequest - SceneItemId: {request.SceneItemId} -  TargetId: {request.TargetId} -  AttackPower: {request.AttackPower} -  CriticalRate: {request.CriticalRate} -  CriticalDamage: {request.CriticalDamage}");
                _interactSystem.EnqueueCommand(request);
                _networkGameObjectPoolManager.Despawn(gameObject);
                return true;
            }

            return false;
        }
    }
}