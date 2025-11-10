using System;
using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Sirenix.OdinInspector;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector
{
    public class TreasureChestComponent : NetworkAutoInjectHandlerBehaviour, IPickable, IItem, IPoolable
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
            var colliderConfig = GamePhysicsSystem.CreateColliderConfig(_chestCollider);
            GameObjectContainer.Instance.AddDynamicObject(netId, transform.position, colliderConfig, ObjectType.Chest, gameObject.layer);
            //_chestDataConfig = configProvider.GetConfig<ChestDataConfig>();
            _chestCommonData = _jsonDataConfig.ChestCommonData;

            lid.transform.eulerAngles = _chestCommonData.InitEulerAngles;
            if (ClientHandler)
            {
                Debug.Log("Init Chest send TargetShowEvent from client called on Init");
                _gameEventManager?.Publish(new TargetShowEvent(transform, _playerTransform, netId));
            }
        }
        
        public override void OnStartClient()
        {
            base.OnStartClient();
            _playerTransform = GameObject.FindGameObjectWithTag("Player").transform;
            Debug.Log("Init Chest send TargetShowEvent from client called on OnStartClient");
            _gameEventManager?.Publish(new TargetShowEvent(transform, _playerTransform, netId));
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

        private void OnDisable()
        {
            _gameEventManager?.Publish(new TargetShowEvent(null, null, netId));
        }

        private void OnDestroy()
        {
            _gameEventManager?.Publish(new TargetShowEvent(null, null, netId));
            _disposables?.Clear();
        }

        public void RequestPick(int pickerConnectionId)
        {
            
        }

        uint IPickable.SceneItemId => netId;

        public async UniTask PickUpSuccess(Action onFinish = null)
        {
            await OpenLid();
            onFinish?.Invoke();
            GameObjectPoolManger.Instance.ReturnObject(gameObject);
        }

        public uint ItemId { get; set; }

        public void OnSelfSpawn()
        {
            if (isClient)
            {
                _gameEventManager?.Publish(new TargetShowEvent(transform, _playerTransform, netId));
            }
        }

        public void OnSelfDespawn()
        {
            if (isClient && _chestCollider)
            {
                _chestCollider.enabled = true;
            }
            _gameEventManager?.Publish(new TargetShowEvent(null, null, netId));
            //_chestDataConfig = null;
            _disposables?.Clear();
            GameObjectContainer.Instance.RemoveDynamicObject(netId);
        }
    }

    [Serializable]
    public struct ChestData
    {
        public int Id;
        //public ChestType Type;
    }
}
