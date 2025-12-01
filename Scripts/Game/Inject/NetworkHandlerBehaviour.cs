using Mirror;

namespace HotUpdate.Scripts.Game.Inject
{
    public abstract class NetworkHandlerBehaviour : NetworkBehaviour
    {
        public bool ServerHandler { get; protected set; }
        public bool ClientHandler { get; protected set; }
        public bool LocalPlayerHandler { get; protected set; }

        public override void OnStartClient()
        {
            base.OnStartClient();
            ClientHandler = true;
            StartServer();
        }
        
        public override void OnStartServer()
        {
            base.OnStartServer();
            ServerHandler = true;
            StartClient();
        }
        
        public override void OnStartLocalPlayer()
        {
            base.OnStartLocalPlayer();
            LocalPlayerHandler = true;
            StartLocalPlayer();
        }

        protected virtual void StartServer()
        {
        }
    
        protected virtual void StartClient()
        {
        }
        protected virtual void StartLocalPlayer()
        {
        }
    }
}