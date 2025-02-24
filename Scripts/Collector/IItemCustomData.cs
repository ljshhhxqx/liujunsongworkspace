using HotUpdate.Scripts.Config.ArrayConfig;
using MemoryPack;

namespace HotUpdate.Scripts.Collector
{
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(CollectItemCustomData))]
    [MemoryPackUnion(1, typeof(ChestItemCustomData))]
    public partial interface IItemCustomData
    {
        byte[] Serialize();
        IItemCustomData Deserialize(byte[] data);
    }
    
    [MemoryPackable]
    public partial struct CollectItemCustomData : IItemCustomData
    {
        [MemoryPackOrder(0)]
        public CollectObjectBuffSize BuffSize;
        [MemoryPackOrder(1)]
        public int BuffId;
        [MemoryPackOrder(2)]
        public int RandomBuffId;
        
        public byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(this);
        }

        public IItemCustomData Deserialize(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<CollectItemCustomData>(data);
        }
    }
    
    [MemoryPackable]
    public partial struct ChestItemCustomData : IItemCustomData
    {
        [MemoryPackOrder(0)]
        public ChestType ChestType;
        
        public byte[] Serialize()
        {
            return MemoryPackSerializer.Serialize(this);
        }

        public IItemCustomData Deserialize(byte[] data)
        {
            return MemoryPackSerializer.Deserialize<ChestItemCustomData>(data);
        }
    }

    public static class ItemCustomDataExtension
    {
        private static readonly MemoryPackSerializerOptions SerializerOptions = 
            MemoryPackSerializerOptions.Utf8;

        public static T GetCustomData<T>(this CollectItemMetaData meta) where T : IItemCustomData
        {
            if (meta.ExtraData == null || meta.ExtraData.Length == 0)
                return default;

            return MemoryPackSerializer.Deserialize<T>(meta.ExtraData, SerializerOptions);
        }

        public static void SetCustomData<T>(ref this CollectItemMetaData meta, T data) where T : IItemCustomData
        {
            meta.ExtraData = MemoryPackSerializer.Serialize(data, SerializerOptions);
        }
    }
}