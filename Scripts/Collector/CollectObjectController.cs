using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.NetworkMes;
using Network.NetworkMes;
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
        private Collider _positionCollider;
        private BuffExtraData _buffData;
        [SerializeField]
        private int collectConfigId;
        [SerializeField]
        private Renderer fillRenderer;
        [SerializeField]
        private LayerMask playerLayer;  
        
        
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
            var collectObjectDataConfig = configProvider.GetConfig<CollectObjectDataConfig>();
            _pooledObject = GetComponent<PooledObject>();
            if (_pooledObject)
            {
                _pooledObject.OnSelfDespawn += OnReturnToPool;
            }
            _collectParticlePlayer = GetComponentInChildren<CollectParticlePlayer>();
            _collectAnimationComponent = GetComponent<CollectAnimationComponent>();
            _mirrorNetworkMessageHandler = FindObjectOfType<MirrorNetworkMessageHandler>();
            _collectAnimationComponent?.Play();
            var collectCollider = GetComponentInChildren<CollectCollider>();
            if (!collectCollider)
            {
                Debug.LogError("Collider not found");
                return;
            }
            CollectObjectData = collectObjectDataConfig.GetCollectObjectData(collectConfigId);
            _collider = collectCollider.GetComponent<Collider>();
            _collider.enabled = true;
            _disposable = _collider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver);
        }

        private void OnReturnToPool()
        {
            _disposable?.Dispose();
            _collectParticlePlayer = null;
            _collectAnimationComponent = null;
            _mirrorNetworkMessageHandler = null;
            _collider = null;
            _pooledObject.OnSelfDespawn -= OnReturnToPool;
        }
        
        private void OnTriggerEnterObserver(Collider other)
        {
            if ((playerLayer.value & (1 << other.gameObject.layer)) == 0 || !isClient)
            {
                return;
            }
            
            if (other.TryGetComponent<Picker>(out var pickerComponent))
            {
                SendCollectRequest(pickerComponent.netId, pickerComponent.PickerType);
            }
        }
        
        protected override void SendCollectRequest(uint pickerId, PickerType pickerType)
        {
            if (isLocalPlayer)
            {
                _mirrorNetworkMessageHandler.SendToServer(new MirrorPickerPickUpCollectMessage(pickerId, collectId));
            }
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