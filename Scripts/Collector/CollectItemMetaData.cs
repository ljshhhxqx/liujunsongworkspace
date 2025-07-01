using System;
using HotUpdate.Scripts.Network.PredictSystem.Data;
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
    
    

    public enum ColliderType
    {
        Box,
        Sphere,
        Capsule,
        Mesh,
    }
}