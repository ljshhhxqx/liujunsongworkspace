using System;
using System.Linq;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;
using MemoryPack;
using Mirror;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.Data
{
    #region 网络命令相关
    // 命令接口
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PropertyCommand))]
    [MemoryPackUnion(1, typeof(InputCommand))]
    [MemoryPackUnion(2, typeof(AnimationCommand))]
    [MemoryPackUnion(3, typeof(InteractionCommand))]
    public partial interface INetworkCommand
    {
        NetworkCommandHeader GetHeader();
        bool IsValid();
        void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, NetworkIdentity executingIdentity = null);
    }
    
    // 命令头
    [MemoryPackable]
    public partial struct NetworkCommandHeader
    {
        [MemoryPackOrder(0)]
        public int connectionId;
        [MemoryPackOrder(1)]
        public int tick;
        [MemoryPackOrder(2)]
        public CommandType commandType;
        [MemoryPackOrder(3)]
        public bool isClientCommand;
        [MemoryPackIgnore] // NetworkIdentity 需要特殊处理
        public NetworkIdentity executingIdentity;
    }
    
    // 命令类型枚举
    public enum CommandType
    {
        Property,   // 属性相关
        Combat,     // 战斗相关
        Input,      // 移动相关
        Animation,  // 动画相关
        Interaction // 交互相关
    }
    
    #endregion

    #region PropertyCommand
    [MemoryPackable]
    public partial struct PropertyCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader header;
        [MemoryPackOrder(1)]
        public PropertyChangeType operationType;
        [MemoryPackIgnore]
        public IPropertyCommandOperation Operation;
        
        [MemoryPackInclude]
        [MemoryPackOrder(2)]
        private object _operationBoxed;

        public NetworkCommandHeader GetHeader() => header;
    
        public bool IsValid()
        {
            return this.IsCommandValid() && // 基础验证
                   Operation.IsValid() && // 操作验证
                   Enum.IsDefined(typeof(PropertyChangeType), Operation);
        }
        [MemoryPackOnSerializing]
        void OnSerializing()
        {
            _operationBoxed = Operation;
        }
        
        [MemoryPackOnDeserialized]
        void OnDeserialized()
        {
            Operation = (IPropertyCommandOperation)_operationBoxed;
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, NetworkIdentity executingIdentity = null)
        {
            header.connectionId = headerConnectionId;
            header.tick = currentTick;
            header.commandType = headerCommandType;
            header.isClientCommand = !executingIdentity;
            header.executingIdentity = NetworkClient.localPlayer;
        }
    }
    
    [MemoryPackable]
    [MemoryPackUnion(0, typeof(PropertyCommandAutoRecover))]
    [MemoryPackUnion(1, typeof(PropertyCommandEnvironmentChange))]
    [MemoryPackUnion(2, typeof(PropertyAnimationCommand))]
    [MemoryPackUnion(3, typeof(PropertyServerChangeAnimationCommand))]
    [MemoryPackUnion(4, typeof(PropertyCommandBuff))]
    [MemoryPackUnion(5, typeof(PropertyCommandAttack))]
    [MemoryPackUnion(6, typeof(PropertyCommandSkill))]
    public interface IPropertyCommandOperation
    {
        bool IsClientCommand { get; }
        bool IsValid();
    }
        
    [MemoryPackable]
    public partial struct PropertyCommandAutoRecover : IPropertyCommandOperation
    {
        public bool IsClientCommand => true;
        public bool IsValid()
        {
            return true;
        }
    }

    [MemoryPackable]
    public partial struct PropertyCommandEnvironmentChange : IPropertyCommandOperation
    {
        public bool IsClientCommand => true;
        [MemoryPackOrder(0)]
        public bool hasInputMovement;
        [MemoryPackOrder(1)]
        public PlayerEnvironmentState environmentType;
        [MemoryPackOrder(2)]
        public bool isSprinting;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(PlayerEnvironmentState), environmentType);
        }
    }
    
    [MemoryPackable]
    public partial struct PropertyAnimationCommand : IPropertyCommandOperation
    {
        public bool IsClientCommand => true;
        [MemoryPackOrder(0)]
        public AnimationState animationState;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), animationState);
        }
    }
    
    [MemoryPackable]
    public struct PropertyServerChangeAnimationCommand : IPropertyCommandOperation
    {
        public bool IsClientCommand => false;
        [MemoryPackOrder(0)]
        public AnimationState animationState;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), animationState);
        }
    }
    
    [MemoryPackable]
    public struct PropertyCommandBuff : IPropertyCommandOperation
    {
        [MemoryPackOrder(0)]
        public int? CasterId;
        [MemoryPackOrder(1)]
        public int targetId;
        [MemoryPackOrder(2)]
        public BuffExtraData buffExtraData;
        public bool IsClientCommand => false;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(BuffType), buffExtraData.buffType) && buffExtraData.buffId > 0 && targetId > 0 && Enum.IsDefined(typeof(CollectObjectBuffSize), buffExtraData.collectObjectBuffSize);
        }
    }
    
    [MemoryPackable]
    public struct PropertyCommandAttack : IPropertyCommandOperation
    {
        [MemoryPackOrder(0)]
        public int attackerId;
        [MemoryPackOrder(1)]
        public int[] targetIds;
        public bool IsClientCommand => false;

        public bool IsValid()
        {
            return targetIds.Length > 0 && attackerId > 0 && targetIds.All(t => t > 0);
        }
    }

    [MemoryPackable]
    public struct PropertyCommandSkill : IPropertyCommandOperation
    {
        public bool IsClientCommand => false;
        [MemoryPackOrder(0)]
        public int skillId;
        public bool IsValid()
        {
            return skillId > 0;
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
    public struct InputCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader header;
        [MemoryPackOrder(1)]
        public Vector3 inputMovement;
        [MemoryPackOrder(2)]
        public AnimationState[] inputAnimationStates;
        public NetworkCommandHeader GetHeader() => header;

        public bool IsValid()
        {
            return this.IsCommandValid() && inputMovement.magnitude > 0 && inputAnimationStates.Length > 0 && inputAnimationStates.All(a => Enum.IsDefined(typeof(AnimationState), a));
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, NetworkIdentity executingIdentity = null)
        {
            header.connectionId = headerConnectionId;
            header.tick = currentTick;
            header.commandType = headerCommandType;
            header.isClientCommand = !executingIdentity;
            header.executingIdentity = NetworkClient.localPlayer;
        }
    }

    #endregion
    
    #region AnimationCommand

    [MemoryPackable]
    public struct AnimationCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader header;
        [MemoryPackOrder(1)]
        public AnimationState actionCommand;

        public NetworkCommandHeader GetHeader() => header;

        public bool IsValid()
        {
            return this.IsCommandValid() &&
                   Enum.IsDefined(typeof(AnimationState), actionCommand);
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, NetworkIdentity executingIdentity = null)
        {
            header.connectionId = headerConnectionId;
            header.tick = currentTick;
            header.commandType = headerCommandType;
            header.isClientCommand = !executingIdentity;
            header.executingIdentity = NetworkClient.localPlayer;
        }
    }

    #endregion
    
    #region InteractionCommand
    [MemoryPackable]
    public struct InteractionCommand : INetworkCommand
    {
        [MemoryPackOrder(0)]
        public NetworkCommandHeader header;
        [MemoryPackOrder(1)]
        public uint[] targetIds;
        
        public NetworkCommandHeader GetHeader() => header;

        public bool IsValid()
        {
            return this.IsCommandValid() && targetIds.Length > 0 && targetIds.All(t => t > 0);
        }

        public void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, NetworkIdentity executingIdentity = null)
        {
            header.connectionId = headerConnectionId;
            header.tick = currentTick;
            header.commandType = headerCommandType;
            header.isClientCommand = !executingIdentity;
            header.executingIdentity = NetworkClient.localPlayer;
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

    public static class SyncNetworkDataExtensions
    {
        public static bool IsCommandValid(this INetworkCommand syncNetworkData)
        {
            var header = syncNetworkData.GetHeader();
            var identity = header.executingIdentity;
            if (!identity)
            {
                return false;
            }
            if (header.isClientCommand)
            {
                return NetworkServer.connections.ContainsKey(header.connectionId) && identity.connectionToClient.connectionId == header.connectionId && header.tick >= 0;
            }

            return identity.isServer && header.tick >= 0;
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
}