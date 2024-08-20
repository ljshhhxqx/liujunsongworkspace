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
            _messageCenter.Register<PlayerTouchedCollectMessage>(MessageType.PlayerTouchedCollectable, OnPlayerTouchedCollect);
            _messageCenter.Register<PlayerCollectChestMessage>(MessageType.PlayerTouchedChest, OnPlayerTouchedChest);
        }

        private void OnPlayerTouchedChest(PlayerCollectChestMessage playerCollectChestMessage)
        {
            
        }

        private void OnPlayerTouchedCollect(PlayerTouchedCollectMessage playerTouchedCollectMessage)
        {
            
        }
    }
}