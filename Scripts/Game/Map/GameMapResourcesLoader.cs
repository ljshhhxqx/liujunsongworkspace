using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Config;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace Game.Map
{
    public class GameMapResourcesLoader : MonoBehaviour, IInjectableObject
    {
        // [SerializeField]
        // private MapType mapType;
        //
        // [Inject]
        // private async UniTask Init(GameEventManager gameEventManager)
        // {
        //     await ResourceManager.Instance.GetMapResource(mapType.ToString());
        //     Debug.Log("Map resources loaded");
        //     gameEventManager.Publish(new GameSceneResourcesLoadedEvent(mapType.ToString()));
        // }
    }
}
