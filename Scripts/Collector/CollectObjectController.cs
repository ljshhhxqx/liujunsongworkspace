using System;
using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Data.NetworkMes;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Collector.Collects;
using HotUpdate.Scripts.Collector.Collects.Move;
using HotUpdate.Scripts.Collector.Effect;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Data;
using HotUpdate.Scripts.Effect;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.Server.InGame;
using Mirror;
using Sirenix.OdinInspector;
using UniRx;
using UnityEngine;
using UnityEngine.Serialization;
using VContainer;

namespace HotUpdate.Scripts.Collector
{
    public class CollectObjectController : CollectObject, IEffectPlayer
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
        [SerializeField]
        private AttackMainEffect attackMainEffect;
        private LayerMask _playerLayer;  
        protected LayerMask _sceneLayer;
        [SyncVar(hook = nameof(OnCollectObjectTypeChanged))]
        public int collectObjectType;
        public int CollectConfigId => collectConfigId;
        public override Collider Collider => _collider;
        public CollectObjectData CollectObjectData { get; private set; }
        public BuffExtraData BuffData => _buffData;
        private Transform _playerTransform;
        private AttackConfig _config;
        private JsonDataConfig _jsonDataConfig;
        private CollectData _collectData;

        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private Collider _collider;
        private Collider _gameObjectCollider;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private IDisposable _disposable;
        private ItemsSpawnerManager _itemsSpawnerManager;
        private IColliderConfig _colliderConfig;
        protected HashSet<DynamicObjectData> CachedDynamicObjectData = new HashSet<DynamicObjectData>();
        public uint NetId;
        private AttackCollectItem _attackCollectItem;
        private MoveCollectItem _moveCollectItem;
        private HiddenItem _hiddenItem;
        
        [Inject]
        private void Init(IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
//            Debug.Log($"{name} CollectObjectController Init -- {ServerHandler} {ClientHandler}");
            var jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _itemsSpawnerManager = FindObjectOfType<ItemsSpawnerManager>();
            var playerConfig = jsonDataConfig.PlayerConfig;
            _playerLayer = playerConfig.PlayerLayer;
            _sceneLayer = jsonDataConfig.GameConfig.groundSceneLayer;
            var collectObjectDataConfig = configProvider.GetConfig<CollectObjectDataConfig>();
            var collectCollider = GetComponentInChildren<CollectCollider>();
            _collider = collectCollider.GetComponent<Collider>();
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            _collectAnimationComponent = GetComponent<CollectAnimationComponent>();
            _gameObjectCollider = GetComponent<Collider>();
            _collectData = jsonDataConfig.CollectData;
            CollectObjectData = collectObjectDataConfig.GetCollectObjectData(collectConfigId);
            if (!collectCollider)
            {
                Debug.LogError("Collider not found");
                return;
            }
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(collectCollider.GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, _colliderConfig, ObjectType.Collectable, gameObject.layer, gameObject.tag);
            
            NetId = netId;
            _gameObjectCollider.enabled = false;
            _playerTransform ??= playerInGameManager.LocalPlayerTransform;
        }

        private void OnCollectObjectTypeChanged(int oldValue, int newValue)
        {
            //Debug.Log("[CollectObjectController] OnCollectObjectTypeChanged -- " + (CollectObjectType)newValue);
            if (ServerHandler)
            {
                return;
            }
            ChangeBehaviour(newValue);
            if (newValue == (int)CollectObjectType.AttackMove || newValue == (int)CollectObjectType.AttackHidden || newValue == (int)CollectObjectType.AttackMoveHidden || newValue == (int)CollectObjectType.Attack)
            {
                _gameObjectCollider.enabled = true;
            }
        }

        public void ServerChangeBehaviour()
        {
//            Debug.Log("[CollectObjectController] ServerHandler ChangeBehaviour -- " + (CollectObjectType)collectObjectType);
            ChangeBehaviour(collectObjectType);
            if (collectObjectType == (int)CollectObjectType.AttackMove || collectObjectType == (int)CollectObjectType.AttackHidden || collectObjectType == (int)CollectObjectType.AttackMoveHidden || collectObjectType == (int)CollectObjectType.Attack)
            {
                _gameObjectCollider.enabled = true;
            }
            if (collectObjectType == 0)
            {
                RpcPlayAnimation();
            }
        }

        [ClientRpc]
        private void RpcPlayAnimation()
        {
            _collectAnimationComponent.Play();
        }

        private void InitAttackItem(AttackInfo attackInfo)
        {
            if (!gameObject.TryGetComponent<AttackCollectItem>(out _attackCollectItem))
            {
                _attackCollectItem = gameObject.AddComponent<AttackCollectItem>();
                ObjectInjectProvider.Instance.InjectMap((MapType)GameLoopDataModel.GameSceneName.Value, _attackCollectItem);
            }
            if (_config == default)
            {
                _config.MinAttackInterval = _jsonDataConfig.CollectData.attackCooldown.min;
                _config.MaxAttackInterval = _jsonDataConfig.CollectData.attackCooldown.max;
                _config.MinAttackPower = _jsonDataConfig.CollectData.attackPowerRange.min;
                _config.MaxAttackPower = _jsonDataConfig.CollectData.attackPowerRange.max;
            }
            _attackCollectItem.enabled = true;
            KeyframeData[] keyframeDatas;
            if (attackInfo.isRemoteAttack)
            {
                keyframeDatas = new[] { _collectData.bulletFrameData };
            }
            else
            {
                keyframeDatas = new[] { _collectData.attackFrameData };
            }
            _attackCollectItem.Init(attackInfo, ServerHandler, netId, ClientHandler, _playerTransform, _config, keyframeDatas, attackMainEffect, netIdentity);
        }
        
