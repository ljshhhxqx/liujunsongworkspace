using System.Collections.Generic;
using AOTScripts.Tool.ECS;
using Mirror;
using UniRx;
using VContainer;

namespace HotUpdate.Scripts.Network.Client.Player
{
    public class PlayerPropertyComponent : NetworkMonoComponent
    {
        private readonly Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>> _properties = new Dictionary<PropertyTypeEnum, ReactiveProperty<PropertyType>>();
        private readonly SyncDictionary<PropertyTypeEnum, PropertyType> _syncProperties = new SyncDictionary<PropertyTypeEnum, PropertyType>();

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            var config = configProvider.GetConfig<PlayerDataConfig>();
            InitializeProperties(config);
        }

        private void InitializeProperties(PlayerDataConfig config)
        {
            for (var i = (int)PropertyTypeEnum.Speed; i <= (int)PropertyTypeEnum.Score; i++)
            {
                var propertyType = (PropertyTypeEnum)i;
                var configProperty = config.PlayerConfigData.MaxProperties.Find(x => x.TypeEnum == propertyType);
                if (configProperty.ValueFloat == 0)
                {
                    throw new System.Exception("Property value cannot be zero.");
                }

                var property = new PropertyType(propertyType, configProperty.ValueFloat);
                _properties.Add(propertyType, new ReactiveProperty<PropertyType>(property));
                if (isServer)
                {
                    _syncProperties.Add(propertyType, property);
                }
            }
        }

        public void ModifyProperty(PropertyTypeEnum type, float amount)
        {
            if (_syncProperties.TryGetValue(type, out var property))
            {
                property.IncreaseValue(amount);
                PropertyChanged(property);
            }
        }

        public void RevertProperty(PropertyTypeEnum type, float amount)
        {
            if (_syncProperties.TryGetValue(type, out var property))
            {
                property.DecreaseValue(amount);
                PropertyChanged(property);
            }
        }


        private void PropertyChanged(PropertyType property)
        {
            _properties[property.TypeEnum].Value = property;
            _properties[property.TypeEnum].SetValueAndForceNotify(property); 
        }
        
        public PropertyType GetProperty(PropertyTypeEnum type)
        {
            return _syncProperties.TryGetValue(type, out var property) ? property : default;
        }
    }
}