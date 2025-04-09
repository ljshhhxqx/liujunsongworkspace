using System;

namespace HotUpdate.Scripts.Network.PredictSystem.UI
{
    public enum UIPropertyDefine
    {
        
        [UIPropertyType(typeof(int))]
        Health,
    
        [UIPropertyType(typeof(int))]
        MaxHealth,
    
        [UIPropertyType(typeof(string))]
        PlayerName,
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