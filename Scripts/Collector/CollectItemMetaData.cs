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

        // 使用位掩码存储状态
        [MemoryPackOrder(2)]
        public byte StateFlags;

        // 物品ConfigId
        [MemoryPackOrder(3)]
        public int ItemCollectConfigId;

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
        
        [MemoryPackOrder(8)]
        public byte[] ExtraData;

        public CollectItemMetaData(uint itemId, Vector3 position, byte stateFlags, int itemCollectConfigId, uint spawnTick, ushort lifetime, ushort randomSeed, int ownerId,
            byte[] extraData = null)
        {
            ItemId = itemId;
            Position = CompressedVector3.FromVector3(position);
            StateFlags = stateFlags;
            ItemCollectConfigId = itemCollectConfigId;
            SpawnTick = spawnTick;
            Lifetime = lifetime;
            RandomSeed = randomSeed;
            OwnerId = ownerId;
            ExtraData = extraData;
        }
        
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
    public partial struct DroppedItemMetaData
    {
        
    }
    
    [MemoryPackable]
    public partial struct CompressedVector2
    {
        [MemoryPackOrder(0)]
        public short x;
        [MemoryPackOrder(1)]
        public short y;

        public Vector2 ToVector2() => new Vector2(x * 0.0001f, y * 0.0001f);
        public static CompressedVector2 FromVector2(Vector2 v) => new CompressedVector2()
        {
            x = (short)(v.x * 10000),
            y = (short)(v.y * 10000),
        };
        
        public static implicit operator Vector2(CompressedVector2 v) => v.ToVector2();
        public static implicit operator CompressedVector2(Vector2 v) => FromVector2(v);
    }
    
    [MemoryPackable]
    public partial struct CompressedVector3
    {
        [MemoryPackOrder(0)]
        public short x;
        [MemoryPackOrder(1)]
        public short y;
        [MemoryPackOrder(2)]
        public short z;

        public Vector3 ToVector3() => new Vector3(x * 0.0001f, y * 0.0001f, z * 0.0001f);
        public static CompressedVector3 FromVector3(Vector3 v) => new CompressedVector3()
        {
            x = (short)(v.x * 10000),
            y = (short)(v.y * 10000),
            z = (short)(v.z * 10000)
        };
        
        public static implicit operator Vector3(CompressedVector3 v) => v.ToVector3();
        public static implicit operator CompressedVector3(Vector3 v) => FromVector3(v);
    }
    
    [MemoryPackable]
    public partial struct CompressedQuaternion
    {
        [MemoryPackOrder(0)]
        public short x;
        [MemoryPackOrder(1)]
        public short y;
        [MemoryPackOrder(2)]
        public short z;
        [MemoryPackOrder(3)]
        public short w;

        public Quaternion ToQuaternion() => new Quaternion(x * 0.0001f, y * 0.0001f, z * 0.0001f, w * 0.0001f);
        public static CompressedQuaternion FromQuaternion(Quaternion q) => new CompressedQuaternion
        {
            x = (short)(q.x * 10000),
            y = (short)(q.y * 10000),
            z = (short)(q.z * 10000),
            w = (short)(q.w * 10000)
        };
    }
    
    [MemoryPackable]
    public partial struct CompressedColor
    {
        [MemoryPackOrder(0)]
        public short r;
        [MemoryPackOrder(1)]
        public short g;
        [MemoryPackOrder(2)]
        public short b;
        [MemoryPackOrder(3)]
        public short a;

        public Color ToQuaternion() => new Color(r * 0.0001f, g * 0.0001f, b * 0.0001f, a * 0.0001f);
        public static CompressedQuaternion FromQuaternion(Quaternion q) => new CompressedQuaternion
        {
            x = (short)(q.x * 10000),
            y = (short)(q.y * 10000),
            z = (short)(q.z * 10000),
            w = (short)(q.w * 10000)
        };
    }

    public enum ColliderType
    {
        Box,
        Sphere,
        Capsule,
        Mesh,
    }
}