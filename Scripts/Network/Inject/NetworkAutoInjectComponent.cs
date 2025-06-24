using HotUpdate.Scripts.Game.Inject;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Inject
{
    public abstract class NetworkAutoInjectComponent : NetworkBehaviour
    {
        [SerializeField]
        private bool autoInject = true;
        [SerializeField]
        private bool isForClient = true;
        [SerializeField]
        private bool isForLocalPlayer = true;
        
        public int ConnectionID => netIdentity.connectionToClient.connectionId;
        public string PlayerId { get;set; }

        public override void OnStartServer()
        {
            base.OnStartServer();
            if (isServer && 
                !netIdentity.isServerOnly)
            {
                RpcInject();
            }
        }
        
        [ClientRpc]
        private void RpcInject()
        {
            if (autoInject && isForClient)
            {
                ObjectInjectProvider.Instance.Inject(this);
                if (isLocalPlayer && isForLocalPlayer)
                {
                    OnInject();
                }
            }
        }

        protected virtual void OnInject()
        {
            
        }
    }
}