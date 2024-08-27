using Config;
using Tool.Message;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using UnityEngine.Serialization;
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
    }
}