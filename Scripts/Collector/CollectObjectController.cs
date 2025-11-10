using System;
using System.Collections.Generic;
using AOTScripts.Data.NetworkMes;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
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
        
        public int CollectConfigId => collectConfigId;
        public override Collider Collider => _collider;
        public CollectObjectData CollectObjectData { get; private set; }
        public BuffExtraData BuffData => _buffData;

        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private Collider _collider;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private IDisposable _disposable;
        protected IColliderConfig ColliderConfig;
        protected HashSet<DynamicObjectData> CachedDynamicObjectData = new HashSet<DynamicObjectData>();

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            var playerConfig = jsonDataConfig.PlayerConfig;
            _playerLayer = playerConfig.PlayerLayer;
            _sceneLayer = jsonDataConfig.GameConfig.groundSceneLayer;
            var collectObjectDataConfig = configProvider.GetConfig<CollectObjectDataConfig>();
            var collectCollider = GetComponentInChildren<CollectCollider>();
            _collider = collectCollider.GetComponent<Collider>();
            CollectObjectData = collectObjectDataConfig.GetCollectObjectData(collectConfigId);
            if (!collectCollider)
            {
                Debug.LogError("Collider not found");
                return;
            }
            ColliderConfig = GamePhysicsSystem.CreateColliderConfig(collectCollider.GetComponent<Collider>());
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, ColliderConfig, ObjectType.Collectable, gameObject.layer);

            if (ClientHandler)
            {
                Debug.Log($"CollectObjectController::Init call On Init");
                // _disposable = Observable.EveryFixedUpdate()
                //     .Where(_ => _collider.enabled &&
                //                 GameObjectContainer.Instance.DynamicObjectIntersects(transform.position, ColliderConfig,
                //                     CachedDynamicObjectData))
                //     .Subscribe(_ =>
                //     {
                //         OnTriggerEnterObserver();
                //     });
            }
        }

        public override void OnSelfSpawn()
        {
            base.OnSelfSpawn();
            if (ClientHandler && _collider)
            {
                Debug.Log("Local player collider enabled");
                // _collider.enabled = true;
                // _disposable = Observable.EveryFixedUpdate()
                //     .Where(_ => _collider.enabled &&
                //                 GameObjectContainer.Instance.DynamicObjectIntersects(transform.position, ColliderConfig,
                //                     CachedDynamicObjectData))
                //     .Subscribe(_ =>
                //     {
                //         OnTriggerEnterObserver();
                //     });
                _collectAnimationComponent?.Play();
            }
        }

        protected override void StartClient()
        {
            Debug.Log("Local player collider started");
            _collectParticlePlayer = GetComponentInChildren<CollectParticlePlayer>();
            _collectAnimationComponent = GetComponent<CollectAnimationComponent>();
            _mirrorNetworkMessageHandler = FindObjectOfType<MirrorNetworkMessageHandler>();
            _interactSystem = FindObjectOfType<InteractSystem>();
            _collectAnimationComponent?.Play();
            Debug.Log("Local player animation");
            _collider.enabled = true;
            _collectAnimationComponent?.Play();
            Debug.Log($"CollectObjectController::Init call On OnStartClient");
            // _disposable = Observable.EveryFixedUpdate()
            //     .Where(_ => _collider.enabled &&
            //                 GameObjectContainer.Instance.DynamicObjectIntersects(transform.position, ColliderConfig,
            //                     CachedDynamicObjectData))
            //     .Subscribe(_ =>
            //     {
            //         OnTriggerEnterObserver();
            //     });
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
        
        protected virtual void OnTriggerEnterObserver()
        {
            foreach (var data in CachedDynamicObjectData)
            {
                if ((_playerLayer.value & (1 << data.Layer)) == 0 || data.Type != ObjectType.Player)
                {
                    continue;
                }
                
                var player = NetworkClient.spawned[data.NetId];
                var picker = player.GetComponent<Picker>();
                if (picker)
                {
                    picker.SendCollectRequest(picker.netId, picker.PickerType, netId,
                        CollectObjectData.collectObjectClass);
                }
            }
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
    }
}