        private void InitMoveItem(MoveInfo moveInfo)
        {
            if (!gameObject.TryGetComponent<MoveCollectItem>(out _moveCollectItem))
            {
                _moveCollectItem = gameObject.AddComponent<MoveCollectItem>();
                ObjectInjectProvider.Instance.InjectMap((MapType)GameLoopDataModel.GameSceneName.Value, _moveCollectItem);
            }
            _moveCollectItem.enabled = true;
            _moveCollectItem.Init(moveInfo, ServerHandler, netId);
        }
        
        private void InitHiddenItem(HiddenItemData hiddenItemData)
        {
            if (!gameObject.TryGetComponent<HiddenItem>(out _hiddenItem))
            {
                _hiddenItem = gameObject.AddComponent<HiddenItem>();
                ObjectInjectProvider.Instance.InjectMap((MapType)GameLoopDataModel.GameSceneName.Value, _hiddenItem);
            }
            _hiddenItem.enabled = true;
            _hiddenItem.Init(hiddenItemData, ServerHandler, netId);
        }

        private void DisableComponent<T>() where T : CollectBehaviour
        {
            if (gameObject.TryGetComponent<T>(out var component))
            {
                component.enabled = false;
            }
        }

        private void ChangeBehaviour(int collectType)
        {
            //Debug.Log($"CollectObjectController::ChangeBehaviour call" + collectType);
            switch ((CollectObjectType)collectType)
            {
                case CollectObjectType.Attack:
                    var attackInfo = _itemsSpawnerManager.GetAttackInfo(NetId);
                    InitAttackItem(attackInfo);
                    DisableComponent<MoveCollectItem>();
                    DisableComponent<HiddenItem>();
                    break;
                case CollectObjectType.Move:
                    var moveInfo = _itemsSpawnerManager.GetMoveInfo(NetId);
                    InitMoveItem(moveInfo);
                    DisableComponent<HiddenItem>();
                    DisableComponent<AttackCollectItem>();
                    break;
                case CollectObjectType.Hidden:
                    var hiddenItemData = _itemsSpawnerManager.GetHiddenItemData(NetId);
                    InitHiddenItem(hiddenItemData);
                    DisableComponent<AttackCollectItem>();
                    DisableComponent<MoveCollectItem>();
                    break;
                case CollectObjectType.AttackMove:
                    attackInfo = _itemsSpawnerManager.GetAttackInfo(NetId);
                    moveInfo = _itemsSpawnerManager.GetMoveInfo(NetId);
                    InitAttackItem(attackInfo);
                    InitMoveItem(moveInfo);
                    DisableComponent<HiddenItem>();
                    break;
                case CollectObjectType.AttackHidden:
                    attackInfo = _itemsSpawnerManager.GetAttackInfo(NetId);
                    hiddenItemData = _itemsSpawnerManager.GetHiddenItemData(NetId);
                    InitHiddenItem(hiddenItemData);
                    InitAttackItem(attackInfo);
                    DisableComponent<MoveCollectItem>();
                    break;
                case CollectObjectType.MoveHidden:
                    hiddenItemData = _itemsSpawnerManager.GetHiddenItemData(NetId);
                    moveInfo = _itemsSpawnerManager.GetMoveInfo(NetId);
                    InitHiddenItem(hiddenItemData);
                    InitMoveItem(moveInfo);
                    DisableComponent<AttackCollectItem>();
                    break;
                case CollectObjectType.AttackMoveHidden:
                    attackInfo = _itemsSpawnerManager.GetAttackInfo(NetId);
                    hiddenItemData = _itemsSpawnerManager.GetHiddenItemData(NetId);
                    moveInfo = _itemsSpawnerManager.GetMoveInfo(NetId);
                    InitHiddenItem(hiddenItemData);
                    InitMoveItem(moveInfo);
                    InitAttackItem(attackInfo);
                    break;
                case CollectObjectType.None:
                    DisableComponent<MoveCollectItem>();
                    DisableComponent<HiddenItem>();
                    DisableComponent<AttackCollectItem>();
                    break;
            }
        }

        public override void OnSelfSpawn()
        {
            base.OnSelfSpawn();
            if (ClientHandler)
            {
                //Debug.Log("Local player collider enabled");
                //ChangeBehaviour(collectObjectType);
            }
        }

        public override void OnSelfDespawn()
        {
            base.OnSelfDespawn();
            //Debug.Log("Local player collider disabled");
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

        [ClientRpc]
        public void RpcOnDeath()
        {
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Explode, transform.position, transform);
            EffectPlayer.Instance.PlayEffect(ParticlesType.Explode, transform.position, transform);
            //NetworkGameObjectPoolManager.Instance.Despawn(gameObject);
        }

        [ClientRpc]
        public void RpcPlayEffect(ParticlesType type)
        {
            EffectPlayer.Instance.PlayEffect(type, transform.position, transform);
        }

        private CollectObjectController _collectObjectController;

        [ClientRpc]
        public void RpcSwitchAttackMode(bool isAttacking)
        {
            _attackCollectItem.RpcSwitchAttackMode(isAttacking);
        }
        
        [ClientRpc]
        public void RpcTriggerAttack( )
        {
            _attackCollectItem.TriggerAttack();
        }

        public void PickerSuccess()
        {
            
        }
    }
}