using AOTScripts.Tool.ECS;
using Mirror;
using Tool.GameEvent;
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
        
        [ClientRpc]
        public void SendGameReadyMessageRpc()
        {
            _gameEventManager.Publish(new GameReadyEvent("MainGame"));
        }

    }
}