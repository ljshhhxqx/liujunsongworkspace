using System;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using MemoryPack;
using Mirror;
using Sirenix.OdinInspector;
using Tool.GameEvent;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using VContainer;
using SceneInteractRequest = HotUpdate.Scripts.Network.PredictSystem.Interact.SceneInteractRequest;

namespace HotUpdate.Scripts.Collector
{
    public class TreasureChestComponent : NetworkBehaviour, IPickable, IItem, IPooledObject
    {
        [SerializeField] 
        private GameObject lid; // 宝箱盖子
        [SerializeField]
        private LayerMask playerLayer;
        [SerializeField]
        private QualityType quality;
        private Collider _chestCollider;
        //private ChestDataConfig _chestDataConfig;
        private JsonDataConfig _jsonDataConfig;
        private MessageCenter _messageCenter;
        private ChestCommonData _chestCommonData;
        private GameEventManager _gameEventManager;
        private Collider _positionCollider;
        private InteractSystem _interactSystem;
        private PooledObject _pooledObject;
        private Transform _playerTransform;
        private Quaternion _initRotation;
        public Collider ChestCollider => _chestCollider;
        public QualityType Quality => quality;
        
        // [SyncVar]
        // public ChestType chestType;
        [HideInInspector]
        [SyncVar]
        public bool isPicked;
        private readonly CompositeDisposable _disposables = new CompositeDisposable();

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
            _jsonDataConfig = configProvider.GetConfig<JsonDataConfig>();

            _gameEventManager = gameEventManager;
            var collectCollider = GetComponentInChildren<CollectCollider>();
            if (!collectCollider)
            {
                Debug.LogError("Collider not found");
                return;
            }
            _chestCollider = collectCollider.GetComponent<Collider>();
            _chestCollider.enabled = true;
            //_chestDataConfig = configProvider.GetConfig<ChestDataConfig>();
            _chestCommonData = _jsonDataConfig.ChestCommonData;

            lid.transform.eulerAngles = _chestCommonData.InitEulerAngles;
            if (isClient)
            {
                _chestCollider.OnTriggerEnterAsObservable()
                    .Subscribe(OnTriggerEnterObserver)
                    .AddTo(_disposables);
                _chestCollider.OnTriggerExitAsObservable()
                    .Subscribe(OnTriggerExitObserver)
                    .AddTo(_disposables);
                _playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
                _gameEventManager?.Publish(new TargetShowEvent(transform, _playerTransform, netId));
            }
        }

        private void OnEnable()
        {
            if (netId == 0)
            {
                return;
            }

            transform.rotation = _initRotation;
            if (!IsInUse && isClient)
            {
                IsInUse = true;
                _chestCollider.OnTriggerEnterAsObservable()
                    .Subscribe(OnTriggerEnterObserver)
                    .AddTo(_disposables);
                _chestCollider.OnTriggerExitAsObservable()
                    .Subscribe(OnTriggerExitObserver)
                    .AddTo(_disposables);
                _gameEventManager?.Publish(new TargetShowEvent(transform, _playerTransform, netId));
            }
            _gameEventManager?.Publish(new TargetShowEvent(transform, _playerTransform, netId));
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            _initRotation = transform.rotation;
            ObjectInjectProvider.Instance.Inject(this);
        }

        private void OnTriggerExitObserver(Collider other)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) != 0 && isClient)
            {
                _gameEventManager.Publish(new GameInteractableEffect(other.gameObject, this, false));
            }
        }
        
        private void OnTriggerEnterObserver(Collider other)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) != 0 && isClient)
            {
                _gameEventManager.Publish(new GameInteractableEffect(other.gameObject, this, true));
            }
        }
        
        [Button("开箱")]
        private void OpenChest()
        {
            OpenLid().Forget();
        }

        private async UniTask OpenLid()
        {
            _chestCollider.enabled = false;
            // 计算开启动画的目标角度
            var targetRotation = Quaternion.Euler(_chestCommonData.EndEulerAngles.x, _chestCommonData.EndEulerAngles.y, _chestCommonData.EndEulerAngles.z);
         
            // 当宝箱盖子没有完全打开时
            while (Quaternion.Angle(lid.transform.rotation, targetRotation) > 0.5f)
            {
                lid.transform.rotation = Quaternion.Slerp(lid.transform.rotation, targetRotation, Time.fixedDeltaTime * _chestCommonData.OpenSpeed);
                await UniTask.Yield();
            }
        }

        public void RequestPick(int pickerConnectionId)
        {
            
        }

        uint IPickable.SceneItemId => netId;

        public async UniTask PickUpSuccess(Action onFinish = null)
        {
            await OpenLid();
            onFinish?.Invoke();
            RecycleItem();
        }

        public uint ItemId { get; set; }

        private void RecycleItem()
        {
            IsInUse = false;
            _gameEventManager?.Publish(new TargetShowEvent(null, null, netId));
            _disposables?.Clear();
            gameObject.SetActive(false);
        }

        [ClientRpc]
        public void RpcRecycleItem()
        {
            RecycleItem();
        }

        public bool IsInUse { get; private set; }
    }

    [Serializable]
    public struct ChestData
    {
        public int Id;
        //public ChestType Type;
    }
}
