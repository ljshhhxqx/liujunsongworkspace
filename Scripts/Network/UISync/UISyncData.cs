using System;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using MemoryPack;

namespace HotUpdate.Scripts.Network.UISync
{
    [MemoryPackable]
    public partial struct UISyncDataHeader
    {
        [MemoryPackOrder(0)] 
        public int ConnectionId;
        [MemoryPackOrder(1)]
        public int Tick;
        // 命令唯一ID（时间戳+序列号）
        [MemoryPackOrder(3)] 
        public uint CommandId;     
        [MemoryPackOrder(4)] 
        public long Timestamp;
        // 执行上下文
        [MemoryPackOrder(5)] 
        public CommandAuthority Authority;
        // 同步模式
        [MemoryPackOrder(6)] 
        public SyncMode SyncMode;
    }

    
    public enum SyncMode : byte
    {
        Immediate,      // 即时同步
        Timed,          // 定时同步
        Hybrid          // 阈值触发+定时兜底
    }
    
    [MemoryPackable]
    public partial struct UISyncData
    {
        [MemoryPackOrder(0)] 
        public UISyncDataHeader Header;
        [MemoryPackOrder(1)]
        public byte[] PayloadData;
        [MemoryPackOrder(2)] 
        public UISyncDataType SyncDataType;
        
        public ReadOnlySpan<byte> Data => PayloadData;
        
        [MemoryPackConstructor]
        public UISyncData(UISyncDataHeader header, UISyncDataType syncDataType, byte[] payloadData)
        {
            Header = header;
            PayloadData = payloadData;
            SyncDataType = syncDataType;
        }
    }

    /// <summary>
    /// 这里注册每一种刷新UI的数据类型
    /// </summary>
    public enum UISyncDataType : short
    {
        PlayerPropertyChange,
    }
}