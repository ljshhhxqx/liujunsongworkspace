using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using MemoryPack;
using Mirror;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Collector
{
    /// <summary>
    /// 挂载在拾取者身上，与拾取者的控制逻辑解耦
    /// </summary>
    public class Picker : NetworkMonoComponent
    {
        public PickerType PickerType { get; set; }
        private GameEventManager _gameEventManager;
        private InteractSystem _interactSystem;
        private PlayerInGameManager _playerInGameManager;

        private readonly List<IPickable> _collects = new List<IPickable>();
    
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<GameInteractableEffect>(OnInteractionStateChange);
            _interactSystem = FindObjectOfType<InteractSystem>();
            _playerInGameManager = FindObjectOfType<PlayerInGameManager>();
            Debug.Log($"Picker Init----{_interactSystem}");
        }

        public void Start()
        {
            ObjectInjectProvider.Instance.Inject(this);
        }

        private void OnDestroy()
        {
            _collects.Clear();   
        }
        
        public void SendCollectRequest(uint pickerId, PickerType pickerType, uint itemId)
        {
            if (isLocalPlayer)
            {
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
        
        [Command]
        private void CmdCollect(byte[] request)
        {
            var data = MemoryPackSerializer.Deserialize<SceneInteractRequest>(request);
            _interactSystem.EnqueueCommand(data);
        }

        private void OnInteractionStateChange(GameInteractableEffect interactableObjectEffectEventEvent)
        {
            if (interactableObjectEffectEventEvent.Picker.GetInstanceID() != gameObject.GetInstanceID()) return;
            if (interactableObjectEffectEventEvent.IsEnter)
            {
                _collects.Add(interactableObjectEffectEventEvent.CollectObject);
            }
            else
            {
                _collects.Remove(interactableObjectEffectEventEvent.CollectObject);
            }
        }

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

        private async UniTaskVoid Collect(IPickable collect)
        {
            if(!isLocalPlayer) return;
            
            var request = new SceneInteractRequest
            {
                Header = InteractSystem.CreateInteractHeader(_playerInGameManager.LocalPlayerId, InteractCategory.PlayerToScene,
                    transform.position, CommandAuthority.Client),
                InteractionType = InteractionType.PickupChest,
                SceneItemId = collect.SceneItemId,
            };
            var json = MemoryPackSerializer.Serialize(request);
            CmdOpenChest(json);
            await UniTask.DelayFrame(1);
        }
        
        [Command]
        private void CmdOpenChest(byte[] data)
        {
            var request = MemoryPackSerializer.Deserialize<SceneInteractRequest>(data);
            _interactSystem.EnqueueCommand(request);
        }
    }
}