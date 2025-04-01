using System;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
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
    public class TreasureChestComponent : NetworkBehaviour, IPickable, IItem
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
        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private ChestCommonData _chestCommonData;
        private GameEventManager _gameEventManager;
        private Collider _positionCollider;
        private InteractSystem _interactSystem;
        private PooledObject _pooledObject;
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
            _pooledObject = GetComponent<PooledObject>();
            if (_pooledObject)
            {
                _pooledObject.OnSelfDespawn += OnReturnToPool;
            }

            _gameEventManager = gameEventManager;
            var collectCollider = GetComponentInChildren<CollectCollider>();
            if (collectCollider == null)
            {
                Debug.LogError("Collider not found");
                return;
            }
            _chestCollider = collectCollider.GetComponent<Collider>();
            _chestCollider.enabled = true;
            //_chestDataConfig = configProvider.GetConfig<ChestDataConfig>();
            _chestCommonData = _jsonDataConfig.ChestCommonData;
            _mirrorNetworkMessageHandler = FindObjectOfType<MirrorNetworkMessageHandler>();
            if (isLocalPlayer)
            {
                _gameEventManager?.Publish(new TargetShowEvent(null));
            }
            lid.transform.eulerAngles = _chestCommonData.InitEulerAngles;
            _chestCollider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver)
                .AddTo(_disposables);
            _chestCollider.OnTriggerExitAsObservable()
                .Subscribe(OnTriggerExitObserver)
                .AddTo(_disposables);
        }

        private void OnReturnToPool()
        {
            if (isLocalPlayer)
            {
                _gameEventManager?.Publish(new TargetShowEvent(null));
            }
            _gameEventManager = null;
            //_chestDataConfig = null;
            _chestCommonData = default;
            _mirrorNetworkMessageHandler = null;
            _disposables?.Clear();
            _pooledObject.OnSelfDespawn -= OnReturnToPool;
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
            if (isLocalPlayer)
            {
                var request = new SceneInteractRequest
                {
                    Header = GameSyncManager.CreateInteractHeader(pickerConnectionId, InteractCategory.PlayerToScene,
                        transform.position, CommandAuthority.Client),
                    InteractionType = InteractionType.PickupChest,
                    SceneItemId = ItemId,
                };
                var json = MemoryPackSerializer.Serialize(request);
                _interactSystem.EnqueueCommand(json);
            }
        }

        public async UniTask PickUpSuccess(Action onFinish)
        {
            await OpenLid();
            onFinish?.Invoke();
        }

        public uint ItemId { get; set; }
    }

    [Serializable]
    public struct ChestData
    {
        public int Id;
        //public ChestType Type;
    }
}
