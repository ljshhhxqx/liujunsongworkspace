using System;
using HotUpdate.Scripts.UI.UIs.Panel.Item;

namespace HotUpdate.Scripts.Network.PredictSystem.UI
{
    public enum UIPropertyDefine
    {
        [UIPropertyType(typeof(PropertyItemData[]))]
        PlayerProperty,
    }

    public enum ReactiveCollectionEvent
    {
        Add,
        Remove,
        Clear,
        Replace,
        Move,
        CountChanged,
    }

    // 复合键结构（玩家ID + 数据Key）
    public struct BindingKey : IEquatable<BindingKey>
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
}