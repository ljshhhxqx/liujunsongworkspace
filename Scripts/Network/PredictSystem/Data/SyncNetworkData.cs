using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using MemoryPack;
using Mirror;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.Data
{
    #region 网络命令相关
    // 命令接口
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PropertyAutoRecoverCommand))]
    [MemoryPackUnion(1, typeof(PropertyClientAnimationCommand))]
    [MemoryPackUnion(2, typeof(PropertyServerAnimationCommand))]
    [MemoryPackUnion(3, typeof(PropertyBuffCommand))]
    [MemoryPackUnion(4, typeof(PropertyAttackCommand))]
    [MemoryPackUnion(5, typeof(PropertySkillCommand))]
    [MemoryPackUnion(6, typeof(PropertyEnvironmentChangeCommand))]
    [MemoryPackUnion(7, typeof(InputCommand))]
    [MemoryPackUnion(8, typeof(AnimationCommand))]
    [MemoryPackUnion(9, typeof(InteractionCommand))]
    public partial interface INetworkCommand
    {
        NetworkCommandHeader GetHeader();
        bool IsValid();
        void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client);
    }
    
    // 命令头
    [MemoryPackable]
    public partial struct NetworkCommandHeader
    {
        [MemoryPackOrder(0)]
        public int ConnectionId;
        [MemoryPackOrder(1)]
        public int Tick;
        [MemoryPackOrder(2)]
        public CommandType CommandType;
        // 命令唯一ID（时间戳+序列号）
        [MemoryPackOrder(3)] 
        public uint CommandId;     
        [MemoryPackOrder(4)] 
        public long Timestamp;
        // 执行上下文
        [MemoryPackOrder(5)] 
        public CommandAuthority Authority;
    }

    public enum CommandAuthority
    {
        Client,     // 客户端发起
        Server,     // 服务器发起
        System      // 系统自动生成
    }
    
    // 命令类型枚举
    public enum CommandType
    {
        Property,   // 属性相关
        Combat,     // 战斗相关
        Input,      // 移动相关
        Animation,  // 动画相关
        Item,       // 道具相关
        Interaction, // 交互相关
        UI,         // UI相关
    }
    
    #endregion

    #region PropertyCommand
    [MemoryPackable]
    public partial struct PropertyAutoRecoverCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public PropertyChangeType OperationType;

        public NetworkCommandHeader GetHeader() => Header;
    
        public bool IsValid()
        {
            return true;
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyEnvironmentChangeCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public bool HasInputMovement;
        [MemoryPackOrder(2)]
        public PlayerEnvironmentState PlayerEnvironmentState;
        [MemoryPackOrder(3)]
        public bool IsSprinting;
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(PlayerEnvironmentState), PlayerEnvironmentState);
        }
        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    
    [MemoryPackable]
    public partial struct PropertyInvincibleChangedCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)] 
        public bool IsInvincible;
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return true;
        }
        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyClientAnimationCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public AnimationState AnimationState;
        
        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), AnimationState);
        }
        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyServerAnimationCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public AnimationState AnimationState;
        
        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), AnimationState);
        }
        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyBuffCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int? CasterId;
        [MemoryPackOrder(2)]
        public int TargetId;
        [MemoryPackOrder(3)]
        public BuffExtraData BuffExtraData;

        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(BuffType), BuffExtraData.buffType) && BuffExtraData.buffId > 0 && TargetId > 0 && Enum.IsDefined(typeof(CollectObjectBuffSize), BuffExtraData.collectObjectBuffSize);
        }
        
        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyAttackCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int AttackerId;
        [MemoryPackOrder(2)]
        public uint[] TargetIds;

        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return TargetIds.Length > 0 && AttackerId > 0 && TargetIds.All(t => t > 0);
        }
        
        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }

    [MemoryPackable]
    public partial struct PropertySkillCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(0)]
        public int SkillId;
        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return SkillId > 0;
        }
        
        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }

    #endregion

    #region CombatCommand
    // [Serializable]
    // public struct AttackCommand : INetworkCommand
    // {
    //     public NetworkCommandHeader header;
    //     public CombatType combatType;
    //     public ICombatCommandOperation Operation;
    //
    //     public NetworkCommandHeader GetHeader() => header;
    //
    //     public bool IsValid()
    //     {
    //         return this.IsCommandValid() && // 基础验证
    //                Operation.IsValid() && // 操作验证
    //                Enum.IsDefined(typeof(CombatType), combatType);
    //     }
    // }


    #endregion
    
    #region InputCommand

    [MemoryPackable]
    public partial struct InputCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public Vector3 InputMovement;
        [MemoryPackOrder(2)]
        public AnimationState[] InputAnimationStates;
        [MemoryPackOrder(3)]
        public AnimationState CommandAnimationState;
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return InputMovement.magnitude > 0 && InputAnimationStates.Length > 0 && InputAnimationStates.All(a => Enum.IsDefined(typeof(AnimationState), a));
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }

    #endregion
    
    #region AnimationCommand

    [MemoryPackable]
    public partial struct AnimationCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public AnimationState AnimationState;

        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), AnimationState);
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    #endregion
    
    #region InteractionCommand
    [MemoryPackable]
    public partial struct InteractionCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public uint[] TargetIds;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return TargetIds.Length > 0 && TargetIds.All(t => t > 0);
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    #endregion

    #region Enum
    
    public enum PropertyChangeType
    {
        AutoRecover,
        EnvironmentChange,
        Buff,
        Attack,
        Skill,
    }
    
    #endregion
    public class CommandValidationResult
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; } = new List<string>();

        public void AddError(string message)
        {
            Errors.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        }
    }
    
    public static class SyncNetworkDataExtensions
    {
        // 基础验证参数配置
        public const int MAX_TICK_DELTA = 30;      // 允许的最大tick偏差
        public const long TIMESTAMP_TOLERANCE = 5000; // 5秒时间容差（毫秒）

        public static NetworkCommandHeader CreateCommand(CommandType commandType, int tick,
            long timeStamp, CommandAuthority authority = CommandAuthority.Client)
        {
            return new NetworkCommandHeader
            {
                ConnectionId = NetworkServer.localConnection.connectionId,
                Tick = tick,
                CommandType = commandType,
                Timestamp = timeStamp,
                Authority = authority
            };
        }

        public static CommandValidationResult ValidateCommand(this INetworkCommand command)
        {
            var result = new CommandValidationResult();
            var header = command.GetHeader();

            // 1. Tick验证
            if (header.Tick <= 0)
            {
                result.AddError("Invalid tick value");
            }

            // 2. 时间戳验证
            var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            if (Math.Abs(currentTime - header.Timestamp) > TIMESTAMP_TOLERANCE)
            {
                result.AddError($"Timestamp out of sync: {currentTime - header.Timestamp}ms");
            }

            // 3. 命令类型验证
            if (!Enum.IsDefined(typeof(CommandType), header.CommandType))
            {
                result.AddError($"Unknown command type: {header.CommandType}");
            }

            // 4. 基础有效性验证
            if (!command.IsValid())
            {
                result.AddError("Command specific validation failed");
            }

            return result;
        }
        
        public static BaseSyncSystem GetSyncSystem(this CommandType syncNetworkData)
        {
            switch (syncNetworkData)
            {
                case CommandType.Property:
                    return new PlayerPropertySyncSystem();
                case CommandType.Input:
                    return new PlayerInputSyncSystem();
            }   
            return null;
        }
    }
    public struct HybridCommandId
    {
        public static uint Generate(bool isServer, ref int sequence)
        {
            // 时间部分：0-3599（60分钟内的秒数）
            var time = (DateTime.UtcNow.Minute % 60) * 60 + DateTime.UtcNow.Second;
        
            // 来源标记：最高位
            uint serverFlag = isServer ? 1u << 31 : 0u;
        
            // 时间部分：15位（覆盖0-32767）
            uint timePart = (uint)(time & 0x7FFF) << 16;
        
            // 序列号：16位（每个来源独立）
            uint seqPart = (uint)(Interlocked.Increment(ref sequence) & 0xFFFF);

            return serverFlag | timePart | seqPart;
        }

        // 解析方法
        public static void Deconstruct(uint commandId, out bool isServer, out int timestamp, out ushort sequence)
        {
            isServer = (commandId & 0x80000000) != 0;
            timestamp = (int)((commandId >> 16) & 0x7FFF);
            sequence = (ushort)(commandId & 0xFFFF);
        }
    }
    
    public static class NetworkCommandValidator
    {
        // 基础结构验证 (50%性能提升)
        public static bool ValidateBasic(this INetworkCommand command)
        {
            var header = command.GetHeader();
            return header.Tick >= 0 
                   && header.Timestamp > DateTime.UtcNow.AddMinutes(-5).Ticks
                   && header.Timestamp <= DateTime.UtcNow.Ticks;
        }

        // 权限验证
        public static bool ValidateAuthority(this INetworkCommand command)
        {
            var header = command.GetHeader();
            return header.Authority switch
            {
                CommandAuthority.Client => NetworkServer.connections.ContainsKey(header.ConnectionId),
                CommandAuthority.Server => NetworkServer.active,
                CommandAuthority.System => true,
                _ => false
            };
        }

        // 安全签名验证 (防篡改)
        // public static bool ValidateSecurity(this INetworkCommand command, byte[] secretKey)
        // {
        //     var header = command.GetHeader();
        //     using var hmac = new HMACSHA256(secretKey);
        //     var computedHash = hmac.ComputeHash(GetSignData(header));
        //     return computedHash.SequenceEqual(header.SecurityHash);
        // }
        //
        // private static byte[] GetSignData(NetworkCommandHeader header)
        // {
        //     using var stream = new MemoryStream();
        //     using var writer = new BinaryWriter(stream);
        //     writer.Write(header.ConnectionId);
        //     writer.Write(header.Tick);
        //     writer.Write((int)header.CommandType);
        //     writer.Write(header.Timestamp);
        //     return stream.ToArray();
        // }
    }
}