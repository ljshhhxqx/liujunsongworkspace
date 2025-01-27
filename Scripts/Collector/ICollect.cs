using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Collector
{
    /// <summary>
    /// 所有可被拾取的物品都应该继承该接口
    /// </summary>
    public interface ICollect
    {
        Collider Collider { get; }
    }

    public abstract class CollectObject : NetworkMonoController, ICollect
    {
        [SyncVar] public int CollectId;
        public abstract Collider Collider { get; }
        protected abstract void SendCollectRequest(uint pickerId, PickerType pickerType);

        // protected virtual void Awake()
        // {
        //     ObjectInjectProvider.Instance.Inject(this);
        // }
    }

    public interface IPickable
    {
        public void RequestPick(int connectionId);
    }
}