using HotUpdate.Scripts.Tool.Coroutine;
using HotUpdate.Scripts.Tool.Message;
using Mirror;
using Model;
using Tool.Message;
using VContainer;

namespace Network.Server
{
    public abstract class ServerSystemBase : NetworkBehaviour
    {
        protected PlayersGameModelManager playersGameModelManager;
        protected IConfigProvider configProvider;
        protected MessageCenter messageCenter;

        [Inject]
        protected virtual void Init(PlayersGameModelManager playersGameModelManager, 
            IConfigProvider configProvider, MessageCenter messageCenter)
        {                                          
            this.playersGameModelManager = playersGameModelManager;
            this.configProvider = configProvider;
            this.messageCenter = messageCenter;
            InitCallback();
        }

        private void Update()
        {
            UpdateCallback();
        }
        
        private void OnDestroy()
        {
            DestroyCallback();
        }

        protected abstract void InitCallback();
        protected abstract void UpdateCallback();
        protected abstract void DestroyCallback();
    }
}