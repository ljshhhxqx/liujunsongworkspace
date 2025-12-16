using System;
using System.Collections.Generic;
using AOTScripts.Data.NetworkMes;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Collector.Collects;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Effect;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector
{
    public class CollectObjectController : CollectObject
    {
        private PooledObject _pooledObject;
        private CollectParticlePlayer _collectParticlePlayer;
        private CollectAnimationComponent _collectAnimationComponent;
        private InteractSystem _interactSystem;
        private Collider _positionCollider;
        private BuffExtraData _buffData;
        [SerializeField]
        private int collectConfigId;
        [SerializeField]
        private Renderer fillRenderer;
        private LayerMask _playerLayer;  
        protected LayerMask _sceneLayer;
        private CollectObjectType _collectObjectType;
        public int CollectConfigId => collectConfigId;
        public override Collider Collider => _collider;
        public CollectObjectData CollectObjectData { get; private set; }
        public BuffExtraData BuffData => _buffData;
        protected Transform PlayerTransform;
        protected AttackConfig AttackConfig;
        protected JsonDataConfig JsonConfig;

        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private Collider _collider;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private IDisposable _disposable;
        protected ItemsSpawnerManager SpawnerManager;
        protected IColliderConfig ColliderConfig;
        protected HashSet<DynamicObjectData> CachedDynamicObjectData = new HashSet<DynamicObjectData>();
        public uint NetId;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            SpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            _collectObjectType = SpawnerManager.GetCollectObjectType(netId);
            var playerConfig = jsonDataConfig.PlayerConfig;
            _playerLayer = playerConfig.PlayerLayer;
            _sceneLayer = jsonDataConfig.GameConfig.groundSceneLayer;
            var collectObjectDataConfig = configProvider.GetConfig<CollectObjectDataConfig>();
            var collectCollider = GetComponentInChildren<CollectCollider>();
            _collider = collectCollider.GetComponent<Collider>();
            JsonConfig = configProvider.GetConfig<JsonDataConfig>();
            _collectAnimationComponent = GetComponent<CollectAnimationComponent>();
            CollectObjectData = collectObjectDataConfig.GetCollectObjectData(collectConfigId);
            if (!collectCollider)
            {
                Debug.LogError("Collider not found");
                return;
            }
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(collectCollider.GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, ColliderConfig, ObjectType.Collectable, gameObject.layer, gameObject.tag);
            
            NetId = netId;
            PlayerTransform ??= PlayerInGameManager.Instance.LocalPlayerTransform;
            ChangeBehaviour();
        }

        private void InitAttackItem(AttackInfo attackInfo)
        {
            if (!gameObject.TryGetComponent<AttackCollectItem>(out var attackCollectItem))
            {
                attackCollectItem = gameObject.AddComponent<AttackCollectItem>();
                ObjectInjectProvider.Instance.Inject(attackCollectItem);
            }
            if (AttackConfig == default)
            {
                AttackConfig.MinAttackInterval = JsonConfig.CollectData.attackCooldown.min;
                AttackConfig.MaxAttackInterval = JsonConfig.CollectData.attackCooldown.max;
                AttackConfig.MinAttackPower = JsonConfig.CollectData.attackPowerRange.min;
                AttackConfig.MaxAttackPower = JsonConfig.CollectData.attackPowerRange.max;
            }
            attackCollectItem.enabled = true;
            attackCollectItem.Init(attackInfo, ServerHandler, netId, ClientHandler, PlayerTransform, AttackConfig);
        }
        
        private void InitMoveItem(MoveInfo moveInfo)
        {
            if (!gameObject.TryGetComponent<MoveCollectItem>(out var moveCollectItem))
            {
                moveCollectItem = gameObject.AddComponent<MoveCollectItem>();
                ObjectInjectProvider.Instance.Inject(moveCollectItem);
            }
            moveCollectItem.enabled = true;
            moveCollectItem.Init(moveInfo, ServerHandler, netId);
        }
        
        private void InitHiddenItem(HiddenItemData hiddenItemData)
        {
            if (!gameObject.TryGetComponent<HiddenItem>(out var hiddenCollectItem))
            {
                hiddenCollectItem = gameObject.AddComponent<HiddenItem>();
                ObjectInjectProvider.Instance.Inject(hiddenCollectItem);
            }
            hiddenCollectItem.enabled = true;
            hiddenCollectItem.Init(hiddenItemData, ServerHandler, netId);
        }

        private void DisableComponent<T>() where T : CollectBehaviour
        {
            if (gameObject.TryGetComponent<T>(out var component))
            {
                component.enabled = false;
            }
        }

        private void ChangeBehaviour()
        {
            Debug.Log($"CollectObjectController::ChangeBehaviour call" + _collectObjectType);
            switch (_collectObjectType)
            {
                case CollectObjectType.Attack:
                    var attackInfo = SpawnerManager.GetAttackInfo(NetId);
                    InitAttackItem(attackInfo);
                    DisableComponent<MoveCollectItem>();
                    DisableComponent<HiddenItem>();
                    break;
                case CollectObjectType.Move:
                    var moveInfo = SpawnerManager.GetMoveInfo(NetId);
                    InitMoveItem(moveInfo);
                    DisableComponent<HiddenItem>();
                    DisableComponent<AttackCollectItem>();
                    break;
                case CollectObjectType.Hidden:
                    var hiddenItemData = SpawnerManager.GetHiddenItemData(NetId);
                    InitHiddenItem(hiddenItemData);
                    DisableComponent<AttackCollectItem>();
                    DisableComponent<MoveCollectItem>();
                    break;
                case CollectObjectType.AttackMove:
                    attackInfo = SpawnerManager.GetAttackInfo(NetId);
                    moveInfo = SpawnerManager.GetMoveInfo(NetId);
                    InitAttackItem(attackInfo);
                    InitMoveItem(moveInfo);
                    DisableComponent<HiddenItem>();
                    break;
                case CollectObjectType.AttackHidden:
                    attackInfo = SpawnerManager.GetAttackInfo(NetId);
                    hiddenItemData = SpawnerManager.GetHiddenItemData(NetId);
                    InitHiddenItem(hiddenItemData);
                    InitAttackItem(attackInfo);
                    DisableComponent<MoveCollectItem>();
                    break;
                case CollectObjectType.MoveHidden:
                    hiddenItemData = SpawnerManager.GetHiddenItemData(NetId);
                    moveInfo = SpawnerManager.GetMoveInfo(NetId);
                    InitHiddenItem(hiddenItemData);
                    InitMoveItem(moveInfo);
                    DisableComponent<AttackCollectItem>();
                    break;
                case CollectObjectType.AttackMoveHidden:
                    attackInfo = SpawnerManager.GetAttackInfo(NetId);
                    hiddenItemData = SpawnerManager.GetHiddenItemData(NetId);
                    moveInfo = SpawnerManager.GetMoveInfo(NetId);
                    InitHiddenItem(hiddenItemData);
                    InitMoveItem(moveInfo);
                    InitAttackItem(attackInfo);
                    break;
                case CollectObjectType.None:
                    DisableComponent<MoveCollectItem>();
                    DisableComponent<HiddenItem>();
                    DisableComponent<AttackCollectItem>();
                    if (ClientHandler)
                    {
                        _collectAnimationComponent?.Play();
                    }
                    break;
            }
        }

        public override void OnSelfSpawn()
        {
            base.OnSelfSpawn();
            if (ClientHandler && _collider)
            {
                Debug.Log("Local player collider enabled");
                ChangeBehaviour();
            }
        }

        public override void OnSelfDespawn()
        {
            base.OnSelfDespawn();
            Debug.Log("Local player collider disabled");
            if (_collider)
            {
                _collider.enabled = false;
            }
            GameObjectContainer.Instance.RemoveDynamicObject(netId);
            _disposable?.Dispose();
        }
        
        protected override void SendCollectRequest(uint pickerId, PickerType pickerType)
        {
        }

        private void OnDestroy()
        {
            _disposable?.Dispose();
            GameObjectContainer.Instance.RemoveDynamicObject(netId);
        }

        public void CollectSuccess()
        {
            _collectParticlePlayer.Play(_collectAnimationComponent.OutlineColorValue);
        }

        [ClientRpc]
        public void RpcOnDeath()
        {
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Explode, transform.position, transform);
            EffectPlayer.Instance.PlayEffect(ParticlesType.Explode, transform.position, transform);
            //NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
        }
    }
}