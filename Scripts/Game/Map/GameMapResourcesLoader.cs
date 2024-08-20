using Cysharp.Threading.Tasks;
using Tool.GameEvent;
using UnityEngine;
using VContainer;

namespace Game.Map
{
    public class GameMapResourcesLoader : MonoBehaviour, IInjectableObject
    {
        [Inject]
        private async UniTask Init(GameEventManager gameEventManager)
        {
            await ResourceManager.Instance.GetMapResource("Main");
            Debug.Log("Map resources loaded");
            gameEventManager.Publish(new GameSceneResourcesLoadedEvent("MainGame"));
        }
    }
}
