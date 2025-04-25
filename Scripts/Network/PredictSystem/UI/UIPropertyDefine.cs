using System;
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
        [UIPropertyType(typeof(GoldData))]
        PlayerBaseData,
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
            PlayerId = scope == DataScope.Global ? -1 : playerId;
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

    public struct GoldData : IUIDatabase
    {
        public float Gold;
        public float Exp;
        public float Health;
        public float Attack;
        public float Speed;
    }

    public struct ItemDetailData
    {
        public int ItemConfigId;
        public string ItemName;
        public QualityType Quality;
    }
}