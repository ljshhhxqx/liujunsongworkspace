using Config;
using Sirenix.OdinInspector;
using System.IO;
using Tool.Message;
using UniRx;
using UniRx.Triggers;
using UnityEditor;
using UnityEngine;
using VContainer;

namespace Network.Server.Collect
{
    public class CollectInteractComponent : CollectObject
    {
        [SerializeField]
        private CollectObjectData collectObjectData;
        
        public override CollectObjectData CollectData => collectObjectData;
        public override Collider Collider => _collider;
        
        private MessageCenter _messageCenter;
        private Collider _collider;
        private CollectObjectDataConfig _collectObjectDataConfig;
        
        [Inject]
        private void Init(MessageCenter messageCenter, IConfigProvider configProvider)
        {
            _messageCenter = messageCenter;
            _collider = GetComponent<Collider>();
            _collider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver)
                .AddTo(this);
            _collectObjectDataConfig = configProvider.GetConfig<CollectObjectDataConfig>();
            collectObjectData = _collectObjectDataConfig.GetCollectObjectData(collectObjectData.CollectType);
        }

        private void OnTriggerEnterObserver(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }
            
            if (other.TryGetComponent<Picker>(out var pickerComponent))
            {
                Collect(pickerComponent.UID, pickerComponent.PickerType);
            }
        }
        
        protected override void Collect(int pickerId, PickerType pickerType)
        {
            _messageCenter.Post(new PlayerTouchedCollectMessage(CollectId, collectObjectData.CollectType));
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
                        collectObjectData = item;
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