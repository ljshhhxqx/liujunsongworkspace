using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Network.NetworkMes;
using Sirenix.OdinInspector;
using Tool.GameEvent;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector
{
    public class TreasureChestComponent : NetworkBehaviour, IPickable
    {
        [SerializeField] 
        private GameObject lid; // 宝箱盖子
        [SerializeField]
        private LayerMask playerLayer;
        private Collider _chestCollider;
        private ChestDataConfig _chestDataConfig;
        private JsonDataConfig _jsonDataConfig;
        private MessageCenter _messageCenter;
        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private ChestCommonData _chestCommonData;
        private GameEventManager _gameEventManager;
        private Collider _positionCollider;
        private PooledObject _pooledObject;
        public Collider ChestCollider => _chestCollider;
        
        [SyncVar]
        public ChestType chestType;
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
            _chestDataConfig = configProvider.GetConfig<ChestDataConfig>();
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
            _chestDataConfig = null;
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
            var targetRotation = Quaternion.Euler(0, lid.transform.rotation.eulerAngles.y, lid.transform.rotation.eulerAngles.z);
         
            // 当宝箱盖子没有完全打开时
            while (Quaternion.Angle(lid.transform.rotation, targetRotation) > 0.5f)
            {
                lid.transform.rotation = Quaternion.Slerp(lid.transform.rotation, targetRotation, Time.deltaTime * _chestCommonData.OpenSpeed);
                await UniTask.Yield();
            }
        }


        public void RequestPick(uint pickerId)
        {
            if (isLocalPlayer)
            {
                _mirrorNetworkMessageHandler.SendToServer(new MirrorPickerPickUpChestMessage(pickerId, netId));
            }
        }

        public async UniTask PickUpSuccess(Action onFinish)
        {
            await OpenLid();
            onFinish?.Invoke();
        }
    }

    [Serializable]
    public struct ChestData
    {
        public int Id;
        public ChestType Type;
    }
}
