using HotUpdate.Scripts.Config.ArrayConfig;
using UnityEngine;

namespace HotUpdate.Scripts.Game.Inject
{
    public interface IInjector
    {
        MapType MapType { get; }
        void Inject<T>(T objectToInject, bool includeMainScope = true, bool includeMapScope = true);
        void InjectGameObject(GameObject gameObject, bool includeMainScope = true, bool includeMapScope = true);
    }
}