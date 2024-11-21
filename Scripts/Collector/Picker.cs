using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using Cysharp.Threading.Tasks;
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

        private readonly List<IPickable> _collects = new List<IPickable>();
    
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
            _gameEventManager.Subscribe<GameInteractableEffect>(OnInteractionStateChange);
        }

        private void OnDestroy()
        {
            _collects.Clear();   
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
                        Debug.Log($"collect chest");
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
            collect.RequestPick(netId);
            await UniTask.DelayFrame(1);
        }
    }
}