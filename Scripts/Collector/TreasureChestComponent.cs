using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.NetworkMes;
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
        private Collider _chestCollider;
        private ChestDataConfig _chestDataConfig;
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
        private IDisposable _triggerEnterObserver;
        private IDisposable _triggerExitObserver;

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager)
        {
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
            _chestCommonData = _chestDataConfig.GetChestCommonData();
            _mirrorNetworkMessageHandler = FindObjectOfType<MirrorNetworkMessageHandler>();
            _gameEventManager.Publish(new TargetShowEvent(transform));
            lid.transform.eulerAngles = _chestCommonData.InitEulerAngles;
            _triggerEnterObserver = _chestCollider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver);
            _triggerExitObserver = _chestCollider.OnTriggerExitAsObservable()
                .Subscribe(OnTriggerExitObserver);
        }

        private void OnReturnToPool()
        {
            _gameEventManager?.Publish(new TargetShowEvent(null));
            _gameEventManager = null;
            _chestDataConfig = null;
            _chestCommonData = default;
            _mirrorNetworkMessageHandler = null;
            _triggerEnterObserver?.Dispose();
            _triggerExitObserver?.Dispose(); 
            _pooledObject.OnSelfDespawn -= OnReturnToPool;
        }

        private void OnTriggerExitObserver(Collider other)
        {
            if (other.CompareTag("Player") && isClient)
            {
                _gameEventManager.Publish(new GameInteractableEffect(other.gameObject, this, false));
            }
        }
        
        private void OnTriggerEnterObserver(Collider other)
        {
            if (other.CompareTag("Player") && isClient)
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


        public void RequestPick(uint pickerNetId)
        {
            if (isClient)
            {
                _mirrorNetworkMessageHandler.SendToServer(new MirrorPickerPickUpChestMessage(pickerNetId, netId));
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
