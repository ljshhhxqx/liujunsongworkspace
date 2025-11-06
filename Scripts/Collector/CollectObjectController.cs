using System;
using AOTScripts.Data.NetworkMes;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using UniRx;
using UniRx.Triggers;
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

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();
            var playerConfig = jsonDataConfig.PlayerConfig;
            _playerLayer = playerConfig.PlayerLayer;
            _sceneLayer = jsonDataConfig.GameConfig.groundSceneLayer;
            var collectObjectDataConfig = configProvider.GetConfig<CollectObjectDataConfig>();

            CollectObjectData = collectObjectDataConfig.GetCollectObjectData(collectConfigId);
            if (isClient)
            {
                Debug.Log($"CollectObjectController::Init call On Init");
                _disposable = _collider.OnTriggerEnterAsObservable()
                    .Subscribe(OnTriggerEnterObserver)
                    .AddTo(this);
            }
        }

        public override void OnSelfSpawn()
        {
            base.OnSelfSpawn();
            if (isClient && _collider)
            {
                Debug.Log("Local player collider enabled");
                _collider.enabled = true;
                _disposable = _collider.OnTriggerEnterAsObservable()
                    .Subscribe(OnTriggerEnterObserver)
                    .AddTo(this);
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
            var collectCollider = GetComponentInChildren<CollectCollider>();
            if (!collectCollider)
            {
                Debug.LogError("Collider not found");
                return;
            }
            _collectAnimationComponent?.Play();
            Debug.Log("Local player animation");
            _collider = collectCollider.GetComponent<Collider>();
            _collider.enabled = true;
            _collectAnimationComponent?.Play();
            Debug.Log($"CollectObjectController::Init call On OnStartClient");
            _disposable = _collider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver)
                .AddTo(this);
        }

        public override void OnSelfDespawn()
        {
            base.OnSelfDespawn();
            Debug.Log("Local player collider disabled");
            if (_collider)
            {
                _collider.enabled = false;
            }
            _disposable?.Dispose();
        }
        
        protected virtual void OnTriggerEnterObserver(Collider other)
        {
            if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0)
            {
                Debug.Log($"OnTriggerEnterObserver -- Not player layer, ignore");
                return;
            }
            
            if (other.TryGetComponent<Picker>(out var pickerComponent))
            {
                Debug.Log($"OnTriggerEnterObserver -- Picker component");
                pickerComponent.SendCollectRequest(pickerComponent.netId, pickerComponent.PickerType, netId, CollectObjectData.collectObjectClass);
            }
        }
        
        protected override void SendCollectRequest(uint pickerId, PickerType pickerType)
        {
        }

        public void CollectSuccess()
        {
            _collectParticlePlayer.Play(_collectAnimationComponent.OutlineColorValue);
        }
    }
}