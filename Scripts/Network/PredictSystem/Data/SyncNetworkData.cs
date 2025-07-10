using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AOTScripts.Data;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.State;
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
    [MemoryPackUnion(10, typeof(ItemsUseCommand))]
    [MemoryPackUnion(11, typeof(ItemLockCommand))]
    [MemoryPackUnion(12, typeof(ItemEquipCommand))]
    [MemoryPackUnion(13, typeof(ItemDropCommand))]
    [MemoryPackUnion(14, typeof(ItemExchangeCommand))]
    [MemoryPackUnion(15, typeof(ItemsSellCommand))]
    [MemoryPackUnion(16, typeof(ItemsBuyCommand))]
    [MemoryPackUnion(17, typeof(GoldChangedCommand))]
    [MemoryPackUnion(18, typeof(PropertyInvincibleChangedCommand))]
    [MemoryPackUnion(19, typeof(PropertyEquipmentPassiveCommand))]
    [MemoryPackUnion(20, typeof(PropertyEquipmentChangedCommand))]
    [MemoryPackUnion(21, typeof(NoUnionPlayerAddMoreScoreAndGoldCommand))]
    [MemoryPackUnion(22, typeof(SkillCommand))]
    [MemoryPackUnion(23, typeof(PlayerDeathCommand))]
    [MemoryPackUnion(24, typeof(PlayerRebornCommand))]
    [MemoryPackUnion(25, typeof(PlayerTouchedBaseCommand))]
    [MemoryPackUnion(26, typeof(PlayerTraceOtherPlayerHpCommand))]
    [MemoryPackUnion(27, typeof(ItemsGetCommand))]
    [MemoryPackUnion(28, typeof(EquipmentCommand))]
    [MemoryPackUnion(29, typeof(BuyCommand))]
    [MemoryPackUnion(30, typeof(RefreshShopCommand))]
    [MemoryPackUnion(31, typeof(SellCommand))]
    [MemoryPackUnion(32, typeof(SkillLoadCommand))]
    [MemoryPackUnion(33, typeof(TriggerCommand))]
    [MemoryPackUnion(34, typeof(SkillChangedCommand))]
    [MemoryPackUnion(35, typeof(PropertyUseSkillCommand))]
    public partial interface INetworkCommand
    {
        NetworkCommandHeader GetHeader();
        bool IsValid();
        NetworkCommandType GetCommandType();
        //void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client);
    }
    
    public enum NetworkCommandType
    {
        PropertyAutoRecover,
        PropertyClientAnimation,
        PropertyServerAnimation,
        PropertyBuff,
        PropertyAttack,
        PropertySkill,
        PropertyEnvironmentChange,
        Input,
        Animation,
        Interaction,
        ItemsUse,
        ItemLock,
        ItemEquip,
        ItemDrop,
        ItemExchange,
        ItemsSell,
        ItemsBuy,
        GoldChanged,
        PropertyInvincibleChanged,
        PropertyEquipmentPassive,
        PropertyEquipmentChanged,
        NoUnionPlayerAddMoreScoreAndGold,
        Skill,
        PlayerDeath,
        PlayerReborn,
        PlayerTouchedBase,
        PlayerTraceOtherPlayerHp,
        ItemsGet,
        Equipment,
        Buy,
        RefreshShop,
        Sell,
        SkillLoad,
        Trigger,
        SkillChanged,
        PropertyUseSkill,
        SpeedChangedByInput
    }

    public static class NetworkCommandExtensions
    {
        const byte COMMAND_PROTOCOL_VERSION = 1;
        const byte PLAYERSTATE_PROTOCOL_VERSION = 2;
        const byte BATTLECONDITION_PROTOCOL_VERSION = 3;
        const byte CONDITIONCHECKER_PROTOCOL_VERSION = 4;
        
         public static byte[] SerializeBattleChecker<T>(T checker) where T : IConditionChecker
        {
            byte typeId = (byte)checker.GetConditionCheckerHeader().TriggerType;
            byte[] payload = MemoryPackSerializer.Serialize(checker);
    
            // 正确分配空间：头部6字节 + payload
            byte[] result = new byte[6 + payload.Length];
    
            // 写入协议头和类型ID
            result[0] = CONDITIONCHECKER_PROTOCOL_VERSION;
            result[1] = typeId;
    
            // 写入长度字段（显式转为大端序）
            byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            Buffer.BlockCopy(lengthBytes, 0, result, 2, 4);
    
            // 写入payload数据
            Buffer.BlockCopy(payload, 0, result, 6, payload.Length);
    
            return result;
        }
        
        public static IConditionChecker DeserializeBattleChecker(byte[] data)
        {
            // 1. 验证基本长度
            if (data == null || data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length", nameof(data));
            }

            // 2. 检查协议版本
            byte version = data[0];
            if (version != CONDITIONCHECKER_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {PLAYERSTATE_PROTOCOL_VERSION}");
            }

            // 3. 提取类型ID
            byte typeId = data[1];

            // 4. 读取长度字段（考虑字节序）
            byte[] lengthBytes = new byte[4];
            Buffer.BlockCopy(data, 2, lengthBytes, 0, 4);
    
            // 如果数据是以大端序存储的，需要反转字节顺序（根据序列化时的设置）
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
    
            int payloadLength = BitConverter.ToInt32(lengthBytes, 0);

            // 5. 验证数据长度
            int expectedTotalLength = 6 + payloadLength; // 1(版本) + 1(类型) + 4(长度) + payload
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}",
                    nameof(data));
            }

            // 6. 提取payload数据
            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 6, payload, 0, payloadLength);
            try
            {
                return payload.GetConditionChecker(typeId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"反序列化失败 ({typeId}): {ex}");
                return null;
            }
        }
        
        public static byte[] SerializeBattleCondition<T>(T checkerParameters) where T : IConditionCheckerParameters
        {
            byte typeId = (byte)checkerParameters.GetCommonParameters().TriggerType;
            byte[] payload = MemoryPackSerializer.Serialize(checkerParameters);
    
            // 正确分配空间：头部6字节 + payload
            byte[] result = new byte[6 + payload.Length];
    
            // 写入协议头和类型ID
            result[0] = BATTLECONDITION_PROTOCOL_VERSION;
            result[1] = typeId;
    
            // 写入长度字段（显式转为大端序）
            byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            Buffer.BlockCopy(lengthBytes, 0, result, 2, 4);
    
            // 写入payload数据
            Buffer.BlockCopy(payload, 0, result, 6, payload.Length);
    
            return result;
        }
        
        public static IConditionCheckerParameters DeserializeBattleCondition(byte[] data)
        {
            // 1. 验证基本长度
            if (data == null || data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length", nameof(data));
            }

            // 2. 检查协议版本
            byte version = data[0];
            if (version != BATTLECONDITION_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {PLAYERSTATE_PROTOCOL_VERSION}");
            }

            // 3. 提取类型ID
            byte typeId = data[1];

            // 4. 读取长度字段（考虑字节序）
            byte[] lengthBytes = new byte[4];
            Buffer.BlockCopy(data, 2, lengthBytes, 0, 4);
    
            // 如果数据是以大端序存储的，需要反转字节顺序（根据序列化时的设置）
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
    
            int payloadLength = BitConverter.ToInt32(lengthBytes, 0);

            // 5. 验证数据长度
            int expectedTotalLength = 6 + payloadLength; // 1(版本) + 1(类型) + 4(长度) + payload
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}",
                    nameof(data));
            }

            // 6. 提取payload数据
            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 6, payload, 0, payloadLength);
            try
            {
                return payload.GetConditionCheckerParameters(typeId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"反序列化失败 ({typeId}): {ex}");
                return null;
            }
        }
        
        public static byte[] SerializePlayerState<T>(T playerState) where T : ISyncPropertyState
        {
            byte typeId = (byte)playerState.GetStateType();
            byte[] payload = MemoryPackSerializer.Serialize(playerState);
    
            // 正确分配空间：头部6字节 + payload
            byte[] result = ArrayPool<byte>.Shared.Rent(6 + payload.Length);//new byte[];
    
            // 写入协议头和类型ID
            result[0] = PLAYERSTATE_PROTOCOL_VERSION;
            result[1] = typeId;
    
            // 写入长度字段（显式转为大端序）
            byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            Buffer.BlockCopy(lengthBytes, 0, result, 2, 4);
    
            // 写入payload数据
            Buffer.BlockCopy(payload, 0, result, 6, payload.Length);
    
            return result;
        }
        
        public static ISyncPropertyState DeserializePlayerState(byte[] data)
        {
            // 1. 验证基本长度
            if (data == null || data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length", nameof(data));
            }

            // 2. 检查协议版本
            byte version = data[0];
            if (version != PLAYERSTATE_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {PLAYERSTATE_PROTOCOL_VERSION}");
            }

            // 3. 提取类型ID
            byte typeId = data[1];

            // 4. 读取长度字段（考虑字节序）
            byte[] lengthBytes = ArrayPool<byte>.Shared.Rent(4);
            Buffer.BlockCopy(data, 2, lengthBytes, 0, 4);
    
            // 如果数据是以大端序存储的，需要反转字节顺序（根据序列化时的设置）
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
    
            int payloadLength = BitConverter.ToInt32(lengthBytes, 0);

            // 5. 验证数据长度
            int expectedTotalLength = 6 + payloadLength; // 1(版本) + 1(类型) + 4(长度) + payload
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}",
                    nameof(data));
            }

            // 6. 提取payload数据
            byte[] payload = ArrayPool<byte>.Shared.Rent(payloadLength);
            Buffer.BlockCopy(data, 6, payload, 0, payloadLength);
            try
            {
                return payload.GetPlayerState(typeId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"反序列化失败 ({typeId}): {ex}");
                return null;
            }
        }

        public static byte[] SerializeCommand<T>(T command) where T : INetworkCommand
        {
            byte typeId = (byte)command.GetCommandType();
            byte[] payload = MemoryPackSerializer.Serialize(command);
    
            // 正确分配空间：头部6字节 + payload
            byte[] result = ArrayPool<byte>.Shared.Rent(6+payload.Length);
    
            // 写入协议头和类型ID
            result[0] = COMMAND_PROTOCOL_VERSION;
            result[1] = typeId;
    
            // 写入长度字段（显式转为大端序）
            byte[] lengthBytes = BitConverter.GetBytes(payload.Length);
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
            Buffer.BlockCopy(lengthBytes, 0, result, 2, 4);
    
            // 写入payload数据
            Buffer.BlockCopy(payload, 0, result, 6, payload.Length);
    
            return result;
        }
        
        public static INetworkCommand DeserializeCommand(byte[] data)
        {
            // 1. 验证基本长度
            if (data == null || data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length", nameof(data));
            }

            // 2. 检查协议版本
            byte version = data[0];
            if (version != COMMAND_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {COMMAND_PROTOCOL_VERSION}");
            }

            // 3. 提取类型ID
            byte typeId = data[1];

            // 4. 读取长度字段（考虑字节序）
            byte[] lengthBytes = new byte[4];
            Buffer.BlockCopy(data, 2, lengthBytes, 0, 4);
    
            // 如果数据是以大端序存储的，需要反转字节顺序（根据序列化时的设置）
            if (BitConverter.IsLittleEndian)
            {
                Array.Reverse(lengthBytes);
            }
    
            int payloadLength = BitConverter.ToInt32(lengthBytes, 0);

            // 5. 验证数据长度
            int expectedTotalLength = 6 + payloadLength; // 1(版本) + 1(类型) + 4(长度) + payload
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}",
                    nameof(data));
            }

            // 6. 提取payload数据
            byte[] payload = new byte[payloadLength];
            Buffer.BlockCopy(data, 6, payload, 0, payloadLength);

            // 7. 根据类型ID确定具体类型并反序列化
            try
            {
                return payload.GetCommand(typeId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"反序列化失败 ({typeId}): {ex}");
                return null;
            }
        }
        public static IConditionCheckerParameters GetConditionCheckerParameters(this byte[] data, int typeId)
        {
            return (TriggerType)typeId switch
            {
                TriggerType.OnAttackHit => MemoryPackSerializer.Deserialize<AttackHitCheckerParameters>(data),
                TriggerType.OnAttack => MemoryPackSerializer.Deserialize<AttackCheckerParameters>(data),
                TriggerType.OnSkillHit => MemoryPackSerializer.Deserialize<SkillHitCheckerParameters>(data),
                TriggerType.OnMove => MemoryPackSerializer.Deserialize<MoveCheckerParameters>(data),
                TriggerType.OnSkillCast => MemoryPackSerializer.Deserialize<SkillCastCheckerParameters>(data),
                TriggerType.OnTakeDamage => MemoryPackSerializer.Deserialize<TakeDamageCheckerParameters>(data),
                TriggerType.OnKill => MemoryPackSerializer.Deserialize<KillCheckerParameters>(data),
                TriggerType.OnHpChange => MemoryPackSerializer.Deserialize<HpChangeCheckerParameters>(data),
                TriggerType.OnManaChange => MemoryPackSerializer.Deserialize<MpChangeCheckerParameters>(data),
                TriggerType.OnCriticalHit => MemoryPackSerializer.Deserialize<CriticalHitCheckerParameters>(data),
                TriggerType.OnDodge => MemoryPackSerializer.Deserialize<DodgeCheckerParameters>(data),
                TriggerType.OnDeath => MemoryPackSerializer.Deserialize<DeathCheckerParameters>(data),
                _ => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, null)
            };
        }
        public static IConditionChecker GetConditionChecker(this byte[] data, int typeId)
        {
            return (TriggerType)typeId switch
            {
                TriggerType.OnAttackHit => MemoryPackSerializer.Deserialize<AttackHitChecker>(data),
                TriggerType.OnAttack => MemoryPackSerializer.Deserialize<AttackChecker>(data),
                TriggerType.OnSkillHit => MemoryPackSerializer.Deserialize<SkillHitChecker>(data),
                TriggerType.OnMove => MemoryPackSerializer.Deserialize<MoveChecker>(data),
                TriggerType.OnSkillCast => MemoryPackSerializer.Deserialize<SkillCastChecker>(data),
                TriggerType.OnTakeDamage => MemoryPackSerializer.Deserialize<TakeDamageChecker>(data),
                TriggerType.OnKill => MemoryPackSerializer.Deserialize<KillChecker>(data),
                TriggerType.OnHpChange => MemoryPackSerializer.Deserialize<HpChangeChecker>(data),
                TriggerType.OnManaChange => MemoryPackSerializer.Deserialize<MpChangeChecker>(data),
                TriggerType.OnCriticalHit => MemoryPackSerializer.Deserialize<CriticalHitChecker>(data),
                TriggerType.OnDodge => MemoryPackSerializer.Deserialize<DodgeChecker>(data),
                TriggerType.OnDeath => MemoryPackSerializer.Deserialize<DeathChecker>(data),
                _ => throw new ArgumentOutOfRangeException(nameof(typeId), typeId, null)
            };
        }

        public static ISyncPropertyState GetPlayerState(this byte[] data, int typeId)
        {
            return (PlayerSyncStateType)typeId switch
            {
                PlayerSyncStateType.PlayerEquipment => MemoryPackSerializer.Deserialize<PlayerEquipmentState>(data),
                PlayerSyncStateType.PlayerProperty => MemoryPackSerializer.Deserialize<PlayerPredictablePropertyState>(data),
                PlayerSyncStateType.PlayerInput => MemoryPackSerializer.Deserialize<PlayerInputState>(data),
                PlayerSyncStateType.PlayerItem => MemoryPackSerializer.Deserialize<PlayerItemState>(data),
                PlayerSyncStateType.PlayerSkill => MemoryPackSerializer.Deserialize<PlayerSkillState>(data),
                PlayerSyncStateType.PlayerShop => MemoryPackSerializer.Deserialize<PlayerShopState>(data),
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        public static INetworkCommand GetCommand(this byte[] data, int typeId)
        {
            return (NetworkCommandType)typeId switch
            {
                NetworkCommandType.PropertyAutoRecover => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyAutoRecoverCommand>(data),
                NetworkCommandType.PropertyClientAnimation => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyClientAnimationCommand>(data),
                NetworkCommandType.PropertyServerAnimation => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyServerAnimationCommand>(data),
                NetworkCommandType.PropertyBuff => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyBuffCommand>(data),
                NetworkCommandType.PropertyAttack => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyAttackCommand>(data),
                NetworkCommandType.PropertySkill => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertySkillCommand>(data),
                NetworkCommandType.PropertyEnvironmentChange => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyEnvironmentChangeCommand>(data),
                NetworkCommandType.Input => (INetworkCommand)MemoryPackSerializer.Deserialize<InputCommand>(data),
                NetworkCommandType.Animation =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<AnimationCommand>(data),
                NetworkCommandType.Interaction => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<InteractionCommand>(data),
                NetworkCommandType.ItemsUse =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<ItemsUseCommand>(data),
                NetworkCommandType.ItemLock =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<ItemLockCommand>(data),
                NetworkCommandType.ItemEquip =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<ItemEquipCommand>(data),
                NetworkCommandType.ItemDrop =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<ItemDropCommand>(data),
                NetworkCommandType.ItemExchange => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<ItemExchangeCommand>(data),
                NetworkCommandType.ItemsSell =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<ItemsSellCommand>(data),
                NetworkCommandType.ItemsBuy =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<ItemsBuyCommand>(data),
                NetworkCommandType.GoldChanged => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<GoldChangedCommand>(data),
                NetworkCommandType.PropertyInvincibleChanged => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyInvincibleChangedCommand>(data),
                NetworkCommandType.PropertyEquipmentPassive => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyEquipmentPassiveCommand>(data),
                NetworkCommandType.PropertyEquipmentChanged => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PropertyEquipmentChangedCommand>(data),
                NetworkCommandType.NoUnionPlayerAddMoreScoreAndGold => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<NoUnionPlayerAddMoreScoreAndGoldCommand>(data),
                NetworkCommandType.Skill => (INetworkCommand)MemoryPackSerializer.Deserialize<SkillCommand>(data),
                NetworkCommandType.PlayerDeath => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PlayerDeathCommand>(data),
                NetworkCommandType.PlayerReborn => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PlayerRebornCommand>(data),
                NetworkCommandType.PlayerTouchedBase => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PlayerTouchedBaseCommand>(data),
                NetworkCommandType.PlayerTraceOtherPlayerHp => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<PlayerTraceOtherPlayerHpCommand>(data),
                NetworkCommandType.ItemsGet =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<ItemsGetCommand>(data),
                NetworkCommandType.Equipment =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<EquipmentCommand>(data),
                NetworkCommandType.Buy => (INetworkCommand)MemoryPackSerializer.Deserialize<BuyCommand>(data),
                NetworkCommandType.RefreshShop => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<RefreshShopCommand>(data),
                NetworkCommandType.Sell => (INetworkCommand)MemoryPackSerializer.Deserialize<SellCommand>(data),
                NetworkCommandType.SkillLoad =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<SkillLoadCommand>(data),
                NetworkCommandType.Trigger =>
                    (INetworkCommand)MemoryPackSerializer.Deserialize<TriggerCommand>(data),
                NetworkCommandType.SkillChanged => (INetworkCommand)MemoryPackSerializer
                    .Deserialize<SkillChangedCommand>(data),
                NetworkCommandType.PropertyUseSkill => MemoryPackSerializer.Deserialize<PropertyUseSkillCommand>(data),
                _ => throw new ArgumentOutOfRangeException()
            };
        }
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
        [MemoryPackOrder(6)] 
        public CommandExecuteType ExecuteType;
    }

    public enum CommandExecuteType
    {
        //服务器立即同步(可以由客户端发起，也可以由服务器发起)
        Immediate,
        //在客户端预测后再同步(客户端本地需要能回滚)
        Predicate,
        //服务器发起的命令(由客户端的预测命令引起的，因而和客户端预测后的时机一起同步)
        ServerSync,
    }

    public enum CommandAuthority
    {
        Client,     // 客户端发起
        Server,     // 服务器发起
        System      // 系统自动生成
    }
    
    // 命令类型枚举
    [Flags]
    public enum CommandType
    {
        Property,   // 属性相关
        Input,      // 移动相关
        Item,       // 道具相关
        Skill,      // 技能相关
        Equipment,  // 装备相关
        Interact,    // 交互相关
        Shop
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

        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyAutoRecover;
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
        
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyEnvironmentChange;
        

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(PlayerEnvironmentState), PlayerEnvironmentState);
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
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyInvincibleChanged;
    }
    
    [MemoryPackable]
    public partial struct SpeedChangedByInputCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public bool IsSprinting;
        [MemoryPackOrder(2)]
        public bool HasInputMovement;
        [MemoryPackOrder(2)]
        public PlayerEnvironmentState PlayerEnvironmentState;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(PlayerEnvironmentState), PlayerEnvironmentState);
        }
        public NetworkCommandType GetCommandType() => NetworkCommandType.SpeedChangedByInput;
    }

    [MemoryPackable]
    public partial struct PropertyClientAnimationCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public AnimationState AnimationState;
        [MemoryPackOrder(2)]
        public int SkillId;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyClientAnimation;

        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), AnimationState);
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyServerAnimationCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public AnimationState AnimationState;
        [MemoryPackOrder(2)] 
        public int SkillId;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyServerAnimation;
        
        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), AnimationState) && AnimationState > 0;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyEquipmentPassiveCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)] 
        public int EquipItemConfigId;
        [MemoryPackOrder(2)] 
        public int EquipItemId;
        [MemoryPackOrder(3)] 
        public bool IsEquipped;
        [MemoryPackOrder(4)] 
        public PlayerItemType PlayerItemType;
        [MemoryPackOrder(5)]
        public string EquipProperty;
        [MemoryPackOrder(6)]
        public int[] TargetIds;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyEquipmentPassive;
        
        public NetworkCommandHeader GetHeader()
        {
            return Header;
        }

        public bool IsValid()
        {
            return EquipItemConfigId > 0 && EquipItemId > 0 && Enum.IsDefined(typeof(PlayerItemType), PlayerItemType) && !string.IsNullOrEmpty(EquipProperty);
        }

    }
    
    [MemoryPackable]
    public partial struct PropertyEquipmentChangedCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int EquipConfigId;
        [MemoryPackOrder(2)]
        public int EquipItemId;
        [MemoryPackOrder(3)] 
        public bool IsEquipped;
        [MemoryPackOrder(3)] 
        public int ItemConfigId;
        [MemoryPackOrder(3)] 
        public EquipmentPart EquipmentPart;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyEquipmentChanged;
        
        public NetworkCommandHeader GetHeader()
        {
            return Header;
        }

        public bool IsValid()
        {
            return EquipConfigId > 0 && EquipItemId > 0;
        }
    }
    
    [MemoryPackable]
    public partial struct NoUnionPlayerAddMoreScoreAndGoldCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;

        [MemoryPackOrder(1)] 
        public int PreNoUnionPlayer;
        public NetworkCommandType GetCommandType() => NetworkCommandType.NoUnionPlayerAddMoreScoreAndGold;
        
        public NetworkCommandHeader GetHeader() => Header;
        
        public bool IsValid()
        {
            return PreNoUnionPlayer > 0;
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
        [MemoryPackOrder(4)]
        public BuffSourceType BuffSourceType;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyBuff;

        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(BuffType), BuffExtraData.buffType) && BuffExtraData.buffId > 0 && TargetId > 0 && CasterId.HasValue && BuffExtraData.buffId > 0 && BuffExtraData.buffType != BuffType.None
                && Enum.IsDefined(typeof(BuffSourceType), BuffSourceType);
        }
        
    }
    
    [MemoryPackable]
    public partial struct PlayerDeathCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int KillerId;
        [MemoryPackOrder(2)]
        public float DeadCountdownTime;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PlayerDeath;

        public bool IsValid()
        {
            return KillerId > 0 && DeadCountdownTime > 0;
        }
    }
    
    [MemoryPackable]
    public partial struct PlayerRebornCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public CompressedVector3 RebornPosition;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PlayerReborn;

        public bool IsValid()
        {
            return RebornPosition.ToVector3() != Vector3.zero;
        }
    }

    [MemoryPackable]
    public partial struct GoldChangedCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)] 
        public float Gold;
        public NetworkCommandType GetCommandType() => NetworkCommandType.GoldChanged;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return Gold > 0;
        }

    }

    [MemoryPackable]
    public partial struct PlayerTouchedBaseCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PlayerTouchedBase;
        public bool IsValid()
        {
            return true;
        }
    }

    [MemoryPackable]
    public partial struct PlayerTraceOtherPlayerHpCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int[] TargetConnectionIds;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PlayerTraceOtherPlayerHp;

        public bool IsValid()
        {
            return TargetConnectionIds.Length > 0 && TargetConnectionIds.All(t => t > 0);
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
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyAttack;

        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return TargetIds.Length > 0 && AttackerId > 0 && TargetIds.All(t => t > 0);
        }
    }

    [MemoryPackable]
    public partial struct PropertySkillCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int SkillId;
        [MemoryPackOrder(2)]
        public int[] HitPlayerIds;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertySkill;
        public bool IsValid()
        {
            return SkillId > 0 && HitPlayerIds.Length > 0 && HitPlayerIds.All(t => t > 0);
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyUseSkillCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int SkillConfigId;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyUseSkill;
        public bool IsValid()
        {
            return SkillConfigId > 0;
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
        public CompressedVector3 InputMovement;
        [MemoryPackOrder(2)]
        public AnimationState InputAnimationStates;
        [MemoryPackOrder(3)]
        public AnimationState CommandAnimationState;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Input;

        public bool IsValid()
        {
            return InputMovement.ToVector3().magnitude > 0 && Enum.IsDefined(typeof(AnimationState), InputAnimationStates) && Enum.IsDefined(typeof(AnimationState), CommandAnimationState);
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
    public partial struct SkillChangedCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int SkillId;
        [MemoryPackOrder(2)]
        public AnimationState AnimationState;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.SkillChanged;

        public bool IsValid()
        {
            return SkillId > 0 && Enum.IsDefined(typeof(AnimationState), AnimationState);
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
        public NetworkCommandType GetCommandType() => NetworkCommandType.Animation;

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
        public NetworkCommandType GetCommandType() => NetworkCommandType.Interaction;
        
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
    
    #region ItemCommand
    /// <summary>
    /// 多个道具的交互数据(可以是多个不同的道具)
    /// </summary>
    [MemoryPackable]
    public partial struct ItemsCommandData
    {
        [MemoryPackOrder(0)]
        public int ItemConfigId;
        [MemoryPackOrder(1)] 
        public int Count;
        [MemoryPackOrder(2)]
        public int[] ItemUniqueId;
        [MemoryPackOrder(3)]
        public int? ItemShopId;
        [MemoryPackOrder(4)]
        public PlayerItemType? ItemType;
    }
    
    [MemoryPackable]
    public partial struct ItemsSellCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public SlotIndexData[] Slots;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsSell;

        public bool IsValid()
        {
            return Slots.Length > 0 && Slots.All(i => i.SlotIndex > 0 && i.Count > 0);
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
    public partial struct ItemsBuyCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public ItemsCommandData[] Items;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsBuy;

        public bool IsValid()
        {
            return Items.Length > 0 && Items.All(i => i.ItemConfigId > 0 && i.Count > 0 && i.ItemShopId > 0);
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
    public partial struct ItemsGetCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public ItemsCommandData[] Items;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsGet;

        public bool IsValid()
        {
            return Items.Length > 0 && Items.All(i => i.ItemConfigId > 0 && i.Count > 0);
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
    public partial struct ItemsUseCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public SlotIndexData[] Slots;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsUse;

        public bool IsValid()
        {
            return Slots.Length > 0 && Slots.All(s => s.SlotIndex > 0 && s.Count > 0);
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
    public partial struct ItemLockCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int SlotIndex;
        [MemoryPackOrder(2)]
        public bool IsLocked;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemLock;

        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return SlotIndex > 0;
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
    public partial struct ItemEquipCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int SlotIndex;
        [MemoryPackOrder(2)] 
        public PlayerItemType PlayerItemType;
        [MemoryPackOrder(2)]
        public bool IsEquip;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemEquip;
        
        public bool IsValid()
        {
            return SlotIndex > 0 && Enum.IsDefined(typeof(PlayerItemType), PlayerItemType) && (PlayerItemType == PlayerItemType.Armor || PlayerItemType == PlayerItemType.Weapon);
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
    public partial struct SlotIndexData
    {
        [MemoryPackOrder(0)]
        public int SlotIndex;
        [MemoryPackOrder(1)]
        public int Count;
    }
    
    [MemoryPackable]
    public partial struct ItemExchangeCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int FromSlotIndex;
        [MemoryPackOrder(2)]
        public int ToSlotIndex;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemExchange;
        
        public bool IsValid()
        {
            return FromSlotIndex > 0 && ToSlotIndex > 0;
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
    public partial struct ItemDropCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public SlotIndexData[] Slots;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemDrop;

        public bool IsValid()
        {
            return Slots.Length > 0 && Slots.All(s => s.SlotIndex > 0 && s.Count > 0);
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
    
    #region EqupimentCommand

    [MemoryPackable]
    public partial struct EquipmentCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int EquipmentConfigId;
        [MemoryPackOrder(2)]
        public EquipmentPart EquipmentPart;
        [MemoryPackOrder(3)]
        public bool IsEquip;
        [MemoryPackOrder(4)] 
        public int ItemId;
        [MemoryPackOrder(5)] 
        public string EquipmentPassiveEffectData;
        [MemoryPackOrder(6)] 
        public string EquipmentMainEffectData;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Equipment;

        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return EquipmentConfigId > 0 && Enum.IsDefined(typeof(EquipmentPart), EquipmentPart)
                && ItemId > 0 && !string.IsNullOrEmpty(EquipmentPassiveEffectData) && !string.IsNullOrEmpty(EquipmentMainEffectData);
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick,
            CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }

    #endregion
    #region ShopCommand

    [MemoryPackable]
    public partial struct BuyCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int ShopId;
        [MemoryPackOrder(2)] 
        public int Count;
        
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Buy;

        public bool IsValid()
        {
            return ShopId > 0 && Count > 0;
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick,
            CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct RefreshShopCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.RefreshShop;

        public bool IsValid()
        {
            return true;
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick,
            CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct SellCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int ItemSlotIndex;
        [MemoryPackOrder(2)]
        public int Count;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Sell;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return ItemSlotIndex > 0 && Count > 0;
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick,
            CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    #endregion
    #region SkillCommand

    [MemoryPackable]
    public partial struct SkillCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] public NetworkCommandHeader Header;
        [MemoryPackOrder(1)] public int SkillConfigId;
        [MemoryPackOrder(2)] public CompressedVector3 DirectionNormalized;
        [MemoryPackOrder(3)] public bool IsAutoSelectTarget;
        [MemoryPackOrder(4)] public AnimationState KeyCode;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Skill;

        public bool IsValid()
        {
            return SkillConfigId > 0 && DirectionNormalized.ToVector3() != Vector3.zero;
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick,
            CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }
    
    [MemoryPackable]
    public partial struct SkillLoadCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] public NetworkCommandHeader Header;
        [MemoryPackOrder(1)] public int SkillConfigId;
        [MemoryPackOrder(2)] public bool IsLoad;
        [MemoryPackOrder(3)] public AnimationState KeyCode;
        public NetworkCommandType GetCommandType() => NetworkCommandType.SkillLoad;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return SkillConfigId > 0;
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick,
            CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }

    #endregion

    #region Command

    [MemoryPackable]
    public partial struct TriggerCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public TriggerType TriggerType;
        [MemoryPackOrder(2)] 
        public byte[] TriggerData;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Trigger;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(TriggerType), TriggerType);
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick,
            CommandAuthority authority = CommandAuthority.Client)
        {
            Header.ConnectionId = headerConnectionId;
            Header.Tick = currentTick;
            Header.CommandType = headerCommandType;
            Header.Authority = authority;
        }
    }

    #endregion


    [MemoryPackable]
    public partial struct GameItemData
    {
        public int ItemId;
        public int ItemConfigId;
        public PlayerItemType ItemType;
        public ItemState ItemState;
    }
    
    [Flags]
    public enum ItemState : byte
    {
        None = 0,
        IsActive = 1 << 0,     // 00000001 - 存在于场景中
        IsInBag = 1 << 1,      // 00000010 - 存在玩家背包中
        IsEquipped = 1 << 2,    // 00000100 - 装备状态
        IsLocked = 1 << 3,     // 00000100 - 锁定状态
        IsInteracting = 1 << 4,// 00001000 - 正在交互中（播放动画）
    }
    
    [MemoryPackable]
    public partial struct GameChestData
    {
        public int ChestId;
        public int ChestConfigId;
        public List<int> ItemIds;
    }
    
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
    public class CommandValidationResult : IPoolObject
    {
        public bool IsValid => Errors.Count == 0;
        public List<string> Errors { get; private set; }

        public void AddError(string message)
        {
            Errors.Add($"[{DateTime.UtcNow:HH:mm:ss.fff}] {message}");
        }

        public void Init()
        {
            Errors ??= new List<string>();
        }

        public void Clear()
        {
            Errors.Clear();
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
            var result = ObjectPoolManager<CommandValidationResult>.Instance.Get();
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
                case CommandType.Item:
                    return new PlayerItemSyncSystem();
                case CommandType.Equipment:
                    return new PlayerEquipmentSystem();
                case CommandType.Shop:
                    return new ShopSyncSystem();
                case CommandType.Skill:
                    return new PlayerSkillSyncSystem();
                case CommandType.Interact:
                    //Debug.LogWarning("Not implemented yet");
                    return null;
                // case CommandType.UI:
                //     return new PlayerCombatSyncSystem();
            }   
            return null;
        }
    }
    public struct HybridIdGenerator
    {
        private static readonly int[] Sequences = new int[Enum.GetValues(typeof(CommandType)).Length];
        public static string RoomId;
        private static int _currentItemId;
        private static int _currentChestId;
        private static int _currentEquipmentId;
        private static int _currentShopId;

        public static int GenerateEquipmentId(int configId, int currentTick)
        {
            _currentEquipmentId++;
            return _currentEquipmentId;
        }

        /// <summary>
        /// 生成物品ID(玩家id+时间戳+物品类型+序列号)
        /// </summary>
        /// <param name="configId"></param>
        /// <param name="currentTick"></param>
        /// <returns></returns>
        public static int GenerateItemId(int configId, int currentTick)
        {
            _currentItemId++;
            return _currentItemId;
        }

        public static ItemIdData DeconstructItemId(ulong itemId, Func<uint, string> ownerMapper)
        {
            return new ItemIdData();
            // {
            //     Tick = (int)((itemId >> 40) & 0xFFFFFF),
            //     OwnerId = ownerMapper((uint)((itemId >> 20) & 0xFFFFF)),
            //     ConfigId = (int)((itemId >> 12) & 0xFF),
            //     Sequence = (int)(itemId & 0xFFF)
            // };
        }

        public struct ItemIdData
        {
            public string OwnerId;
            public DateTime Timestamp;
            public int ConfigId;
            public int Tick;
        }

        public static uint GenerateCommandId(bool isServer, CommandType commandType, ref int? sequence)
        {
            // 时间部分：0-3599（60分钟内的秒数），12位 (0-4095)
            var time = (DateTime.UtcNow.Minute % 60) * 60 + DateTime.UtcNow.Second;
        
            // 来源标记：最高位
            var serverFlag = isServer ? 1u << 31 : 0u;
        
            // 时间部分：12位
            var timePart = (uint)(time & 0xFFF) << 19;
        
            // CommandType部分：3位
            var cmdTypePart = (uint)commandType << 16;
        
            // 序列号：16位
            var seq = sequence.GetValueOrDefault(0);
            var seqPart = sequence.HasValue 
                ? (uint)(Interlocked.Increment(ref seq) & 0xFFFF)
                : (uint)(Interlocked.Increment(ref Sequences[(int)commandType]) & 0xFFFF);

            return serverFlag | timePart | cmdTypePart | seqPart;
        }

        // 解析方法
        public static NetworkCommandData Deconstruct(uint commandId)
        {
            var data = new NetworkCommandData();
            data.IsServer = (commandId & 0x80000000) != 0;
            data.Timestamp = (int)((commandId >> 19) & 0xFFF);
            data.CommandType = (CommandType)((commandId >> 16) & 0x7);
            data.Sequence = (ushort)(commandId & 0xFFFF);
            return data;
        }
        
        public struct NetworkCommandData
        {
            public bool IsServer;
            public long Timestamp;
            public CommandType CommandType;
            public ushort Sequence;
        }

        public static int GenerateChestId(int configId, int currentTick)
        {
            return ++_currentChestId; 
        }
        
        public static int GenerateShopId(int configId, int currentTick)
        {
            return ++_currentShopId; 
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
    
    [MemoryPackable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)] // 紧密打包
    public partial struct CompressedVector2
    {
        [MemoryPackOrder(0)]
        public short x;
        [MemoryPackOrder(1)]
        public short y;

        public Vector2 ToVector2() => new Vector2(x * 0.001f, y * 0.001f);
        public static CompressedVector2 FromVector2(Vector2 v) => new CompressedVector2()
        {
            x = Math.Clamp((short)(v.x * 1000), short.MinValue, short.MaxValue),
            y = Math.Clamp((short)(v.y * 1000), short.MinValue, short.MaxValue)
        };
        
        public static implicit operator Vector2(CompressedVector2 v) => v.ToVector2();
        public static implicit operator CompressedVector2(Vector2 v) => FromVector2(v);
    }
    
    [MemoryPackable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)] // 紧密打包
    public partial struct CompressedVector3
    {
        [MemoryPackOrder(0)]
        public float x;
        [MemoryPackOrder(1)]
        public float y;
        [MemoryPackOrder(2)]
        public float z;

        public Vector3 ToVector3() => new Vector3(x, y, z);
        public static CompressedVector3 FromVector3(Vector3 v) => new CompressedVector3()
        {
            x = v.x,
            y = v.y,
            z = v.z
        };
        
        public static implicit operator Vector3(CompressedVector3 v) => v.ToVector3();
        public static implicit operator CompressedVector3(Vector3 v) => FromVector3(v);
    }
    
    [MemoryPackable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)] // 紧密打包
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

        public Quaternion ToQuaternion() => new Quaternion(x * 0.001f, y * 0.001f, z * 0.001f, w * 0.001f);
        public static CompressedQuaternion FromQuaternion(Quaternion q) => new CompressedQuaternion
        {
            x = Math.Clamp((short)(q.x * 1000), short.MinValue, short.MaxValue),
            y = Math.Clamp((short)(q.y * 1000), short.MinValue, short.MaxValue),
            z = Math.Clamp((short)(q.z * 1000), short.MinValue, short.MaxValue),
            w = Math.Clamp((short)(q.w * 1000), short.MinValue, short.MaxValue)
        };
        
        public static implicit operator Quaternion(CompressedQuaternion q) => q.ToQuaternion();
        public static implicit operator CompressedQuaternion(Quaternion q) => FromQuaternion(q);
    }
    
    [MemoryPackable]
    [StructLayout(LayoutKind.Sequential, Pack = 1)] // 紧密打包
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

        public Color ToColor() => new Color(r * 0.001f, g * 0.001f, b * 0.001f, a * 0.001f);
        public static CompressedColor FromColor(Color q) => new CompressedColor
        {
            r = Math.Clamp((short)(q.r * 0.001f), short.MinValue, short.MaxValue),
            g = Math.Clamp((short)(q.g * 0.001f), short.MinValue, short.MaxValue),
            b = Math.Clamp((short)(q.b * 0.001f), short.MinValue, short.MaxValue),
            a = Math.Clamp((short)(q.a * 0.001f), short.MinValue, short.MaxValue)
        };
        
        public static implicit operator Color(CompressedColor c) => c.ToColor();
        public static implicit operator CompressedColor(Color c) => FromColor(c);
    }
    
}