using System;
using System.Collections.Generic;
using System.Reflection;
using AOTScripts.Tool.ECS;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerPropertyComponent : NetworkMonoComponent
    {
        private ReactiveProperty<PropertyType> Health { get; } = new ReactiveProperty<PropertyType>(new PropertyType(PropertyTypeEnum.Health, 100));
        private ReactiveProperty<PropertyType> Strength { get; } = new ReactiveProperty<PropertyType>(new PropertyType(PropertyTypeEnum.Strength, 50));
        private ReactiveProperty<PropertyType> Speed { get; } = new ReactiveProperty<PropertyType>(new PropertyType(PropertyTypeEnum.Speed, 10));
        private ReactiveProperty<PropertyType> Attack { get; } = new ReactiveProperty<PropertyType>(new PropertyType(PropertyTypeEnum.Attack, 20));
        private ReactiveProperty<PropertyType> Score { get; } = new ReactiveProperty<PropertyType>(new PropertyType(PropertyTypeEnum.Score, 10));
        private readonly Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>> _properties = new Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>>();
        
        [Inject]
        private void Init()
        {
            InitializeProperties();
        }

        private void InitializeProperties()
        {
            var fields = GetType().GetFields(BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);

            foreach (var field in fields)
            {
                if (field.FieldType == typeof(ReactiveProperty<PropertyType>))
                {
                    if (field.GetValue(this) is ReactiveProperty<PropertyType> property)
                    {
                        string fieldName = field.Name;
                        if (Enum.TryParse(fieldName, out PropertyTypeEnum propertyTypeEnum))
                        {
                            _properties[propertyTypeEnum] = property;
                        }
                    }
                }
            }
        }

        public void ModifyProperty(PropertyTypeEnum type, float amount)
        {
            if (_properties.ContainsKey(type))
            {
                var property = _properties[type].Value;
                property.IncreaseValue(amount);
                _properties[type].SetValueAndForceNotify(property); // 强制通知
            }
        }

        public void RevertProperty(PropertyTypeEnum type, float amount)
        {
            if (_properties.ContainsKey(type))
            {
                var property = _properties[type].Value;
                property.DecreaseValue(amount);
                _properties[type].SetValueAndForceNotify(property); // 强制通知
            }
        }

        public ReactiveProperty<PropertyType> GetProperty(PropertyTypeEnum type)
        {
            return _properties.TryGetValue(type, out var property) ? property : null;
        }
    }
}