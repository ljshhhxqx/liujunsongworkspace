using AOTScripts.Tool.ECS;
using HotUpdate.Scripts.Config;
using HotUpdate.Scripts.Config.ArrayConfig;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;

namespace HotUpdate.Scripts.Collector
{
    /// <summary>
    /// 所有可被拾取的物品都应该继承该接口
    /// </summary>
    public interface ICollect
    {
        Collider Collider { get; }
    }

    public interface IItem
    {
        public uint ItemId { get; }
    }

    public abstract class CollectObject : NetworkMonoController, ICollect, IItem
    {
        [HideInInspector]
        [SyncVar] 
        public uint collectId;
        [SerializeField]
        private QualityType quality;
        public QualityType Quality => quality;
        public abstract Collider Collider { get; }
        protected abstract void SendCollectRequest(uint pickerId, PickerType pickerType);

        // protected virtual void Awake()
        // {
        //     ObjectInjectProvider.Instance.Inject(this);
        // }
        public uint ItemId { get; set; }
    }

    public interface IPickable
    {
        public void RequestPick(int pickerConnectionId);
        uint SceneItemId { get; }  
    }
}