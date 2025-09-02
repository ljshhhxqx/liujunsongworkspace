using System;
using HotUpdate.Scripts.Config.ArrayConfig;
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

        [SerializeField] 
        private MapType mapType = MapType.Town;
        
        public int ConnectionID => netIdentity.connectionToClient.connectionId;
        public string PlayerId { get;set; }

        private void Awake()
        {
            if (autoInject && isForClient)
            {
                ObjectInjectProvider.Instance.InjectMap(mapType, this);
                
                OnInject();
            }
        }

        protected virtual void OnInject()
        {
            
        }
    }
}