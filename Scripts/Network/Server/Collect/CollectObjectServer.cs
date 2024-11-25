using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Tool.Message;
using VContainer;

namespace Network.Server.Collect
{
    public class CollectObjectServer : NetworkBehaviour
    {
        private MessageCenter _messageCenter;
        
        [Inject]
        private void Init(MessageCenter messageCenter)
        {
            _messageCenter = messageCenter;
            _messageCenter.Register<PlayerTouchedCollectMessage>(OnPlayerTouchedCollect);
            _messageCenter.Register<PlayerCollectChestMessage>(OnPlayerTouchedChest);
        }

        private void OnPlayerTouchedChest(PlayerCollectChestMessage playerCollectChestMessage)
        {
            
        }

        private void OnPlayerTouchedCollect(PlayerTouchedCollectMessage playerTouchedCollectMessage)
        {
            
        }
    }
}