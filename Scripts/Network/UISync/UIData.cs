using MemoryPack;

namespace HotUpdate.Scripts.Network.UISync
{
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PropertyData))]
    public partial interface IUIData
    {
        public UISyncDataType SyncDataType { get; }
    }

    [MemoryPackable]
    public partial struct PropertyData : IUIData
    {
        public UISyncDataType SyncDataType => UISyncDataType.PlayerPropertyChange;
        [MemoryPackOrder(0)]
        public PropertyTypeEnum Type;
        [MemoryPackOrder(1)]
        public float Value;
    }
    
    // Add more data types here...
}