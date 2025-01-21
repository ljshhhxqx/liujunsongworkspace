using HotUpdate.Scripts.Game.Inject;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Data.PredictableObject
{
    public class PredictableNetworkAutoInjectBehaviour : PredictableNetworkBehaviour
    {
        [SerializeField]
        private bool autoInject = true;
        [SerializeField]
        private bool isForClient = true;
        [SerializeField]
        private bool isForLocalPlayer = true;
        
        public int ConnectionID => netIdentity.connectionToClient.connectionId;
        public string PlayerId { get;set; }
        
        protected virtual void Start()
        {
            if (isServer && !netIdentity.isServerOnly)
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