using System;
using AOTScripts.Tool.ObjectPool;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.UI.UIs.Panel.Item;

namespace HotUpdate.Scripts.Network.PredictSystem.UI
{
    public enum UIPropertyDefine
    {
        [UIPropertyType(typeof(PropertyItemData))]
        PlayerProperty,
        [UIPropertyType(typeof(BagItemData))]
        BagItem,
        [UIPropertyType(typeof(EquipItemData))]
        EquipmentItem,
        [UIPropertyType(typeof(RandomShopItemData))]
        ShopItem,
        [UIPropertyType(typeof(ValuePropertyData))]
        PlayerBaseData,
        [UIPropertyType(typeof(PlayerDeathTimeData))]
        PlayerDeathTime,
        [UIPropertyType(typeof(PlayerHpItemData))]
        PlayerTraceOtherPlayerHp,
        [UIPropertyType(typeof(PlayerControlData))]
        PlayerControl,
        [UIPropertyType(typeof(AnimationStateData))]
        Animation
    }

    // 复合键结构（玩家ID + 数据Key）
    public readonly struct BindingKey : IEquatable<BindingKey>
    {
        public readonly int PlayerId;
        public readonly UIPropertyDefine PropertyKey;
        public readonly DataScope Scope;

        public BindingKey(UIPropertyDefine key, DataScope scope = DataScope.LocalPlayer, int playerId = 0)
        {
            PropertyKey = key;
            Scope = scope;
            PlayerId = scope switch
            {
                DataScope.LocalPlayer => UIPropertyBinder.LocalPlayerId,
                DataScope.SpecificPlayer => playerId,
                DataScope.Global => -1,
                _ => 0
            };
        }

        public bool Equals(BindingKey other) => 
            PlayerId == other.PlayerId && 
            PropertyKey == other.PropertyKey && 
            Scope == other.Scope;

        public override int GetHashCode() => 
            HashCode.Combine(PlayerId, (int)PropertyKey, (int)Scope);
    }
    
    public enum DataScope
    {
        LocalPlayer,   // 本地玩家数据
        SpecificPlayer, // 指定玩家数据
        Global          // 全局共享数据
    }
    
    [AttributeUsage(AttributeTargets.Field)]
    public class UIPropertyTypeAttribute : Attribute
    {
        public Type ValueType { get; }

        public UIPropertyTypeAttribute(Type valueType)
        {
            ValueType = valueType;
        }
    }

    public interface IUIDatabase
    {
        
    }

    public struct ValuePropertyData : IUIDatabase, IEquatable<ValuePropertyData>, IPoolObject
    {
        public float Gold;
        public float Exp;
        public float Health;
        public float Attack;
        public float MaxHealth;
        public float Mana;
        public float MaxMana;
        public float Speed;
        public float Score;
        public float Fov;
        public float Alpha;

        public bool Equals(ValuePropertyData other)
        {
            return Gold.Equals(other.Gold) && Exp.Equals(other.Exp) && Health.Equals(other.Health) && Attack.Equals(other.Attack) && Speed.Equals(other.Speed)
                && MaxHealth.Equals(other.MaxHealth) && Mana.Equals(other.Mana) && MaxMana.Equals(other.MaxMana) && Score.Equals(other.Score) && Alpha.Equals(other.Alpha) && Fov.Equals(other.Fov);
        }

        public override bool Equals(object obj)
        {
            return obj is ValuePropertyData other && Equals(other);
        }

        public override int GetHashCode()
        {
            var hashCode = new HashCode();
            hashCode.Add(Gold);
            hashCode.Add(Exp);
            hashCode.Add(Health);
            hashCode.Add(Attack);
            hashCode.Add(MaxHealth);
            hashCode.Add(Mana);
            hashCode.Add(MaxMana);
            hashCode.Add(Speed);
            hashCode.Add(Score);
            hashCode.Add(Fov);
            hashCode.Add(Alpha);
            return hashCode.ToHashCode();
        }

        public void Init()
        {
            
        }

        public void Clear()
        {
            Gold = 0;
            Exp = 0;
            Health = 0;
            Attack = 0;
            MaxHealth = 0;
            Mana = 0;
            MaxMana = 0;
            Speed = 0;
            Score = 0;
            Fov = 0;
            Alpha = 0;
        }
    }
    
    public struct PlayerDeathTimeData : IUIDatabase
    {
        public float DeathTime;
        public bool IsDead;
    }

    public struct PlayerControlData : IUIDatabase
    {
        public ControlSkillType ControlSkill;
        public float Duration;
    }

    public struct ItemDetailData
    {
        public int ItemConfigId;
        public string ItemName;
        public QualityType Quality;
    }
}