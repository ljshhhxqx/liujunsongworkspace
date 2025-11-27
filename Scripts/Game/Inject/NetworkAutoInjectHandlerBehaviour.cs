using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Data;

namespace HotUpdate.Scripts.Game.Inject
{
    public abstract class NetworkAutoInjectHandlerBehaviour : NetworkHandlerBehaviour
    {
        protected virtual bool AutoInjectClient => true;
        protected virtual bool AutoInjectServer => true;
        protected virtual bool AutoInjectLocalPlayer => true;
        protected MapType MapType;

        private void Awake()
        {
            MapType = GameLoopDataModel.GameSceneName.Value;
        }

        protected override void StartClient()
        {
            base.StartClient();
            if (!AutoInjectClient)
            {
                InjectClientCallback();
                return;
            }

            ObjectInjectProvider.Instance.InjectMap(MapType, this);
            InjectClientCallback();
        }
        
        protected override void StartServer()
        {
            base.StartServer();
            if (!AutoInjectServer)
            {
                InjectServerCallback();
                return;
            }

            ObjectInjectProvider.Instance.InjectMap(MapType, this);
            InjectServerCallback();
        }

        protected override void StartLocalPlayer()
        {
            base.StartServer();
            if (!AutoInjectLocalPlayer)
            {
                InjectLocalPlayerCallback();
                return;
            }

            ObjectInjectProvider.Instance.InjectMap(MapType, this);
            InjectLocalPlayerCallback();
        }

        protected virtual void InjectLocalPlayerCallback()
        {
            
        }

        protected virtual void InjectClientCallback()
        {
            
        }

        protected virtual void InjectServerCallback()
        {
            
        }



    }
}