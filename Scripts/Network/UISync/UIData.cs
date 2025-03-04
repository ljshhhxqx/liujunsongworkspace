using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace HotUpdate.Scripts.Network.UISync
{
    /// <summary>
    /// 这里撰写UI刷新时需要的数据
    /// </summary>
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(InventoryData))]
    public partial interface IUIData
    {
        public UISyncDataType SyncDataType { get; }
    }

    [MemoryPackable]
    public partial struct InventoryData : IUIData
    {
        public UISyncDataType SyncDataType => UISyncDataType.PlayerInventory;
        
        [MemoryPackOrder(0)]
        public SlotData[] Slots;
        
        [MemoryPackConstructor]
        public InventoryData(IEnumerable<SlotData> slots)
        {
            Slots = slots.ToArray();
        }
    }
    
    [MemoryPackable]
    public partial struct SlotData
    {
        [MemoryPackOrder(0)]
        public int ItemIndex;
        [MemoryPackOrder(1)]
        public int Count;
        [MemoryPackOrder(2)]
        public int MaxCount;
        [MemoryPackOrder(3)]
        public int ItemConfigId;
    }
    

    // Add more data types here...
}