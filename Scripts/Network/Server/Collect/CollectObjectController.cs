using System;
using System.IO;
using System.Linq;
using Config;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.NetworkMes;
using Network.NetworkMes;
using Network.Server.Collect;
using Sirenix.OdinInspector;
using UniRx;
using UniRx.Triggers;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Server.Collect
{
    public class CollectObjectController : CollectObject
    {
        [SerializeField] 
        private CollectType collectType;
        private PooledObject _pooledObject;
        private CollectParticlePlayer _collectParticlePlayer;
        private CollectAnimationComponent _collectAnimationComponent;
        private Collider _positionCollider;
        
        public override Collider Collider => _collider;
        public CollectType CollectType => collectType;
        public CollectObjectData CollectObjectData { get; private set; }

        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private Collider _collider;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private IDisposable _disposable;
        
        [Inject]
        private void Init()
        {
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
            if (collectCollider == null)
            {
                Debug.LogError("Collider not found");
                return;
            }
            _collider = collectCollider.GetComponent<Collider>();
            _collider.enabled=true;
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
            if (!other.CompareTag("Player") || !isClient)
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
            if (isClient)
            {
                _mirrorNetworkMessageHandler.SendToServer(new MirrorPickerPickUpCollectMessage(pickerId, CollectId));
            }
        }

        public void CollectSuccess()
        {
            _collectParticlePlayer.Play(_collectAnimationComponent.OutlineColorValue);
        }

        [Button("设置CollectType")]
        private void SetCollectType()
        {
            if (Enum.TryParse(gameObject.name, out CollectType type))
            {
                collectType = type;
                return;
            }
            throw new ArgumentException("GameObject name is not a valid CollectType");
        }

        [Button("重置配置数据")]
        private void SetConfigData()
        {
#if UNITY_EDITOR
            string path = Path.Combine(Application.streamingAssetsPath, "Config");
            string filePath = Path.Combine(path, $"CollectObjectDataConfig.json");
            // 检查文件是否存在
            if (File.Exists(filePath))
            {
                // 读取 JSON 文件内容
                string json = File.ReadAllText(filePath);

                CollectObjectDataConfig newInstance = ScriptableObject.CreateInstance<CollectObjectDataConfig>();

                // 将 JSON 数据应用到新的实例中
                JsonUtility.FromJsonOverwrite(json, newInstance);

                // 输出日志确认加载成功
                Debug.Log($"ScriptableObject instance created from {filePath}");

                foreach (var item in newInstance.CollectConfigDatas)
                {
                    if (item.CollectType.ToString().Equals(this.name))
                    {
                        CollectObjectData = item;
                    }
                }

                AssetDatabase.Refresh();
            }
            else
            {
                Debug.LogWarning($"File not found at {filePath}");
            }
#endif
        }
    }
}