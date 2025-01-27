using System;
using System.Linq;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.SyncSystem;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.Data
{
    // 命令接口
    public interface INetworkCommand
    {
        NetworkCommandHeader GetHeader();
        bool IsValid();
    }
    
    // 命令类型枚举
    public enum CommandType
    {
        Property,   // 属性相关
        Combat,     // 战斗相关
        Input,   // 移动相关
        Skill,      // 技能相关
        Interaction // 交互相关
    }
    
    // 命令头
    [Serializable]
    public struct NetworkCommandHeader
    {
        public int connectionId;
        public int tick;
        public CommandType commandType;
        public bool isClientCommand;
    }

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
    public struct PropertyCommandBuff : IPropertyCommandOperation
    {
        public int buffId;
        public BuffType buffType;
        public bool IsClientCommand => false;

        public bool IsValid()
        {
            return Enum.IsDefined(typeof(BuffType), buffType) && buffId > 0;
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
            return header.tick >= 0;
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