using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Game.GamePlay;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
using HotUpdate.Scripts.UI.UIBase;
using HotUpdate.Scripts.UI.UIs.Overlay;
using MemoryPack;
using Mirror;
using UI.UIBase;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector
{
    /// <summary>
    /// 挂载在拾取者身上，与拾取者的控制逻辑解耦
    /// </summary>
    public class Picker : NetworkAutoInjectHandlerBehaviour
    {
        public bool IsTouching { get; set; }

        public PickerType PickerType { get; set; }
        private GameEventManager _gameEventManager;
        private InteractSystem _interactSystem;
        private MapElementData _collectData;
        private PlayerInGameManager _playerInGameManager;
        private IColliderConfig _colliderConfig;
        private UIManager _uiManager;
        private PlayerAnimationOverlay _playerPropertiesOverlay;
        private HashSet<DynamicObjectData> _cachedCollects = new HashSet<DynamicObjectData>();
        protected override bool AutoInjectClient => false;

        private readonly HashSet<DynamicObjectData> _collects = new HashSet<DynamicObjectData>();
    
        [Inject]
        private void Init(GameEventManager gameEventManager, IObjectResolver objectResolver, UIManager uiManager, 
            IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
            _gameEventManager = gameEventManager;
            _collectData = configProvider.GetConfig<JsonDataConfig>().CollectData.mapElementData;
            _interactSystem = objectResolver.Resolve<InteractSystem>();
            _playerInGameManager = playerInGameManager;
            _uiManager = uiManager;
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());

            Observable.EveryFixedUpdate()
                .Where(_ => LocalPlayerHandler && GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, _colliderConfig, _cachedCollects))
                .Subscribe(_ =>
                {
                    HandlePlayerTouched();
                })
                .AddTo(this);
            Debug.Log($"Picker Init----{_interactSystem}");
        }

        private void HandlePlayerTouched()
        {
            // foreach (var collect in _cachedCollects)
            // {
            //     Debug.Log($"Picker HandlePlayerTouched - {collect.NetId}-{collect.Type}-{collect.Position}");
            // }
            _collects.Clear();
            if (_cachedCollects.Count == 0)
            {
                return;
            }

            foreach (var collect in _cachedCollects)
            {
                if (!NetworkClient.spawned.TryGetValue(collect.NetId, out var identity))
                {
                    //Debug.LogWarning($"Identity not found for netId {collect.NetId}");
                    continue;
                }

                switch (collect.Type)
                {
                    case ObjectType.Collectable:
                        var collectObjectController = identity.GetComponent<CollectObjectController>();
                        SendCollectRequest(netId, PickerType, collect.NetId, collectObjectController.CollectObjectData.collectObjectClass);
                        break;
                    case ObjectType.Well:
                        if (identity.TryGetComponent<Well>(out var well))
                        {
                            if (!well.CanTouchWell())
                            {
                                return;
                            }
                            _collects.Add(collect);
                        }
                        break;
                    case ObjectType.Chest:
                    case ObjectType.Rocket:
                    case ObjectType.Train:
                        _collects.Add(collect);
                        break;
                }
            }
        }

        private void OnDestroy()
        {
            _collects.Clear();   
            GameObjectContainer.Instance.RemoveDynamicObject(netId);
        }
        
        public void SendCollectRequest(uint pickerId, PickerType pickerType, uint itemId, CollectObjectClass itemClass)
        {
            if (LocalPlayerHandler)
            {
                if (!_interactSystem.IsItemCanPickup(itemId))
                {
                    //Debug.Log($"Pickup Item {itemId} not found or can't be picked up");
                    return;
                }
                //Debug.Log($"Send Collect Request: {pickerId} {pickerType} {itemId} {itemClass}");
                var request = new SceneInteractRequest
                {
                    Header = InteractSystem.CreateInteractHeader(_playerInGameManager.GetPlayerId(pickerId), InteractCategory.PlayerToScene,
                        transform.position, CommandAuthority.Client),
                    InteractionType = (int)InteractionType.PickupItem,
                    SceneItemId = itemId,
                };
                var commandBytes = MemoryPackSerializer.Serialize(request);
                CmdCollect(commandBytes);
            }
        }

        private void PlayAudio(CollectObjectClass itemClass)
        {
            switch (itemClass)
            {
                case CollectObjectClass.Score:
                    GameAudioManager.Instance.PlaySFX(AudioEffectType.Gem, transform.position, transform);
                    break;
                case CollectObjectClass.Gold:
                    GameAudioManager.Instance.PlaySFX(AudioEffectType.Gold, transform.position, transform);
                    break;
                case CollectObjectClass.Buff:
                    GameAudioManager.Instance.PlaySFX(AudioEffectType.Drug, transform.position, transform);
                    break;
            }
        }
        
        [Command]
        private void CmdCollect(byte[] request)
        {
            var data = BoxingFreeSerializer.MemoryDeserialize<SceneInteractRequest>(request);
            _interactSystem.EnqueueCommand(data);
            //RpcPlayEffect(itemClass);
        }

        [ClientRpc]
        public void RpcPlayEffect(int itemClass)
        {
            PlayAudio((CollectObjectClass)itemClass);
        }

        private void Update()
        {
            if (!LocalPlayerHandler)
            {
                return;
            }

            switch (PickerType)
            {
                case PickerType.Player:
                    if (Input.GetKeyDown(KeyCode.F))
                    {
                        PerformPickup();
                    }
                    break;
                case PickerType.Computer:
                    PerformPickup();
                    break;
            }
        }

        private void PerformPickup()
        {
            if (!_playerPropertiesOverlay)
            {
                _playerPropertiesOverlay = _uiManager.GetActiveUI<PlayerAnimationOverlay>(UIType.PlayerAnimationOverlay, UICanvasType.Overlay);
            }
            foreach (var collect in _collects)
            {
                IsTouching = true;
                var time = _collectData.GetTouchTime(collect.Type);
                _playerPropertiesOverlay.StartProgress($"收集{collect.Type}中...需要{time}秒 ", time, () => OnComplete(collect) , GetIsTouching);
            }
            _collects.Clear();
        }

        private void OnComplete(DynamicObjectData collect)
        {
            Debug.Log($"PICKER : PerformPickup {collect.NetId} {collect.Type}");
            Collect(collect).Forget();
            IsTouching = false;
        }

        private bool GetIsTouching()
        {
            return IsTouching;
        }

        private static InteractionType GetInteractionType(ObjectType objectType)
        {
            return objectType switch
            {
                ObjectType.Chest => InteractionType.PickupChest,
                ObjectType.Well => InteractionType.TouchWell,
                ObjectType.Train => InteractionType.TouchTrain,
                ObjectType.Rocket => InteractionType.TouchRocket,
                _ => InteractionType.PickupItem,
            };
        }

        private async UniTaskVoid Collect(DynamicObjectData collect)
        {
            if(!LocalPlayerHandler) return;
            
            var request = new SceneInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(_playerInGameManager.LocalPlayerId, InteractCategory.PlayerToScene,
                    transform.position, CommandAuthority.Client),
                InteractionType = (int)GetInteractionType(collect.Type),
                SceneItemId = collect.NetId,
            };
            Debug.Log($"PICKER UniTaskVoid: Collect {collect.NetId} {request.InteractionType}");
            var json = MemoryPackSerializer.Serialize(request);
            CmdSendInteract(json);
            await UniTask.DelayFrame(1);
        }
        
        [Command]
        private void CmdSendInteract(byte[] data)
        {
            var request = BoxingFreeSerializer.MemoryDeserialize<SceneInteractRequest>(data);
            _interactSystem.EnqueueCommand(request);
        }

        public void AddPicked(uint pickupId)
        {
            //_pickerCollects.Add(pickupId);
        }
    }
}