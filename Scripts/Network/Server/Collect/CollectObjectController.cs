using System;
using System.IO;
using Common;
using Config;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.NetworkMes;
using Network.NetworkMes;
using Network.Server.Collect;
using Sirenix.OdinInspector;
using Tool.Message;
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

        private CollectParticlePlayer _collectParticlePlayer;
        private CollectAnimationComponent _collectAnimationComponent;
        
        public override Collider Collider => _collider;
        public CollectType CollectType => collectType;
        public CollectObjectData CollectObjectData { get; private set; }

        private MessageCenter _messageCenter;
        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private Collider _collider;
        private CollectObjectDataConfig _collectObjectDataConfig;
        private IConfigProvider _configProvider;
        
        [Inject]
        private void Init(MessageCenter messageCenter, IConfigProvider configProvider, MirrorNetworkMessageHandler mirrorNetworkMessageHandler)
        {
            _messageCenter = messageCenter;
            _configProvider = configProvider;
            _collectParticlePlayer = GetComponentInChildren<CollectParticlePlayer>();
            _collectAnimationComponent = GetComponent<CollectAnimationComponent>();
            _mirrorNetworkMessageHandler = mirrorNetworkMessageHandler;
            _collider = GetComponent<Collider>();
            _collider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver)
                .AddTo(this);
        }

        public CollectObjectData GetCollectObjectData()
        {
            if (collectType != CollectObjectData?.CollectType)
            {
                CollectObjectData = _collectObjectDataConfig.GetCollectObjectData(collectType);
            }
            return CollectObjectData;
        }

        private void OnTriggerEnterObserver(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }
            
            if (other.TryGetComponent<Picker>(out var pickerComponent))
            {
                SendCollectRequest(pickerComponent.ConnectionID, pickerComponent.PickerType);
            }
        }
        
        protected override void SendCollectRequest(int pickerId, PickerType pickerType)
        {
            _mirrorNetworkMessageHandler.SendMessage(new MirrorPickerPickUpCollectMessage(pickerId, CollectId));
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