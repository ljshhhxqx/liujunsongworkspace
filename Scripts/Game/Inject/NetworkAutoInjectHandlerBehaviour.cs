using System;
using AOTScripts.Data;
using HotUpdate.Scripts.Data;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Inject
{
    public abstract class NetworkAutoInjectHandlerBehaviour : NetworkHandlerBehaviour
    {
        protected virtual bool AutoInjectClient => true;
        protected virtual bool AutoInjectServer => true;
        protected virtual bool AutoInjectLocalPlayer => true;
        protected MapType MapType;

        protected virtual void OnEnable()
        {
            MapType = (MapType)GameLoopDataModel.GameSceneName.Value;
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
            Debug.Log($"InjectClient + {MapType} + {name}");
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
            Debug.Log($"InjectServer + {MapType} + {name}");
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
            Debug.Log($"InjectLocalPlayer + {MapType} + {name}");
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