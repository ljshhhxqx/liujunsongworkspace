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

        private readonly HashSet<uint> _collects = new HashSet<uint>();
    
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _interactSystem = FindObjectOfType<InteractSystem>();
            _playerInGameManager = FindObjectOfType<PlayerInGameManager>();
            _colliderConfig = GetComponent<PlayerComponentController>().ColliderConfig;

            Observable.EveryFixedUpdate()
                .Where(_ => LocalPlayerHandler && GameObjectContainer.Instance.DynamicObjectIntersects(transform.position, _colliderConfig, _cachedCollects))
                .Subscribe(_ =>
                {
                    HandlePlayerTouched();
                })
                .AddTo(this);
            Debug.Log($"Picker Init----{_interactSystem}");
        }

        private void HandlePlayerTouched()
        {
            if (_cachedCollects.Count == 0)
            {
                if (_collects.Count > 0)
                    _collects.Clear();
                return;
            }
            _collects.RemoveWhere(x => _cachedCollects.All(y => y.NetId != x));
            foreach (var collect in _cachedCollects)
            {
                if (collect.Type == ObjectType.Collectable)
                {
                    var identity = NetworkClient.spawned[collect.NetId];
                    var collectObjectController = identity.GetComponent<CollectObjectController>();
                    SendCollectRequest(netId, PickerType, collect.NetId, collectObjectController.CollectObjectData.collectObjectClass);
                }
                else if (collect.Type == ObjectType.Chest)
                {
                    _collects.Add(collect.NetId);
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
                var request = new SceneInteractRequest
                {
                    Header = InteractSystem.CreateInteractHeader(PlayerInGameManager.Instance.GetPlayerId(pickerId), InteractCategory.PlayerToScene,
                        transform.position, CommandAuthority.Client),
                    InteractionType = InteractionType.PickupItem,
                    SceneItemId = itemId,
                };
                PlayAudio(itemClass);
                var commandBytes = MemoryPackSerializer.Serialize(request);
                CmdCollect(commandBytes, (int)itemClass);
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
        private void CmdCollect(byte[] request, int itemClass)
        {
            var data = MemoryPackSerializer.Deserialize<SceneInteractRequest>(request);
            _interactSystem.EnqueueCommand(data);
            RpcPlayEffect(itemClass);
        }

        [ClientRpc]
        private void RpcPlayEffect(int itemClass)
        {
            if (LocalPlayerHandler)
            {
                return;
            }
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