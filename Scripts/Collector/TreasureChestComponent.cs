using System;
using Config;
using Cysharp.Threading.Tasks;
using Mirror;
using Sirenix.OdinInspector;
using Tool.Message;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using VContainer;

namespace Collector
{
    public class TreasureChestComponent : NetworkBehaviour
    {
        [SerializeField] 
        private GameObject lid; // 宝箱盖子
        [SerializeField] 
        private Collider collider;
        private CollectObjectData _collectObjectData;
        private ChestDataConfig _chestDataConfig;
        private MessageCenter _messageCenter;
        private ChestCommonData _chestCommonData;
        //public CollectObjectData CollectData => _chestDataConfig.ChestConfigData;
        //public Collider Collider => collider;
        private void Collect(int pickerId, PickerType pickerType)
        {
            _messageCenter.Post(new PlayerCollectChestMessage(1, CollectType.TreasureChest));
        }

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _chestDataConfig = configProvider.GetConfig<ChestDataConfig>();
            _chestCommonData = _chestDataConfig.GetChestCommonData();
            lid.transform.eulerAngles = _chestCommonData.InitEulerAngles;
            collider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver)
                .AddTo(this);
        }
    
        private void OnTriggerEnterObserver(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }

            if (Input.GetButtonDown("Collect"))
            {
                Debug.Log("TreasureChest OnTriggerEnterObserver Collect F！！！！！");
        
                if (other.TryGetComponent<Picker>(out var pickerComponent))
                {
                    OpenLid().Forget();
                    Collect(pickerComponent.UID, pickerComponent.PickerType);
                }
            }
        }
        
        [Button("开箱")]
        private void OpenChest()
        {
            OpenLid().Forget();
        }

        private async UniTaskVoid OpenLid()
        {
            collider.enabled = false;
            // 计算开启动画的目标角度
            var targetRotation = Quaternion.Euler(0, lid.transform.rotation.eulerAngles.y, lid.transform.rotation.eulerAngles.z);
        
            // 当宝箱盖子没有完全打开时
            while (Quaternion.Angle(lid.transform.rotation, targetRotation) > 0.01f)
            {
                lid.transform.rotation = Quaternion.Slerp(lid.transform.rotation, targetRotation, Time.deltaTime * _chestCommonData.OpenSpeed);
            }

            // 等待0.25秒
            await UniTask.Delay(TimeSpan.FromSeconds(0.25f));
            #if UNITY_EDITOR
            gameObject.SetActive(false);
            Debug.Log("宝箱盖子打开！！！！！");
            #else
            // 宝箱消失
            Destroy(gameObject);
            #endif
        }

    }
}
