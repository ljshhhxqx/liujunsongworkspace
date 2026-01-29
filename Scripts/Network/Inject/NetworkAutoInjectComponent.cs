using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Data;
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

        private MapType _mapType;
        
        public int ConnectionID => netIdentity.connectionToClient.connectionId;
        public string PlayerId { get;set; }

        private void Start()
        {
            _mapType = (MapType)GameLoopDataModel.GameSceneName.Value;
            if (autoInject && isForLocalPlayer)
            {
                ObjectInjectProvider.Instance.InjectMap(_mapType, this);
                
                OnInject();
            }
        }

        protected virtual void OnInject()
        {
            
        }
    }
}