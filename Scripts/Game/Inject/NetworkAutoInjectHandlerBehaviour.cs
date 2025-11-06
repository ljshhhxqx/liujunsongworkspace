using AOTScripts.Data;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Inject
{
    public abstract class NetworkAutoInjectHandlerBehaviour : NetworkHandlerBehaviour
    {
        [SerializeField] 
        private MapType mapType = MapType.Town;

        private void Start()
        {
            ObjectInjectProvider.Instance.InjectMap(mapType, this);
        }
    }
}