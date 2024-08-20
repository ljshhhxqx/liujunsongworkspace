using System;
using System.Collections;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

/// <summary>
/// 挂载在拾取者身上，与拾取者的控制逻辑解耦
/// </summary>
public class Picker : MonoBehaviour
{
    public int UID { get; set; }

    public PickerType PickerType { get; set; }
    private GameEventManager gameEventManager;

    private List<CollectObject> collects = new List<CollectObject>();
    
    [Inject]
    private void Init(GameEventManager gameEventManager)
    {
        this.gameEventManager = gameEventManager;
        this.gameEventManager.Subscribe<GameInteractableEffect>(OnInteractionStateChange);
    }

    private void OnDestroy()
    {
        //gameEventManager.Unsubscribe<InteractableObjectEffectEvent>(OnInteractionStateChange);
        collects.Clear();   
    }

    private void OnInteractionStateChange(GameInteractableEffect interactableObjectEffectEventEvent)
    {
        if (interactableObjectEffectEventEvent.Picker.GetInstanceID() != gameObject.GetInstanceID()) return;
        if (interactableObjectEffectEventEvent.IsEnter)
        {
            collects.Add(interactableObjectEffectEventEvent.CollectObject);
        }
        else
        {
            collects.Remove(interactableObjectEffectEventEvent.CollectObject);
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
            default:
                PerformPickup();
                break;
        }
    }

    private void PerformPickup()
    {
        foreach (var collect in collects)
        {
            Collect(collect).Forget();
        }
    }

    private async UniTaskVoid Collect(CollectObject collect)
    {
        //collect.Collect(gameObject);
        await UniTask.DelayFrame(1);
    }
}