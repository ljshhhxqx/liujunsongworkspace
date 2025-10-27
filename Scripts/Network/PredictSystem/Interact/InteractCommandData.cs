﻿using System;
using System.Linq;
using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Interact
{
    public enum InteractCategory 
    {
        PlayerToScene,    // 玩家与场景交互
        PlayerToPlayer,   // 玩家间交互
        SceneToPlayer     // 场景主动影响玩家
    }
    
    public enum InteractResult : byte
    {
        Pending,         // 等待处理
        Success,         // 交互成功
        Failed,          // 交互失败
        PartiallySuccess // 部分成功
    }

    public enum InteractionType
    {
        PickupItem = 0,
        PickupChest,
        DropItem,
        Count
    }

    // 基础交互头
    [MemoryPackable]
    public partial struct InteractHeader : IPoolObject
    {
        [MemoryPackOrder(0)] 
        public uint CommandId;              // 命令唯一ID（时间戳+序列号）
        [MemoryPackOrder(1)] 
        public int RequestConnectionId;               // 发起者connectionId
        [MemoryPackOrder(2)]
        public int Tick;
        [MemoryPackOrder(3)] 
        public InteractCategory Category;
        [MemoryPackOrder(4)] 
        public CompressedVector3 Position; // 交互发生位置
        [MemoryPackOrder(5)] 
        public long Timestamp;     // UTC时间戳（毫秒）
        // 执行上下文
        [MemoryPackOrder(6)] 
        public CommandAuthority Authority;

        public void Init()
        {
        }

        public void Clear()
        {
            CommandId = 0;
            RequestConnectionId = 0;
            Tick = 0;
            Category = 0;
            Position = default;
            Timestamp = 0;
            Authority = 0;
        }
    }
    
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(SceneInteractRequest))]
    [MemoryPackUnion(1, typeof(PlayerInteractRequest))]
    [MemoryPackUnion(2, typeof(EnvironmentInteractRequest))]
    [MemoryPackUnion(3, typeof(PlayerToSceneRequest))]
    public partial interface IInteractRequest
    {
        InteractHeader GetHeader();
        bool IsValid();
    }
    
    //玩家主动创造物品给场景(比如扔下可以被交互的物品)
    [MemoryPackable]
    public partial struct PlayerToSceneRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public DroppedItemData[] ItemDatas; // 物品id（由服务器生成的id，而非场景id）
        [MemoryPackOrder(2)] public InteractionType InteractionType; // 交互类型（开门/拾取等）
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            if (ItemDatas == null || ItemDatas.Length == 0)
            {
                Debug.LogError("ItemDatas is null or empty");
                return false;
            }

            for (int i = 0; i < ItemDatas.Length; i++)
            {
                var itemData = ItemDatas[i];
                if (itemData.ItemConfigId <= 0 || itemData.Count <= 0)
                {
                    Debug.LogError($"ItemDatas[{i}] is invalid, ItemConfigId: {itemData.ItemConfigId}, Count: {itemData.Count}");
                    return false;
                }
            }
            return InteractionType >= InteractionType.PickupItem && InteractionType < InteractionType.Count && ItemDatas.Length > 0 && ItemDatas.All(id => id.ItemConfigId > 0 && id.Count > 0);
        }
    }

    // 玩家与场景交互
    [MemoryPackable]
    public partial struct SceneInteractRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public uint SceneItemId; // 场景物体ID（由服务器生成的id）
        [MemoryPackOrder(2)] public InteractionType InteractionType; // 交互类型（开门/拾取等）
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            return InteractionType >= InteractionType.PickupItem && InteractionType < InteractionType.Count && SceneItemId > 0;
        }
    }

    // 玩家间交互
    [MemoryPackable]
    public partial struct PlayerInteractRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public uint TargetPlayerId;
        [MemoryPackOrder(2)] public ushort InteractionId; // 预定义的交互ID
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            return TargetPlayerId > 0 && InteractionId > 0;
        }
    }

    // 环境主动交互
    [MemoryPackable]
    public partial struct EnvironmentInteractRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public uint HazardId;    // 环境危险区域ID
        [MemoryPackOrder(2)] public float Intensity;  // 影响强度
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            return HazardId > 0 && Intensity > 0;
        }
    }
    
    [MemoryPackable]
    public partial struct PlayerChangeUnionRequest : IInteractRequest
    {
        [MemoryPackOrder(0)] public InteractHeader Header;
        [MemoryPackOrder(1)] public uint KillerPlayerId;
        [MemoryPackOrder(2)] public uint DeadPlayerId;
        public InteractCategory Category => Header.Category;
        public InteractHeader GetHeader() => Header;
        public bool IsValid()
        {
            return KillerPlayerId > 0 && DeadPlayerId > 0;
        }
    }

    public static class InteractNetworkDataExtensions
    {
        // 基础验证参数配置
        public const int MAX_TICK_DELTA = 30;      // 允许的最大tick偏差
        public const long TIMESTAMP_TOLERANCE = 5000; // 5秒时间容差（毫秒）
        public static CommandValidationResult CommandValidResult(this IInteractRequest command)
        {
            var result = ObjectPoolManager<CommandValidationResult>.Instance.Get(30);
            var header = command.GetHeader();

            // 1. Tick验证
            if (header.Tick <= 0)
            {
                result.AddError($"Invalid tick value, {header.Tick}, now tick is {GameSyncManager.CurrentTick}");
            }

            // 2. 时间戳验证
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Math.Abs(currentTime - header.Timestamp) > TIMESTAMP_TOLERANCE)
            {
                result.AddError($"Timestamp out of sync: {currentTime - header.Timestamp}ms");
            }

            // 3. 命令类型验证
            if (header.Category < 0 || header.Category > InteractCategory.SceneToPlayer)
            {
                result.AddError($"Unknown command type: {header.Category}");
            }

            // 4. 基础有效性验证
            if (!command.IsValid())
            {
                result.AddError($"Command specific validation failed, type is {header.Category}");
            }

            return result;
        }
    }

    [MemoryPackable]
    public partial struct DroppedItemData : IEquatable<DroppedItemData>
    {
        [MemoryPackOrder(0)]
        public int ItemConfigId;
        [MemoryPackOrder(1)]
        public int Count;
        [MemoryPackOrder(2)]
        public QualityType Quality;
        [MemoryPackOrder(3)]
        public int[] ItemIds;

        public bool Equals(DroppedItemData other)
        {
            return ItemConfigId == other.ItemConfigId && Count == other.Count && Quality == other.Quality;
        }

        public override bool Equals(object obj)
        {
            return obj is DroppedItemData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ItemConfigId, Count);
        }
    }
}