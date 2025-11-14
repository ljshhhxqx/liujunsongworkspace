using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using AOTScripts.Data.State;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Tool.HotFixSerializeTool;
using MemoryPack;
using Mirror;
using UnityEngine;
using ISyncPropertyState = AOTScripts.Data.State.ISyncPropertyState;
using PlayerEquipmentState = AOTScripts.Data.State.PlayerEquipmentState;
using PlayerInputState = AOTScripts.Data.State.PlayerInputState;
using PlayerItemState = AOTScripts.Data.State.PlayerItemState;
using PlayerPredictablePropertyState = AOTScripts.Data.State.PlayerPredictablePropertyState;
using PlayerShopState = AOTScripts.Data.State.PlayerShopState;
using PlayerSkillState = AOTScripts.Data.State.PlayerSkillState;

namespace AOTScripts.Data
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
    [MemoryPackUnion(37, typeof(ItemSkillEnableCommand))]
    [MemoryPackUnion(36, typeof(PropertyGetScoreGoldCommand))]
    [MemoryPackUnion(38, typeof(SkillLoadOverloadAnimationCommand))]
    [MemoryPackUnion(39, typeof(PropertyItemAttackCommand))]
    public partial interface INetworkCommand
    {
        NetworkCommandHeader GetHeader();
        bool IsValid();
        NetworkCommandType GetCommandType();
        //void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, CommandAuthority authority = CommandAuthority.Client);
    }
    
    public enum NetworkCommandType
    {
        None = -1,
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
        PropertyGetScoreGold,
        ItemSkillEnable,
        SkillOverride,
        PropertyItemAttack
    }

    public static class NetworkCommandExtensions
    {
        const byte COMMAND_PROTOCOL_VERSION = 1;
        const byte PLAYERSTATE_PROTOCOL_VERSION = 2;
        const byte BATTLECONDITION_PROTOCOL_VERSION = 3;
        const byte CONDITIONCHECKER_PROTOCOL_VERSION = 4;
        
        private static readonly ArrayPool<byte> ByteArrayPool = ArrayPool<byte>.Shared;
        
        public static (byte[] buffer, int length) SerializeBattleChecker<T>(T checker) where T : IConditionChecker
        {
            byte typeId = (byte)checker.GetConditionCheckerHeader().TriggerType;
            byte[] payload = MemoryPackSerializer.Serialize(checker);
    
            // 使用数组池获取缓冲区
            int totalLength = 6 + payload.Length;
            byte[] buffer = ByteArrayPool.Rent(totalLength);
    
            try
            {
                // 写入协议头和类型ID
                buffer[0] = CONDITIONCHECKER_PROTOCOL_VERSION;
                buffer[1] = typeId;
    
                // 直接写入大端序长度（避免创建临时数组）
                if (BitConverter.IsLittleEndian)
                {
                    buffer[2] = (byte)(payload.Length >> 24);
                    buffer[3] = (byte)(payload.Length >> 16);
                    buffer[4] = (byte)(payload.Length >> 8);
                    buffer[5] = (byte)payload.Length;
                }
                else
                {
                    BitConverter.TryWriteBytes(new Span<byte>(buffer, 2, 4), payload.Length);
                }
    
                // 写入payload数据
                Buffer.BlockCopy(payload, 0, buffer, 6, payload.Length);
    
                return (buffer, totalLength);
            }
            finally
            {
                // 立即归还payload数组
                //ByteArrayPool.Return(payload);
            }
        }
        
        public static IConditionChecker DeserializeBattleChecker(ReadOnlySpan<byte> data)
        {
            // 1. 验证基本长度
            if (data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length");
            }

            // 2. 检查协议版本
            byte version = data[0];
            if (version != CONDITIONCHECKER_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {CONDITIONCHECKER_PROTOCOL_VERSION}");
            }

            // 3. 提取类型ID
            byte typeId = data[1];

            // 4. 读取长度字段（直接处理字节序）
            int payloadLength;
            if (BitConverter.IsLittleEndian)
            {
                payloadLength = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5];
            }
            else
            {
                payloadLength = BitConverter.ToInt32(data.Slice(2, 4));
            }

            // 5. 验证数据长度
            int expectedTotalLength = 6 + payloadLength;
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}");
            }

            // 6. 直接使用数据切片，避免复制
            ReadOnlySpan<byte> payload = data.Slice(6, payloadLength);
    
            try
            {
                return GetConditionChecker(payload, typeId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"反序列化失败 ({typeId}): {ex}");
                return null;
            }
        }
        
        
        public static (byte[] buffer, int length) SerializeBattleCondition<T>(T checker) where T : IConditionCheckerParameters
        {
            byte typeId = (byte)checker.GetCommonParameters().TriggerType;
            byte[] payload = MemoryPackSerializer.Serialize(checker);
    
            // 使用数组池获取缓冲区
            int totalLength = 6 + payload.Length;
            byte[] buffer = ByteArrayPool.Rent(totalLength);
    
            try
            {
                // 写入协议头和类型ID
                buffer[0] = BATTLECONDITION_PROTOCOL_VERSION;
                buffer[1] = typeId;
    
                // 直接写入大端序长度（避免创建临时数组）
                if (BitConverter.IsLittleEndian)
                {
                    buffer[2] = (byte)(payload.Length >> 24);
                    buffer[3] = (byte)(payload.Length >> 16);
                    buffer[4] = (byte)(payload.Length >> 8);
                    buffer[5] = (byte)payload.Length;
                }
                else
                {
                    BitConverter.TryWriteBytes(new Span<byte>(buffer, 2, 4), payload.Length);
                }
    
                // 写入payload数据
                Buffer.BlockCopy(payload, 0, buffer, 6, payload.Length);
    
                return (buffer, totalLength);
            }
            finally
            {
                // 立即归还payload数组
                //ByteArrayPool.Return(payload);
            }
        }
        
        public static IConditionCheckerParameters DeserializeBattleCondition(ReadOnlySpan<byte> data)
        {
            // 1. 验证基本长度
            if (data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length");
            }

            // 2. 检查协议版本
            byte version = data[0];
            if (version != BATTLECONDITION_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {BATTLECONDITION_PROTOCOL_VERSION}");
            }

            // 3. 提取类型ID
            byte typeId = data[1];

            // 4. 读取长度字段（直接处理字节序）
            int payloadLength;
            if (BitConverter.IsLittleEndian)
            {
                payloadLength = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5];
            }
            else
            {
                payloadLength = BitConverter.ToInt32(data.Slice(2, 4));
            }

            // 5. 验证数据长度
            int expectedTotalLength = 6 + payloadLength;
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}");
            }

            // 6. 直接使用数据切片，避免复制
            ReadOnlySpan<byte> payload = data.Slice(6, payloadLength);
    
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
        
        public static (byte[], int) SerializePlayerState<T>(T playerState) where T : ISyncPropertyState
        {
            byte typeId = (byte)playerState.GetStateType();
            byte[] payload = MemoryPackSerializer.Serialize(playerState);
    
            // 使用数组池获取缓冲区
            int totalLength = 6 + payload.Length;
            byte[] buffer = ByteArrayPool.Rent(totalLength);
    
            try
            {
                // 写入协议头和类型ID
                buffer[0] = PLAYERSTATE_PROTOCOL_VERSION;
                buffer[1] = typeId;
    
                // 直接写入大端序长度（避免创建临时数组）
                if (BitConverter.IsLittleEndian)
                {
                    buffer[2] = (byte)(payload.Length >> 24);
                    buffer[3] = (byte)(payload.Length >> 16);
                    buffer[4] = (byte)(payload.Length >> 8);
                    buffer[5] = (byte)payload.Length;
                }
                else
                {
                    BitConverter.TryWriteBytes(new Span<byte>(buffer, 2, 4), payload.Length);
                }
    
                // 写入payload数据
                Buffer.BlockCopy(payload, 0, buffer, 6, payload.Length);
    
                return (buffer, totalLength);
            }
            finally
            {
                // 立即归还payload数组
                //ByteArrayPool.Return(payload);
            }
        }
        
        public static ISyncPropertyState DeserializePlayerState(ReadOnlySpan<byte> data)
        {
            // 1. 验证基本长度
            if (data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length");
            }

            // 2. 检查协议版本
            byte version = data[0];
            if (version != PLAYERSTATE_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {PLAYERSTATE_PROTOCOL_VERSION}");
            }

            // 3. 提取类型ID
            byte typeId = data[1];

            // 4. 读取长度字段（直接处理字节序）
            int payloadLength;
            if (BitConverter.IsLittleEndian)
            {
                payloadLength = (data[2] << 24) | (data[3] << 16) | (data[4] << 8) | data[5];
            }
            else
            {
                payloadLength = BitConverter.ToInt32(data.Slice(2, 4));
            }

            // 5. 验证数据长度
            int expectedTotalLength = 6 + payloadLength;
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}");
            }

            // 6. 直接使用数据切片，避免复制
            ReadOnlySpan<byte> payload = data.Slice(6, payloadLength);
    
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

        public static (byte[], int) SerializeCommand<T>(T command) where T : INetworkCommand
        {
            byte typeId = (byte)command.GetCommandType();
            byte[] payload = MemoryPackSerializer.Serialize(command);
    
            int totalLength = 6 + payload.Length;
            byte[] buffer = ByteArrayPool.Rent(totalLength);
    
            try
            {
                // 写入协议头和类型ID
                buffer[0] = COMMAND_PROTOCOL_VERSION;
                buffer[1] = typeId;
    
                // 修复1: 始终以网络字节序(大端序)写入长度
                buffer[2] = (byte)(payload.Length >> 24);
                buffer[3] = (byte)(payload.Length >> 16);
                buffer[4] = (byte)(payload.Length >> 8);
                buffer[5] = (byte)payload.Length;
    
                // 写入payload数据
                Buffer.BlockCopy(payload, 0, buffer, 6, payload.Length);
    
                return (buffer, totalLength);
            }
            finally
            {
                //ByteArrayPool.Return(payload);
            }
        }

        public static INetworkCommand DeserializeCommand(ReadOnlySpan<byte> data)
        {
            if (data.Length < 6)
            {
                throw new ArgumentException("Invalid data format: insufficient length");
            }

            byte version = data[0];
            if (version != COMMAND_PROTOCOL_VERSION)
            {
                throw new InvalidOperationException($"Unsupported protocol version: {version}. Expected: {COMMAND_PROTOCOL_VERSION}");
            }

            byte typeId = data[1];

            // 修复4: 直接读取大端序长度，无需反转
            int payloadLength = (data[2] << 24) | 
                                (data[3] << 16) | 
                                (data[4] << 8) | 
                                data[5];

            int expectedTotalLength = 6 + payloadLength;
            if (data.Length < expectedTotalLength)
            {
                throw new ArgumentException(
                    $"Data length insufficient. Expected: {expectedTotalLength}, Actual: {data.Length}");
            }

            // 修复5: 使用Slice获取精确范围的payload
            ReadOnlySpan<byte> payload = data.Slice(6, payloadLength);
    
            try
            {
                // 修复6: 使用MemoryPack的Span反序列化API
                return payload.GetCommand(typeId);
            }
            catch (Exception ex)
            {
                Debug.LogError($"Deserialization failed ({typeId}): {ex}");
                return null;
            }
        }

        public static IConditionCheckerParameters GetConditionCheckerParameters(this ReadOnlySpan<byte> readOnlySpan, int typeId)
        {
            Debug.Log($"GetConditionCheckerParameters: {typeId}");
            var data = readOnlySpan.ToArray();
            return (TriggerType)typeId switch
            {
                TriggerType.OnAttackHit => BoxingFreeSerializer.MemoryDeserialize<AttackHitCheckerParameters>(data),
                TriggerType.OnAttack => BoxingFreeSerializer.MemoryDeserialize<AttackCheckerParameters>(data),
                TriggerType.OnSkillHit => BoxingFreeSerializer.MemoryDeserialize<SkillHitCheckerParameters>(data),
                TriggerType.OnMove => BoxingFreeSerializer.MemoryDeserialize<MoveCheckerParameters>(data),
                TriggerType.OnSkillCast => BoxingFreeSerializer.MemoryDeserialize<SkillCastCheckerParameters>(data),
                TriggerType.OnTakeDamage => BoxingFreeSerializer.MemoryDeserialize<TakeDamageCheckerParameters>(data),
                TriggerType.OnKill => BoxingFreeSerializer.MemoryDeserialize<KillCheckerParameters>(data),
                TriggerType.OnHpChange => BoxingFreeSerializer.MemoryDeserialize<HpChangeCheckerParameters>(data),
                TriggerType.OnManaChange => BoxingFreeSerializer.MemoryDeserialize<MpChangeCheckerParameters>(data),
                TriggerType.OnCriticalHit => BoxingFreeSerializer.MemoryDeserialize<CriticalHitCheckerParameters>(data),
                TriggerType.OnDodge => BoxingFreeSerializer.MemoryDeserialize<DodgeCheckerParameters>(data),
                TriggerType.OnDeath => BoxingFreeSerializer.MemoryDeserialize<DeathCheckerParameters>(data),
            };
        }
        public static IConditionChecker GetConditionChecker(this ReadOnlySpan<byte> readOnlySpan, int typeId)
        {
            var data = readOnlySpan.ToArray();
            return (TriggerType)typeId switch
            {
                TriggerType.OnAttackHit => BoxingFreeSerializer.MemoryDeserialize<AttackHitChecker>(data),
                TriggerType.OnAttack => BoxingFreeSerializer.MemoryDeserialize<AttackChecker>(data),
                TriggerType.OnSkillHit => BoxingFreeSerializer.MemoryDeserialize<SkillHitChecker>(data),
                TriggerType.OnMove => BoxingFreeSerializer.MemoryDeserialize<MoveChecker>(data),
                TriggerType.OnSkillCast => BoxingFreeSerializer.MemoryDeserialize<SkillCastChecker>(data),
                TriggerType.OnTakeDamage => BoxingFreeSerializer.MemoryDeserialize<TakeDamageChecker>(data),
                TriggerType.OnKill => BoxingFreeSerializer.MemoryDeserialize<KillChecker>(data),
                TriggerType.OnHpChange => BoxingFreeSerializer.MemoryDeserialize<HpChangeChecker>(data),
                TriggerType.OnManaChange => BoxingFreeSerializer.MemoryDeserialize<MpChangeChecker>(data),
                TriggerType.OnCriticalHit => BoxingFreeSerializer.MemoryDeserialize<CriticalHitChecker>(data),
                TriggerType.OnDodge => BoxingFreeSerializer.MemoryDeserialize<DodgeChecker>(data),
                TriggerType.OnDeath => BoxingFreeSerializer.MemoryDeserialize<DeathChecker>(data),
                TriggerType.None => BoxingFreeSerializer.MemoryDeserialize<NoConditionChecker>(data),
            };
        }

        public static ISyncPropertyState GetPlayerState(this ReadOnlySpan<byte> readOnlySpan, int typeId)
        {
            var data = readOnlySpan.ToArray();
            return (PlayerSyncStateType)typeId switch
            {
                PlayerSyncStateType.PlayerEquipment => BoxingFreeSerializer.MemoryDeserialize<PlayerEquipmentState>(data),
                PlayerSyncStateType.PlayerProperty => BoxingFreeSerializer.MemoryDeserialize<PlayerPredictablePropertyState>(data),
                PlayerSyncStateType.PlayerInput => BoxingFreeSerializer.MemoryDeserialize<PlayerInputState>(data),
                PlayerSyncStateType.PlayerItem => BoxingFreeSerializer.MemoryDeserialize<PlayerItemState>(data),
                PlayerSyncStateType.PlayerSkill => BoxingFreeSerializer.MemoryDeserialize<PlayerSkillState>(data),
                PlayerSyncStateType.PlayerShop => BoxingFreeSerializer.MemoryDeserialize<PlayerShopState>(data),
            };
        }

        public static INetworkCommand GetCommand(this ReadOnlySpan<byte> readOnlySpan, int typeId)
        {
            var data = readOnlySpan.ToArray();
            return (NetworkCommandType)typeId switch
            {
                NetworkCommandType.PropertyAutoRecover => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyAutoRecoverCommand>(data),
                NetworkCommandType.PropertyClientAnimation => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyClientAnimationCommand>(data),
                NetworkCommandType.PropertyServerAnimation => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyServerAnimationCommand>(data),
                NetworkCommandType.PropertyBuff => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyBuffCommand>(data),
                NetworkCommandType.PropertyAttack => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyAttackCommand>(data),
                NetworkCommandType.PropertySkill => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertySkillCommand>(data),
                NetworkCommandType.PropertyEnvironmentChange => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyEnvironmentChangeCommand>(data),
                NetworkCommandType.Input => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<InputCommand>(data),
                NetworkCommandType.Animation =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<AnimationCommand>(data),
                NetworkCommandType.Interaction => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<InteractionCommand>(data),
                NetworkCommandType.ItemsUse =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemsUseCommand>(data),
                NetworkCommandType.ItemLock =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemLockCommand>(data),
                NetworkCommandType.ItemEquip =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemEquipCommand>(data),
                NetworkCommandType.ItemDrop =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemDropCommand>(data),
                NetworkCommandType.ItemExchange => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemExchangeCommand>(data),
                NetworkCommandType.ItemsSell =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemsSellCommand>(data),
                NetworkCommandType.ItemsBuy =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemsBuyCommand>(data),
                NetworkCommandType.GoldChanged => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<GoldChangedCommand>(data),
                NetworkCommandType.PropertyInvincibleChanged => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyInvincibleChangedCommand>(data),
                NetworkCommandType.PropertyEquipmentPassive => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyEquipmentPassiveCommand>(data),
                NetworkCommandType.PropertyEquipmentChanged => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyEquipmentChangedCommand>(data),
                NetworkCommandType.NoUnionPlayerAddMoreScoreAndGold => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<NoUnionPlayerAddMoreScoreAndGoldCommand>(data),
                NetworkCommandType.Skill => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<SkillCommand>(data),
                NetworkCommandType.PlayerDeath => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PlayerDeathCommand>(data),
                NetworkCommandType.PlayerReborn => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PlayerRebornCommand>(data),
                NetworkCommandType.PlayerTouchedBase => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PlayerTouchedBaseCommand>(data),
                NetworkCommandType.PlayerTraceOtherPlayerHp => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PlayerTraceOtherPlayerHpCommand>(data),
                NetworkCommandType.ItemsGet =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemsGetCommand>(data),
                NetworkCommandType.Equipment =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<EquipmentCommand>(data),
                NetworkCommandType.Buy => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<BuyCommand>(data),
                NetworkCommandType.RefreshShop => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<RefreshShopCommand>(data),
                NetworkCommandType.Sell => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<SellCommand>(data),
                NetworkCommandType.SkillLoad =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<SkillLoadCommand>(data),
                NetworkCommandType.Trigger =>
                    (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<TriggerCommand>(data),
                NetworkCommandType.SkillChanged => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<SkillChangedCommand>(data),
                NetworkCommandType.PropertyUseSkill =>BoxingFreeSerializer.MemoryDeserialize<PropertyUseSkillCommand>(data),
                NetworkCommandType.ItemSkillEnable => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<ItemSkillEnableCommand>(data),
                NetworkCommandType.PropertyItemAttack => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyItemAttackCommand>(data),
                NetworkCommandType.PropertyGetScoreGold => (INetworkCommand)BoxingFreeSerializer.MemoryDeserialize<PropertyGetScoreGoldCommand>(data),
            };
        }
    }

    // 命令头
    [MemoryPackable]
    public partial struct NetworkCommandHeader : IPoolObject
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

        public void Init()
        {
        }

        public void Clear()
        {
            ConnectionId = 0;
            Tick = 0;
            CommandType = default;
            CommandId = 0;
            Timestamp = 0;
            Authority = default;
            ExecuteType = default;
        }
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
    public partial struct PropertyAutoRecoverCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default; 
            OperationType = default;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyEnvironmentChangeCommand : INetworkCommand, IPoolObject
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
            return PlayerEnvironmentState >= 0 && PlayerEnvironmentState <= PlayerEnvironmentState.Swimming;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            HasInputMovement = default;
            PlayerEnvironmentState = default;
            IsSprinting = default;
        }
    }
    
    
    [MemoryPackable]
    public partial struct PropertyInvincibleChangedCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            IsInvincible = default;
        }
    }

    [MemoryPackable]
    public partial struct PropertyClientAnimationCommand : INetworkCommand, IPoolObject
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
            return AnimationState >= 0 && SkillId >= 0 && AnimationState >= 0 && AnimationState <= AnimationState.SkillQ;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            AnimationState = default;
            SkillId = 0;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyServerAnimationCommand : INetworkCommand, IPoolObject
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
            return AnimationState >= 0 && AnimationState <= AnimationState.SkillQ && AnimationState > 0;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            AnimationState = default;
            SkillId = default;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyEquipmentPassiveCommand : INetworkCommand, IPoolObject
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

        [MemoryPackOrder(7)]
        public float CountDownTime;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyEquipmentPassive;
        
        public NetworkCommandHeader GetHeader()
        {
            return Header;
        }

        public bool IsValid()
        {
            return EquipItemConfigId > 0 && EquipItemId > 0 && PlayerItemType <= PlayerItemType.Score;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            EquipItemConfigId = default;
            EquipItemId = default;
            IsEquipped = default;
            PlayerItemType = default;
            EquipProperty = default;
            TargetIds = null;
        }

    }
    
    [MemoryPackable]
    public partial struct PropertyEquipmentChangedCommand : INetworkCommand, IPoolObject
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
            return EquipConfigId > 0 && EquipItemId > 0 && ItemConfigId > 0 && EquipmentPart <= EquipmentPart.Weapon;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            EquipConfigId = default;
            EquipItemId = default;
            IsEquipped = default;
            ItemConfigId = default;
            EquipmentPart = default;
        }
    }
    
    [MemoryPackable]
    public partial struct NoUnionPlayerAddMoreScoreAndGoldCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            PreNoUnionPlayer = default;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyGetScoreGoldCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int Score;
        [MemoryPackOrder(2)]
        public int Gold;

        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return true; 
        }

        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyGetScoreGold;
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
            return BuffExtraData.buffType >= 0 && BuffExtraData.buffType <= BuffType.Random && BuffExtraData.buffId > 0 && TargetId >= 0 && BuffExtraData.buffId > 0 && BuffExtraData.buffType != BuffType.None
                    && BuffSourceType <= BuffSourceType.Auto;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            CasterId = default;
            TargetId = default;
            BuffExtraData = default;
            BuffSourceType = default;
        }
        
    }
    
    [MemoryPackable]
    public partial struct PlayerDeathCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            KillerId = default;
            DeadCountdownTime = default;
        }
    }
    
    [MemoryPackable]
    public partial struct PlayerRebornCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            RebornPosition = default;
        }
    }

    [MemoryPackable]
    public partial struct GoldChangedCommand : INetworkCommand, IPoolObject
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

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            Gold = default;
        }
    }

    [MemoryPackable]
    public partial struct PlayerTouchedBaseCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PlayerTouchedBase;
        public bool IsValid()
        {
            return true;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
        }
    }

    [MemoryPackable]
    public partial struct PlayerTraceOtherPlayerHpCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public uint[] TargetConnectionIds;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PlayerTraceOtherPlayerHp;

        public bool IsValid()
        {
            if (TargetConnectionIds == null || TargetConnectionIds.Length == 0)
            {
                return false;
            }

            return TargetConnectionIds.Length > 0 && TargetConnectionIds.All(t => t > 0);
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            TargetConnectionIds = null;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyItemAttackCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public uint AttackerId;
        [MemoryPackOrder(2)]
        public int TargetId;
        [MemoryPackOrder(3)]
        public float Damage;
        [MemoryPackOrder(4)]
        public bool IsCritical;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertyItemAttack;

        public NetworkCommandHeader GetHeader() => Header;
        public bool IsValid()
        {
            return AttackerId > 0 && TargetId > 0;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            AttackerId = default;
        }
    }

    [MemoryPackable]
    public partial struct PropertyAttackCommand : INetworkCommand, IPoolObject
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
            if (TargetIds == null || TargetIds.Length == 0)
            {
                return false;
            }
            return AttackerId > 0 && TargetIds.All(t => t > 0);
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            AttackerId = default;
            TargetIds = null;
        }
    }

    [MemoryPackable]
    public partial struct PropertySkillCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int SkillId;
        [MemoryPackOrder(2)]
        public uint[] HitPlayerIds;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.PropertySkill;
        public bool IsValid()
        {
            if (HitPlayerIds == null || HitPlayerIds.Length == 0)
            {
                return false;
            }
            return SkillId > 0 && HitPlayerIds.All(t => t > 0);
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            SkillId = default;
            HitPlayerIds = null;
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyUseSkillCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            SkillConfigId = default;
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
    public partial struct InputCommand : INetworkCommand, IPoolObject
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
             return CommandAnimationState >= 0 && CommandAnimationState <= AnimationState.SkillQ;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            InputMovement = default;
            InputAnimationStates = default;
            CommandAnimationState = default;
        }
    }
    [MemoryPackable]
    public partial struct SkillChangedCommand : INetworkCommand, IPoolObject
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
            return SkillId > 0 && AnimationState >= 0 && AnimationState <= AnimationState.SkillQ;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            SkillId = default;
            AnimationState = default;
        }
    }

    #endregion
    
    #region AnimationCommand

    [MemoryPackable]
    public partial struct AnimationCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public AnimationState AnimationState;

        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Animation;

        public bool IsValid()
        {
            return AnimationState >= 0 && AnimationState <= AnimationState.SkillQ;
        }

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            AnimationState = default;
        }
    }
    #endregion
    
    #region InteractionCommand
    [MemoryPackable]
    public partial struct InteractionCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public uint[] TargetIds;
        public NetworkCommandType GetCommandType() => NetworkCommandType.Interaction;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            if (TargetIds == null || TargetIds.Length == 0)
            {
                return false;
            }
            return TargetIds.All(t => t > 0);
        }

        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            TargetIds = null;
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
    public partial struct ItemsSellCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public MemoryList<SlotIndexData> Slots;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsSell;

        public bool IsValid()
        {
            if (Slots.Count == 0)
            {
                Debug.LogError("ItemsSellCommand Slots is empty");
                return false;
            }

            for (int i = 0; i < Slots.Count; i++)
            {
                var slot = Slots[i];
                if (slot.SlotIndex <= 0 || slot.Count < 0)
                {
                    Debug.LogError("ItemsSellCommand Slots[" + i + "] SlotIndex or Count is invalid");
                    return false;
                }
            }
            return true;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            Slots.Clear();
        }
    }
    
    [MemoryPackable]
    public partial struct ItemSkillEnableCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int SkillConfigId;
        [MemoryPackOrder(2)]
        public bool IsEnable;
        [MemoryPackOrder(3)]
        public int SlotIndex;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemSkillEnable;

        public bool IsValid()
        {
            return SkillConfigId > 0 && SlotIndex > 0;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            
        }
    }
    
    [MemoryPackable]
    public partial struct ItemsBuyCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public MemoryList<ItemsCommandData> Items;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsBuy;

        public bool IsValid()
        {
            if (Items.Count == 0)
            {
                Debug.LogError("ItemsBuyCommand Items is empty");
                return false;
            }
            foreach (var item in Items)
            {
                if (item.ItemConfigId <= 0 || item.Count < 0)
                {
                    Debug.LogError("ItemsBuyCommand Items[" + item.ItemConfigId + "] ItemConfigId or Count is invalid");
                    return false;
                }
            }
            return true;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            Items.Clear();
        }
    }

    [MemoryPackable]
    public partial struct ItemsGetCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public MemoryList<ItemsCommandData> Items;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsGet;

        public bool IsValid()
        {
            if (Items.Count == 0)
            {
                Debug.LogError("ItemsGetCommand Items is empty");
                return false;
            }

            for (int i = 0; i < Items.Count; i++)
            {
                var item = Items[i];
                if (item.ItemConfigId <= 0 || item.Count < 0)
                {
                    Debug.LogError("ItemsGetCommand Items[" + i + "] ItemConfigId or Count is invalid");
                    return false;
                }
            }
            return true;    
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            Items.Clear();
        }
    }
    
    [MemoryPackable]
    public partial struct ItemsUseCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public MemoryDictionary<int, SlotIndexData> Slots;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemsUse;

        public bool IsValid()
        {
            if (Slots.Count == 0)
            {
                Debug.LogError("ItemsUseCommand Slots is empty");
                return false;
            }
            foreach (var slot in Slots.Keys)
            {
                if (Slots[slot].SlotIndex <= 0 || Slots[slot].Count < 0)
                {
                    Debug.LogError("ItemsUseCommand Slots[" + slot + "] SlotIndex or Count is invalid");
                    return false;
                }
            }
            return true;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            Slots.Clear();
        }
    }
    
    [MemoryPackable]
    public partial struct ItemLockCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            SlotIndex = default;
            IsLocked = default;
        }
        
    }
    
    [MemoryPackable]
    public partial struct ItemEquipCommand : INetworkCommand, IPoolObject
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
            return SlotIndex > 0 && PlayerItemType > 0 && PlayerItemType <= PlayerItemType.Score && (PlayerItemType == PlayerItemType.Armor || PlayerItemType == PlayerItemType.Weapon);
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            SlotIndex = default;
            PlayerItemType = default;
            IsEquip = default;
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
    public partial struct ItemExchangeCommand : INetworkCommand, IPoolObject
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
        
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            FromSlotIndex = default;
            ToSlotIndex = default;
        }
    }

    [MemoryPackable]
    public partial struct ItemDropCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public MemoryDictionary<int, SlotIndexData> Slots;
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.ItemDrop;

        public bool IsValid()
        {
            if (Slots.Count == 0)
            {
                Debug.LogError("ItemDropCommand Slots is empty");
                return false;
            }
            foreach (var slot in Slots.Keys)
            {
                if (Slots[slot].SlotIndex <= 0 || Slots[slot].Count < 0)
                {
                    Debug.LogError("ItemDropCommand Slots[" + slot + "] SlotIndex or Count is invalid");
                    return false;
                }
            }
            return true;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            Slots.Clear();
        }
    }
    
    #endregion
    
    #region EqupimentCommand

    [MemoryPackable]
    public partial struct EquipmentCommand : INetworkCommand, IPoolObject
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
            if (EquipmentConfigId < 0)
            {
                Debug.LogError($"EquipmentCommand EquipmentConfigId is invalid {EquipmentConfigId}");
                return false;
            }
            if (EquipmentPart > EquipmentPart.Weapon)
            {
                Debug.LogError($"EquipmentCommand EquipmentPart is invalid {EquipmentPart}");
                return false;
            }
            if (ItemId <= 0)
            {
                Debug.LogError($"EquipmentCommand ItemId is invalid {ItemId}");
                return false;
            }

            if (!IsEquip)
            {
                return true;
            }
            if (string.IsNullOrEmpty(EquipmentPassiveEffectData))
            {
                Debug.LogError($"EquipmentCommand EquipmentPassiveEffectData is invalid {EquipmentPassiveEffectData}");
                return false;
            }
            if (string.IsNullOrEmpty(EquipmentMainEffectData))
            {
                Debug.LogError($"EquipmentCommand EquipmentMainEffectData is invalid {EquipmentMainEffectData}");
                return false;
            }
            return true;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            EquipmentConfigId = default;
            EquipmentPart = default;
            IsEquip = default;
            ItemId = default;
            EquipmentPassiveEffectData = default;
            EquipmentMainEffectData = default;
        }
    }

    #endregion
    #region ShopCommand

    [MemoryPackable]
    public partial struct BuyCommand : INetworkCommand, IPoolObject
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

        
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            ShopId = default;
            Count = default;
        }
    }
    
    [MemoryPackable]
    public partial struct RefreshShopCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        
        public NetworkCommandHeader GetHeader() => Header;
        public NetworkCommandType GetCommandType() => NetworkCommandType.RefreshShop;

        public bool IsValid()
        {
            return true;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
        }
    }
    
    [MemoryPackable]
    public partial struct SellCommand : INetworkCommand, IPoolObject
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
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            ItemSlotIndex = default;
            Count = default;
        }
    }
    
    #endregion
    #region SkillCommand

    [MemoryPackable]
    public partial struct SkillCommand : INetworkCommand, IPoolObject
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
            return SkillConfigId > 0;
        }

        
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            SkillConfigId = default;
            DirectionNormalized = default;
            IsAutoSelectTarget = default;
            KeyCode = default;
        }
    }
    [MemoryPackable]
    public partial struct SkillLoadOverloadAnimationCommand : INetworkCommand, IPoolObject
    {
        [MemoryPackOrder(0)] public NetworkCommandHeader Header;
        [MemoryPackOrder(1)] public float Cost;
        [MemoryPackOrder(2)] public float Cooldowntime;
        [MemoryPackOrder(3)] public AnimationState KeyCode;
        [MemoryPackOrder(4)] public bool IsLoad;
        public NetworkCommandType GetCommandType() => NetworkCommandType.SkillOverride;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return KeyCode == AnimationState.SkillE || KeyCode == AnimationState.SkillQ;
        }

        
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            Cost = 0;
            Cooldowntime = 0;
            IsLoad = false;
            KeyCode = default;
        }
    }
    
    [MemoryPackable]
    public partial struct SkillLoadCommand : INetworkCommand, IPoolObject
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

        
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            SkillConfigId = default;
            IsLoad = default;
            KeyCode = default;
        }
    }

    #endregion

    #region Command

    [MemoryPackable]
    public partial struct TriggerCommand : INetworkCommand, IPoolObject
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
            return TriggerType > 0 && TriggerType <= TriggerType.OnDeath;
        }
        public void Init()
        {
        }

        public void Clear()
        {
            Header = default;
            TriggerType = default;
            TriggerData = null;
        }
    }

    #endregion


    [MemoryPackable]
    public partial struct GameItemData
    {
        [MemoryPackOrder(0)]
        public int ItemId;
        [MemoryPackOrder(1)]
        public int ItemConfigId;
        [MemoryPackOrder(2)]
        public PlayerItemType ItemType;
        [MemoryPackOrder(3)]
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
        [MemoryPackOrder(0)]
        public int ChestId;
        [MemoryPackOrder(1)]
        public int ChestConfigId;
        [MemoryPackOrder(2)]
        public MemoryList<int> ItemIds;
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
            Errors?.Clear();
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

        public static uint GenerateCommandId(bool isServer, CommandType commandType, NetworkCommandType networkCommandType, ref int? sequence)
        {
            // 时间部分：0-3599（60分钟内的秒数），12位 (0-4095)
            var time = (DateTime.UtcNow.Minute % 60) * 60 + DateTime.UtcNow.Second;
        
            // 来源标记：最高位
            var serverFlag = isServer ? 1u << 31 : 0u;
        
            // 时间部分：12位
            var timePart = (uint)(time & 0xFFF) << 19;
        
            // CommandType部分：3位
            var cmdTypePart = networkCommandType == NetworkCommandType.None ?  (uint)commandType << 16 : (uint)networkCommandType << 16;
        
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
    public partial struct CompressedVector4
    {
        [MemoryPackOrder(0)]
        public float x;
        [MemoryPackOrder(1)]
        public float y;
        [MemoryPackOrder(2)]
        public float z;
        [MemoryPackOrder(3)]
        public float w;

        public Quaternion ToQuaternion() => new Quaternion(x, y, z, w);
        public static CompressedVector4 FromVector4(Quaternion v) => new CompressedVector4()
        {
            x = v.x,
            y = v.y,
            z = v.z,
            w = v.w
        };
        
        public static implicit operator Quaternion(CompressedVector4 v) => v.ToQuaternion();
        public static implicit operator CompressedVector4(Quaternion v) => FromVector4(v);
        
        public static implicit operator Vector4(CompressedVector4 v) => new Vector4(v.x, v.y, v.z, v.w);
        public static implicit operator CompressedVector4(Vector4 v) => new CompressedVector4()
        {
            x = v.x,
            y = v.y,
            z = v.z,
            w = v.w
        };
        
        public static implicit operator Color(CompressedVector4 v) => new Color(v.x, v.y, v.z, v.w);

        public static implicit operator CompressedVector4(Color c) => new CompressedVector4()
        {
            x = c.r,
            y = c.g,
            z = c.b,
            w = c.a
        };
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