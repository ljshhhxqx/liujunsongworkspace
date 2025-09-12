using System;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.NetworkMes;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Tool.Coroutine;
using Mirror;
using Sirenix.OdinInspector;
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
        
        
        public int CollectConfigId => collectConfigId;
        public override Collider Collider => _collider;
        public CollectObjectData CollectObjectData { get; private set; }
        public BuffExtraData BuffData => _buffData;

        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private Collider _collider;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private IDisposable _disposable;

        public void SetMaterial(Material material)
        {
            if (CollectObjectData.collectObjectClass == CollectObjectClass.Buff)
            {
                fillRenderer.material = material;
                _collectAnimationComponent.SetOutlineColor(material.color);
                return;
            }
            //Debug.Log($"SetMaterial failed, CollectObject config id-{CollectObjectData.id} is not a buff collect object");
        }

        public void SetBuffData(BuffExtraData buffExtraData)
        {
            if (CollectObjectData.collectObjectClass == CollectObjectClass.Buff)
            {
                _buffData = buffExtraData;
                return;
            }
            //Debug.Log($"SetBuffData failed, CollectObject config id-{CollectObjectData.id} is not a buff collect object");
        }

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var playerConfig = configProvider.GetConfig<JsonDataConfig>().PlayerConfig;
            _playerLayer = playerConfig.PlayerLayer;
            var collectObjectDataConfig = configProvider.GetConfig<CollectObjectDataConfig>();
            _pooledObject = GetComponent<PooledObject>();
            // if (_pooledObject)
            // {
            //     _pooledObject.OnSelfDespawn += OnReturnToPool;
            // }
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


            if (isClient)
            {
                _collectAnimationComponent?.Play();
                Debug.Log("Local player animation");
                _collider = collectCollider.GetComponent<Collider>();
                _collider.enabled = true;
                _disposable = _collider.OnTriggerEnterAsObservable()
                    .Subscribe(OnTriggerEnterObserver)
                    .AddTo(this);
            }
            CollectObjectData = collectObjectDataConfig.GetCollectObjectData(collectConfigId);
        }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ObjectInjectProvider.Instance.Inject(this);
        }

        [ClientRpc]
        public void RpcRecycleItem()
        {
            if (_collider)
            {
                _collider.enabled = false;
            }

            if (isServer)
            {
                return;
            }
            GameObjectPoolManger.Instance.ReturnObject(gameObject);
            //_collectParticlePlayer.Play(_collectAnimationComponent.OutlineColorValue);
            //DelayInvoker.DelayInvoke(0.75f, ReturnToPool);
        }
        
        private void OnTriggerEnterObserver(Collider other)
        {
            
            if ((_playerLayer.value & (1 << other.gameObject.layer)) == 0)
            {
                return;
            }
            
            if (other.TryGetComponent<Picker>(out var pickerComponent))
            {
                pickerComponent.SendCollectRequest(pickerComponent.netId, pickerComponent.PickerType, netId);
            }
        }
        
        protected override void SendCollectRequest(uint pickerId, PickerType pickerType)
        {
            // var request = new SceneInteractRequest
            // {
            //     Header = GameSyncManager.CreateInteractHeader(PlayerInGameManager.Instance.GetPlayerId(pickerId), InteractCategory.PlayerToScene,
            //         transform.position, CommandAuthority.Client),
            //     InteractionType = InteractionType.PickupItem,
            //     SceneItemId = ItemId,
            // };
            // var json = MemoryPackSerializer.Serialize(request);
            // _interactSystem.EnqueueCommand(json);
        }

        public void CollectSuccess()
        {
            _collectParticlePlayer.Play(_collectAnimationComponent.OutlineColorValue);
        }

        [Button("重置配置数据")]
        private void SetConfigData()
        {
#if UNITY_EDITOR
            // string path = Path.Combine(Application.streamingAssetsPath, "Config");
            // string filePath = Path.Combine(path, $"CollectObjectDataConfig.json");
            // // 检查文件是否存在
            // if (File.Exists(filePath))
            // {
            //     // 读取 JSON 文件内容
            //     string json = File.ReadAllText(filePath);
            //
            //     CollectObjectDataConfig newInstance = ScriptableObject.CreateInstance<CollectObjectDataConfig>();
            //
            //     // 将 JSON 数据应用到新的实例中
            //     JsonUtility.FromJsonOverwrite(json, newInstance);
            //
            //     // 输出日志确认加载成功
            //     Debug.Log($"ScriptableObject instance created from {filePath}");
            //
            //     foreach (var item in newInstance.CollectConfigDatas)
            //     {
            //         if (item.CollectType.ToString().Equals(this.name))
            //         {
            //             CollectObjectData = item;
            //         }
            //     }
            //
            //     AssetDatabase.Refresh();
            // }
            // else
            // {
            //     Debug.LogWarning($"File not found at {filePath}");
            // }
#endif
        }
    }
}