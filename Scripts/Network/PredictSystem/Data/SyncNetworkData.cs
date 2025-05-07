using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
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
    [MemoryPackUnion(10, typeof(ItemsUseCommand))]
    [MemoryPackUnion(11, typeof(ItemLockCommand))]
    [MemoryPackUnion(12, typeof(ItemEquipCommand))]
    [MemoryPackUnion(13, typeof(ItemDropCommand))]
    [MemoryPackUnion(14, typeof(ItemExchangeCommand))]
    [MemoryPackUnion(15, typeof(ItemsSellCommand))]
    [MemoryPackUnion(16, typeof(ItemsBuyCommand))]
    [MemoryPackUnion(17, typeof(GoldChangedCommand))]
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

    public enum CommandAuthority : byte
    {
        Client,     // 客户端发起
        Server,     // 服务器发起
        System      // 系统自动生成
    }
    
    // 命令类型枚举
    [Flags]
    public enum CommandType : byte
    {
        Property,   // 属性相关
        Input,      // 移动相关
        Item,       // 道具相关
        UI,         // UI相关
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
        
        public NetworkCommandHeader GetHeader()
        {
            return Header;
        }

        public bool IsValid()
        {
            return EquipItemConfigId > 0 && EquipItemId > 0;
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
        
        public NetworkCommandHeader GetHeader()
        {
            return Header;
        }

        public bool IsValid()
        {
            return EquipConfigId > 0 && EquipItemId > 0;
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
            return Enum.IsDefined(typeof(BuffType), BuffExtraData.buffType) && BuffExtraData.buffId > 0 && TargetId > 0;
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
    public partial struct PlayerDeathCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public int KillerId;
        [MemoryPackOrder(2)]
        public float DeadCountdownTime;
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return KillerId > 0 && DeadCountdownTime > 0;
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
    public partial struct PlayerRebornCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public Vector3 RebornPosition;
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return RebornPosition != Vector3.zero;
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
    public partial struct GoldChangedCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)] 
        public float Gold;
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return Gold > 0;
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
    public partial struct PlayerTouchedBaseCommand : INetworkCommand
    {
        [MemoryPackOrder(0)] 
        public NetworkCommandHeader Header;
        
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
        
        public NetworkCommandHeader GetHeader() => Header;

        public bool IsValid()
        {
            return EquipmentConfigId > 0 && Enum.IsDefined(typeof(EquipmentPart), EquipmentPart);
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

    #region Command

    [MemoryPackable]
    public partial struct TriggerCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader Header;
        [MemoryPackOrder(1)]
        public TriggerType TriggerType;
        [MemoryPackOrder(2)]
        public int EquipmentConfigId;
        [MemoryPackOrder(3)] 
        public byte[] TriggerData;
        public NetworkCommandHeader GetHeader() => Header;

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
                case CommandType.Item:
                    return new PlayerItemSyncSystem();
                case CommandType.Equipment:
                    return new PlayerEquipmentSystem();
                case CommandType.Shop:
                    return new ShopSyncSystem();
                case CommandType.Skill:
                    return new PlayerSkillSystem();
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
}