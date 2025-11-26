using AOTScripts.Data;
using HotUpdate.Scripts.Data;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Inject
{
    public abstract class NetworkAutoInjectHandlerBehaviour : NetworkHandlerBehaviour
    {
        private MapType _mapType;

        protected virtual void Start()
        {
            _mapType = GameLoopDataModel.GameSceneName.Value;
            ObjectInjectProvider.Instance.InjectMap(_mapType, this);
        }
    }
}