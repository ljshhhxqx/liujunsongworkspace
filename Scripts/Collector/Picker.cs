using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Tool.ECS;
using AOTScripts.Tool.GameEvent;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Audio;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Game.Inject;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.GameEvent;
using MemoryPack;
using Mirror;
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
        
        public void SendCollectRequest(uint pickerId, PickerType pickerType, uint itemId, CollectObjectClass itemClass)
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
            if (isLocalPlayer)
            {
                return;
            }
            PlayAudio((CollectObjectClass)itemClass);
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
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Chest, transform.position, transform);
            await UniTask.DelayFrame(1);
        }
        
        [Command]
        private void CmdOpenChest(byte[] data)
        {
            var request = MemoryPackSerializer.Deserialize<SceneInteractRequest>(data);
            _interactSystem.EnqueueCommand(request);
            RpcPlayChest();
        }
        
        [ClientRpc]
        private void RpcPlayChest()
        {
            if (isLocalPlayer) return;
            GameAudioManager.Instance.PlaySFX(AudioEffectType.Chest, transform.position, transform);
        }
    }
}