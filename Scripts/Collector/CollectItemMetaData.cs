using System;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Collector
{
    [MemoryPackable]
    public partial struct CollectItemMetaData
    {
        // 使用32位唯一标识符（节省空间）
        [MemoryPackOrder(0)]
        public uint ItemId;

        // 使用压缩坐标（精度0.01米，范围±1600米）
        [MemoryPackOrder(1)]
        public CompressedVector3 Position;

        // 使用位掩码存储状态（0b00000001:是否激活, 0b00000010:是否已收集）
        [MemoryPackOrder(2)]
        public byte StateFlags;

        // 物品ConfigId
        [MemoryPackOrder(3)]
        public int ItemConfigId;

        // 服务器生成帧
        [MemoryPackOrder(4)]
        public uint SpawnTick;

        // 存在时间（单位秒，最大655秒）
        [MemoryPackOrder(5)]
        public ushort Lifetime;

        // 随机种子（客户端生成一致随机效果）
        [MemoryPackOrder(6)]
        public ushort RandomSeed;

        // 所属玩家ID（使用NetworkConnection的ID）
        [MemoryPackOrder(7)]
        public int OwnerId;

        // 物品buff的大中小   
        [MemoryPackOrder(8)]
        public CollectObjectBuffSize BuffSize;
        
        [MemoryPackOrder(9)]
        public int RandomBuffId;
        
        [MemoryPackOrder(10)]
        public int BuffId;
        
        // 扩展属性（使用动态编码）
        [MemoryPackOrder(9)]
        public byte[] CustomData;

        public CollectItemMetaData(uint itemId, Vector3 position, byte stateFlags, int itemConfigId, uint spawnTick, ushort lifetime, ushort randomSeed, int ownerId, CollectObjectBuffSize buffSize, int randomBuffId, int buffId, byte[] customData = null)
        {
            ItemId = itemId;
            Position = CompressedVector3.FromVector3(position);
            StateFlags = stateFlags;
            ItemConfigId = itemConfigId;
            SpawnTick = spawnTick;
            Lifetime = lifetime;
            RandomSeed = randomSeed;
            OwnerId = ownerId;
            BuffSize = buffSize;
            CustomData = customData;
            RandomBuffId = randomBuffId;
            BuffId = buffId;
        }

        public bool IsActive => (StateFlags & 0x01) != 0;
        public bool IsCollected => (StateFlags & 0x02) != 0;
   
        public void SetStateActive() => StateFlags |= 0x01;
        public void SetCollected() => StateFlags |= 0x02;
        
        public float GetClientRemainingTime(float clientTime, float serverTickRate)
        {
            var serverTime = SpawnTick * serverTickRate + Lifetime;
            return Mathf.Max(0, serverTime - clientTime);
        }
        
        public static uint GenerateItemId(Vector3 position)
        {
            var x = (ushort)(position.x * 100);
            var z = (ushort)(position.z * 100);
            return (uint)((x << 16) | z);
        }
    }
    
    [MemoryPackable]
    public partial struct CompressedVector3
    {
        [MemoryPackOrder(0)]
        public short x; // 精度0.01，范围±327.67米
        [MemoryPackOrder(1)]
        public short y;
        [MemoryPackOrder(2)]
        public short z;

        public Vector3 ToVector3() => new(x * 0.01f, y * 0.01f, z * 0.01f);
        public static CompressedVector3 FromVector3(Vector3 v) => new()
        {
            x = (short)(v.x * 100),
            y = (short)(v.y * 100),
            z = (short)(v.z * 100)
        };
    }
    
    [Flags]
    public enum ItemState : byte
    {
        None = 0,
        //存在于场景中
        IsActive = 1 << 0,    // 00000001
        //已被收集
        IsCollected = 1 << 1, // 00000010
        //锁定状态
        IsLocked = 1 << 2,    // 00000100
        // 可继续扩展其他状态...
    }

    public static class CollectItemMetaDataExtensions
    {
        // 是否拥有指定状态
        public static bool HasState(this CollectItemMetaData metaData, ItemState state)
        {
            return (metaData.StateFlags & (byte)state) != 0;
        }
        
        // 添加状态
        public static CollectItemMetaData AddState(this CollectItemMetaData metaData, ItemState state)
        {
            var data = metaData;
            data.StateFlags |= (byte)state;
            return data;
        }
        
        // 移除状态
        public static CollectItemMetaData RemoveState(this CollectItemMetaData metaData, ItemState state)
        {
            var data = metaData;
            data.StateFlags &= (byte)~state;
            return data;
        }
        
        // 切换状态
        public static CollectItemMetaData ToggleState(this CollectItemMetaData metaData, ItemState state)
        {
            var data = metaData;
            data.StateFlags &= (byte)~state;
            return data;
        }
        
    }


    public enum ColliderType
    {
        Box,
        Sphere,
        Capsule,
        Mesh,
    }
}