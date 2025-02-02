using System;
using System.Linq;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;
using Mirror;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.Data
{
    #region 网络命令相关
    // 命令接口
    public interface INetworkCommand
    {
        NetworkCommandHeader GetHeader();
        bool IsValid();
        void SetHeader(int headerConnectionId, CommandType headerCommandType, int currentTick, NetworkIdentity executingIdentity = null);
    }
    
    // 命令头
    [Serializable]
    public struct NetworkCommandHeader
    {
        public int connectionId;
        public int tick;
        public CommandType commandType;
        public bool isClientCommand;
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
    
    [Serializable]
    public struct PropertyCommand : INetworkCommand
    {
        public NetworkCommandHeader header;
        public PropertyChangeType operationType;
        public IPropertyCommandOperation Operation;

        public NetworkCommandHeader GetHeader() => header;
    
        public bool IsValid()
        {
            return this.IsCommandValid() && // 基础验证
                   Operation.IsValid() && // 操作验证
                   Enum.IsDefined(typeof(PropertyChangeType), Operation);
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

    public interface IPropertyCommandOperation
    {
        bool IsClientCommand { get; }
        bool IsValid();
    }
        
    [Serializable]
    public struct PropertyCommandAutoRecover : IPropertyCommandOperation
    {
        public bool IsClientCommand => true;
        public bool IsValid()
        {
            return true;
        }
    }

    [Serializable]
    public struct PropertyCommandEnvironmentChange : IPropertyCommandOperation
    {
        public bool IsClientCommand => true;
        public bool hasInputMovement;
        public PlayerEnvironmentState environmentType;
        public bool isSprinting;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(PlayerEnvironmentState), environmentType);
        }
    }
    
    [Serializable]
    public struct PropertyAnimationCommand : IPropertyCommandOperation
    {
        public bool IsClientCommand => true;
        public AnimationState animationState;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), animationState);
        }
    }
    
    [Serializable]
    public struct PropertyServerChangeAnimationCommand : IPropertyCommandOperation
    {
        public bool IsClientCommand => false;
        public AnimationState animationState;
        public bool IsValid()
        {
            return Enum.IsDefined(typeof(AnimationState), animationState);
        }
    }
    
    [Serializable]
    public struct PropertyCommandBuff : IPropertyCommandOperation
    {
        public BuffExtraData buffExtraData;
        public int? CasterId;
        public int targetId;
        public bool IsClientCommand => false;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(BuffType), buffExtraData.buffType) && buffExtraData.buffId > 0 && targetId > 0 && Enum.IsDefined(typeof(CollectObjectBuffSize), buffExtraData.collectObjectBuffSize);
        }
    }
    
    [Serializable]
    public struct PropertyCommandAttack : IPropertyCommandOperation
    {
        public int attackerId;
        public int[] targetIds;
        public bool IsClientCommand => false;

        public bool IsValid()
        {
            return targetIds.Length > 0 && attackerId > 0 && targetIds.All(t => t > 0);
        }
    }

    [Serializable]
    public struct PropertyCommandSkill : IPropertyCommandOperation
    {
        public bool IsClientCommand => false;
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

    [Serializable]
    public struct InputCommand : INetworkCommand
    {
        public NetworkCommandHeader header;
        public Vector3 inputMovement;
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

    [Serializable]
    public struct AnimationCommand : INetworkCommand
    {
        public NetworkCommandHeader header;
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
    [Serializable]
    public struct InteractionCommand : INetworkCommand
    {
        public NetworkCommandHeader header;
        public int[] targetIds;
        
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
            }   
            return null;
        }
    }
}