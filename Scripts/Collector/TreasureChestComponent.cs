using System;
using Config;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Network.NetworkMes;
using Mirror;
using Network.NetworkMes;
using Sirenix.OdinInspector;
using Tool.GameEvent;
using Tool.Message;
using UniRx;
using UniRx.Triggers;
using UnityEngine;
using VContainer;

namespace Collector
{
    public class TreasureChestComponent : NetworkBehaviour, IPickable
    {
        [SerializeField] 
        private GameObject lid; // 宝箱盖子
        [SerializeField] 
        private Collider collider;
        private ChestDataConfig _chestDataConfig;
        private MessageCenter _messageCenter;
        private MirrorNetworkMessageHandler _mirrorNetworkMessageHandler;
        private ChestCommonData _chestCommonData;
        private GameEventManager _gameEventManager;
        public ChestType ChestType { get; set; }
        [SyncVar]
        public bool isPicked;

        [Inject]
        private void Init(IConfigProvider configProvider, GameEventManager gameEventManager, MirrorNetworkMessageHandler mirrorNetworkMessageHandler)
        {
            _gameEventManager = gameEventManager;
            //netId;
            _chestDataConfig = configProvider.GetConfig<ChestDataConfig>();
            _chestCommonData = _chestDataConfig.GetChestCommonData();
            _mirrorNetworkMessageHandler = mirrorNetworkMessageHandler;
            lid.transform.eulerAngles = _chestCommonData.InitEulerAngles;
            collider.OnTriggerEnterAsObservable()
                .Subscribe(OnTriggerEnterObserver)
                .AddTo(this);
            collider.OnTriggerExitAsObservable()
                .Subscribe(OnTriggerExitObserver)
                .AddTo(this);
        }
        
        private void OnTriggerExitObserver(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }
            
            _gameEventManager.Publish(new GameInteractableEffect(other.gameObject, this, false));
        }
        
        private void OnTriggerEnterObserver(Collider other)
        {
            if (!other.CompareTag("Player"))
            {
                return;
            }
            
            _gameEventManager.Publish(new GameInteractableEffect(other.gameObject, this, true));
        }
        
        [Button("开箱")]
        private void OpenChest()
        {
            OpenLid().Forget();
        }

        private async UniTask OpenLid()
        {
            collider.enabled = false;
            // 计算开启动画的目标角度
            var targetRotation = Quaternion.Euler(0, lid.transform.rotation.eulerAngles.y, lid.transform.rotation.eulerAngles.z);
        
            // 当宝箱盖子没有完全打开时
            while (Quaternion.Angle(lid.transform.rotation, targetRotation) > 0.01f)
            {
                lid.transform.rotation = Quaternion.Slerp(lid.transform.rotation, targetRotation, Time.deltaTime * _chestCommonData.OpenSpeed);
                await UniTask.Yield();
            }
        }


        public void RequestPick(int pickerId)
        {
            _mirrorNetworkMessageHandler.SendMessage(new MirrorPickerPickUpChestMessage(pickerId, (int)netId));
        }

        public void PickUpSuccess()
        {
            OpenChest();
        }
    }

    [Serializable]
    public struct ChestData
    {
        public int Id;
        public ChestType Type;
    }
}
