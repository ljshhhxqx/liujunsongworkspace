using AOTScripts.Data;

namespace HotUpdate.Scripts.Game.Inject
{
    public abstract class NetworkAutoInjectHandlerBehaviour : NetworkHandlerBehaviour
    {
        protected virtual bool AutoInjectClient => true;
        protected virtual bool AutoInjectServer => true;
        protected virtual bool AutoInjectLocalPlayer => true;
        private MapType _mapType;

        protected override void StartClient()
        {
            base.StartClient();
            if (!AutoInjectClient)
            {
                InjectCallback();
                return;
            }

            ObjectInjectProvider.Instance.InjectMap(_mapType, this);
            InjectCallback();
        }
        
        protected override void StartServer()
        {
            base.StartServer();
            if (!AutoInjectServer)
            {
                InjectCallback();
                return;
            }

            ObjectInjectProvider.Instance.InjectMap(_mapType, this);
            InjectCallback();
        }

        protected override void StartLocalPlayer()
        {
            base.StartServer();
            if (!AutoInjectLocalPlayer)
            {
                InjectCallback();
                return;
            }

            ObjectInjectProvider.Instance.InjectMap(_mapType, this);
            InjectCallback();
        }

        protected virtual void InjectCallback()
        {
            
        }


    }
}