using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Tool.GameEvent;
using Mirror;
using Sirenix.OdinInspector;
using VContainer;

namespace HotUpdate.Scripts.Network.Server
{
    public class NetworkManagerRpcCaller : ServerNetworkComponent
    {
        private GameEventManager _gameEventManager;
        
        [Inject]
        private void Init(GameEventManager gameEventManager)
        {
            _gameEventManager = gameEventManager;
        }
        
        // public void SendGameReadyMessageRpc(string gameName, )
        // {
        //     _gameEventManager.Publish(new GameReadyEvent(gameName));
        // }

    }
}