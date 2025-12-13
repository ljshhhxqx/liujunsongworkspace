using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Game.Map;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PlayerInput;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
using MemoryPack;
using Mirror;
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
        public PickerType PickerType { get; set; }
        private GameEventManager _gameEventManager;
        private InteractSystem _interactSystem;
        private PlayerInGameManager _playerInGameManager;
        private IColliderConfig _colliderConfig;
        private HashSet<DynamicObjectData> _cachedCollects = new HashSet<DynamicObjectData>();
        protected override bool AutoInjectClient => false;

        private readonly HashSet<uint> _collects = new HashSet<uint>();
    
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _interactSystem = FindObjectOfType<InteractSystem>();
            _playerInGameManager = FindObjectOfType<PlayerInGameManager>();
            _colliderConfig = GamePhysicsSystem.CreateColliderConfig(GetComponent<Collider>());

            Observable.EveryFixedUpdate()
                .Where(_ => LocalPlayerHandler)
                .Subscribe(_ =>
                {
                    if(GameObjectContainer.Instance.DynamicObjectIntersects(netId, transform.position, _colliderConfig, _cachedCollects))
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
                    case ObjectType.Chest:
                        _collects.Add(collect.NetId);
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
                // if (!_interactSystem.IsItemCanPickup(itemId))
                // {
                //     Debug.Log($"Pickup Item {itemId} not found or can't be picked up");
                //     return;
                // }
                Debug.Log($"Send Collect Request: {pickerId} {pickerType} {itemId} {itemClass}");
                var request = new SceneInteractRequest
                {
                    Header = InteractSystem.CreateInteractHeader(PlayerInGameManager.Instance.GetPlayerId(pickerId), InteractCategory.PlayerToScene,
                        transform.position, CommandAuthority.Client),
                    InteractionType = InteractionType.PickupItem,
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

        // private void OnInteractionStateChange(GameInteractableEffect interactableObjectEffectEventEvent)
        // {
        //     if (interactableObjectEffectEventEvent.Picker.GetInstanceID() != gameObject.GetInstanceID()) return;
        //     if (interactableObjectEffectEventEvent.IsEnter)
        //     {
        //         _collects.Add(interactableObjectEffectEventEvent.CollectObject);
        //     }
        //     else
        //     {
        //         _collects.Remove(interactableObjectEffectEventEvent.CollectObject);
        //     }
        // }

        private void Update()
        {
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
                default:
                    Debug.LogError("PickerType is not set");
                    break;
            }
        }

        private void PerformPickup()
        {
            foreach (var collect in _collects)
            {
                Collect(collect).Forget();
            }
        }

        private async UniTaskVoid Collect(uint sceneItemId)
        {
            if(!LocalPlayerHandler) return;
            
            var request = new SceneInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(_playerInGameManager.LocalPlayerId, InteractCategory.PlayerToScene,
                    transform.position, CommandAuthority.Client),
                InteractionType = InteractionType.PickupChest,
                SceneItemId = sceneItemId,
            };
            var json = MemoryPackSerializer.Serialize(request);
            CmdOpenChest(json);
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Chest, transform.position, transform);
            await UniTask.DelayFrame(1);
        }
        
        [Command]
        private void CmdOpenChest(byte[] data)
        {
            var request = BoxingFreeSerializer.MemoryDeserialize<SceneInteractRequest>(data);
            _interactSystem.EnqueueCommand(request);
            RpcPlayChest();
        }
        
        [ClientRpc]
        private void RpcPlayChest()
        {
            if (LocalPlayerHandler) return;
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Chest, transform.position, transform);
        }
    }
}