using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Tool;
using AOTScripts.Tool.Coroutine;
using HotUpdate.Scripts.Collector.Effect;
using HotUpdate.Scripts.Effect;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;
using AnimationEvent = AOTScripts.Data.AnimationEvent;
using AnimationState = AOTScripts.Data.AnimationState;

namespace HotUpdate.Scripts.Collector.Collects
{
    public class AttackCollectItem : CollectBehaviour, IPoolable
    {
        [SyncVar]
        private AttackInfo _attackInfo;
        private CollectEffectController _collectEffectController;
        private float _lastAttackTime;
        private readonly HashSet<uint> _collectedObjects = new HashSet<uint>();
        private AttackMainEffect _attackMainEffect;
        private bool _isAttacking;
        private KeyframeCooldown _keyframeCooldown;
        private CompositeDisposable _disposable = new CompositeDisposable();
        private GameSyncManager _gameSyncManager;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private PlayerInGameManager _playerInGameManager;

        [Inject]
        private void Init(PlayerInGameManager playerInGameManager, GameSyncManager gameSyncManager,
            ItemsSpawnerManager itemsSpawnerManager)
        {
            _playerInGameManager = playerInGameManager;
            _gameSyncManager = gameSyncManager;
            _itemsSpawnerManager = itemsSpawnerManager;
        }

        private void FixedUpdate()
        {
            _keyframeCooldown.Update(Time.fixedDeltaTime);
            if (IsDead || !_gameSyncManager || !ServerHandler || !IsAttackable || !_keyframeCooldown.IsReady() || Time.time < _lastAttackTime + _attackInfo.attackCooldown)
            {
                //Debug.LogError($"{name} is dead or not attackable or attack cooldown not over yet");
                return;
            }
            
            if (_gameSyncManager.isGameOver)
            {
                return;
            }
            GameObjectContainer.Instance.DynamicObjectIntersects(NetId, transform.position, ColliderConfig,
                _collectedObjects, OnInteract);
        }

        public void RpcSwitchAttackMode(bool isAttacking)
        {
            if (isAttacking)
            {
                _collectEffectController.SwitchToAttackMode();
            }
            else
            {
                _collectEffectController.SwitchToTrackingMode();
            }
        }

        private bool OnInteract(DynamicObjectData target)
        {
            if (_isAttacking)
            {
                Debug.Log($"[ AttackCollectItem ] {NetId} Stop Attack");
                _isAttacking = false;
                CollectObjectController.RpcSwitchAttackMode(_isAttacking);
                return false;
            }
            if (target.Type == ObjectType.Player)
            {
                if (_playerInGameManager.IsPlayerDead(target.NetId, out var countdown))
                {
                    return false;
                }
                //Attack(_direction, target.NetId);
                if (!_isAttacking)
                {
                    Debug.Log($"[ AttackCollectItem ] Start Attack");
                    _isAttacking = true;
                    CollectObjectController.RpcSwitchAttackMode(_isAttacking);
                }
                _lastAttackTime = Time.time;
                _keyframeCooldown.Use();
                _collectEffectController.TriggerAttack();
                if (!_attackInfo.isRemoteAttack)
                {
                    CollectObjectController.RpcPlayEffect(ParticlesType.Slash);
                }
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
                CollectObjectController.RpcPlayEffect(ParticlesType.Emit);
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
        
        public void Init(AttackInfo info, bool serverHandler, uint id, bool clientHandler, Transform player, AttackConfig config,
            KeyframeData[] keyframeData, AttackMainEffect attackMainEffect)
        {
            // _disposable?.Dispose();
            // _disposable?.Clear();
            _gameSyncManager ??= FindObjectOfType<GameSyncManager>();
            _attackInfo = info;
            NetId = id;
            ServerHandler = serverHandler;
            _keyframeCooldown?.Reset();
            _keyframeCooldown = new KeyframeCooldown(AnimationState.Attack, info.attackCooldown, keyframeData, 1);
            _keyframeCooldown.EventStream
                .Where(x => x == AnimationEvent.OnAttack)
                .Subscribe(_ => OnAttack())
                .AddTo(_disposable);
            _attackMainEffect ??= attackMainEffect;
            if (clientHandler)
            {
                GameEventManager.Publish(new SceneItemSpawnedEvent(NetId, gameObject, true, player));
                if (!TryGetComponent(out _collectEffectController))
                {
                    _collectEffectController = _attackMainEffect.gameObject.AddComponent<CollectEffectController>();
                    _collectEffectController.Initialize(_itemsSpawnerManager.EffectShader);
                    _collectEffectController.SetMinMaxAttackParameters(config.MinAttackPower, config.MaxAttackPower, config.MinAttackInterval, config.MaxAttackInterval);
                    _collectEffectController.SetAttackParameters(GameStaticExtensions.GetAttackExpectancy(_attackInfo.damage, _attackInfo.criticalRate, _attackInfo.criticalDamage),  _attackInfo.attackCooldown);
                    _collectEffectController.SwitchToTrackingMode();
                }
            }
        }

        private void OnAttack()
        {
            if (_collectedObjects.Count == 0)
            {
                Debug.LogWarning($"[ OnAttack ] {name} collected no object");
                return;
            }

            var distance = float.MaxValue;
            DynamicObjectData dynamicObject = default;
            foreach (var id in _collectedObjects)
            {
                var data = GameObjectContainer.Instance.GetDynamicObjectData(id);
                var dis = Vector3.Distance(transform.position, data.Position);
                if (distance > dis)
                {
                    distance = dis;
                    dynamicObject = data;
                }
            }

            if (dynamicObject.ColliderConfig != null)
            {
                Debug.Log($"[ OnAttack ] {name} attack {dynamicObject.NetId}");
                var direction = (dynamicObject.Position - transform.position).normalized;
                Attack(direction, dynamicObject.NetId);
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
            GameEventManager.Publish(new SceneItemSpawnedEvent(NetId, gameObject, false, null));
        }

        public static KeyframeData KeyframeData;
    }

    public struct AttackConfig : IEquatable<AttackConfig>
    {
        public float MaxAttackPower;
        public float MinAttackPower;
        public float MaxAttackInterval;
        public float MinAttackInterval;

        public bool Equals(AttackConfig other)
        {
            return MaxAttackPower.Equals(other.MaxAttackPower) && MinAttackPower.Equals(other.MinAttackPower) && MaxAttackInterval.Equals(other.MaxAttackInterval) && MinAttackInterval.Equals(other.MinAttackInterval);
        }

        public override bool Equals(object obj)
        {
            return obj is AttackConfig other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(MaxAttackPower, MinAttackPower, MaxAttackInterval, MinAttackInterval);
        }

        public static bool operator ==(AttackConfig left, AttackConfig right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AttackConfig left, AttackConfig right)
        {
            return !left.Equals(right);
        }
    }
}