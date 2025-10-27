using System;
using AOTScripts.Data;
using MemoryPack;
using Mirror;

namespace HotUpdate.Scripts.Network.UISync
{
    [MemoryPackable]
    public partial struct UISyncDataHeader : NetworkMessage
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader CommandHeader;
        [MemoryPackOrder(1)]
        public SyncMode SyncMode;
    }
    
    public enum SyncMode : byte
    {
        Immediate,      // 即时同步
        Timed,          // 定时同步
    }
    
    [MemoryPackable]
    public partial struct UISyncCommand
    {
        [MemoryPackOrder(0)] 
        public UISyncDataHeader Header;
        [MemoryPackOrder(1)]
        public byte[] CommandData;
        [MemoryPackOrder(2)] 
        public UISyncDataType SyncDataType;
        
        [MemoryPackIgnore]
        public ReadOnlySpan<byte> ReadOnlyData => CommandData;
        
        [MemoryPackConstructor]
        public UISyncCommand(UISyncDataHeader header, UISyncDataType syncDataType, byte[] commandData)
        {
            Header = header;
            CommandData = commandData;
            SyncDataType = syncDataType;
        }
    }

    /// <summary>
    /// 这里注册每一种刷新UI的数据类型
    /// </summary>
    public enum UISyncDataType : short
    {
        PlayerInventory,
    }

    //注册每一个刷新UI的请求对应的接口
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PlayerUseItemData))]
    [MemoryPackUnion(1, typeof(PlayerExchangeItemData))]
    public partial interface IUISyncCommandData
    {
        
    }
    
    [MemoryPackable]
    public partial struct PlayerUseItemData : IUISyncCommandData
    {
        [MemoryPackOrder(1)]
        public int ItemSlotIndex;

        [MemoryPackOrder(2)] 
        public int Count;
    }
    
    [MemoryPackable]
    public partial struct PlayerExchangeItemData : IUISyncCommandData
    {
        [MemoryPackOrder(1)]
        public uint ItemSlotFromIndex;
        [MemoryPackOrder(2)]
        public uint ItemSlotToIndex;
    }
}