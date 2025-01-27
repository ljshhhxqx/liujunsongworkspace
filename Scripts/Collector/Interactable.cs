using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Tool.GameEvent;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

/// <summary>
/// 用于可拾取物品的碰撞体上
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class Interactable : MonoBehaviour
{
    [SerializeField] private CollectObject collectObject;
    private GameEventManager gameEventManager;

    [Inject]
    private void Init(GameEventManager gameEventManager)
    {
        this.gameEventManager = gameEventManager;
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.GetComponent<Picker>())
        {
            //this.gameEventManager.Publish(new GameInteractableEffect(other.gameObject, collectObject, true));
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.GetComponent<Picker>())
        {
            //this.gameEventManager.Publish(new GameInteractableEffect(other.gameObject, collectObject, false));
        }
    }
}