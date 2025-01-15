using AOTScripts.Tool.ECS;
using Mirror;
using UnityEngine;
using VContainer;
using VContainer.Unity;

namespace HotUpdate.Scripts.Network.Server
{
    public class NetworkInjectManager : ServerNetworkComponent
    {
        private IObjectResolver _resolver;
        
        [Inject]
        private void Init(IObjectResolver resolver)
        {
            _resolver = resolver;
        }

        private void Inject<T>(T obj)
        {
            _resolver.Inject(obj);
        }

        private void InjectGameObject(NetworkIdentity obj)
        {
            _resolver.InjectGameObject(obj.gameObject);
        }
    }
}