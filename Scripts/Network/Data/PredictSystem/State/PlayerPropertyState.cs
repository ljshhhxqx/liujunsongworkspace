using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.State
{
    [Serializable]
    public struct PlayerPropertyState : IPropertyState
    {
        public Dictionary<PropertyTypeEnum, PropertyCalculator> Properties;
    }
    
    [Serializable]
    public struct PropertyCalculator
    {
        [Serializable]
        public struct PropertyData
        {
            public float baseValue;
            public float additive;
            public float multiplier;
            public float correction;
            public float currentValue;
            public float maxCurrentValue;
        }
        
        public float CurrentValue => _propertyData.currentValue;
        public float MaxCurrentValue => _propertyData.maxCurrentValue;
        
        private readonly PropertyTypeEnum _propertyType;
        private PropertyData _propertyData;
        private float _maxValue;
        private float _minValue;
        public PropertyCalculator(PropertyTypeEnum propertyType, PropertyData propertyData, float maxValue, float minValue)
        {
            _propertyType = propertyType;
            _propertyData = propertyData;
            _maxValue = maxValue;
            _minValue = minValue;
        }
        
        private bool IsResourceProperty(PropertyTypeEnum type)
        {
            return type.GetConsumeType() == PropertyConsumeType.Consume;
        }

        public PropertyCalculator UpdateCalculator(BuffIncreaseData data)
        {
            var propertyData = _propertyData;
            switch (data.increaseType)
            {
                case BuffIncreaseType.Base:
                    propertyData.baseValue = Mathf.Max(0, data.increaseValue + propertyData.baseValue);
                    break;
                case BuffIncreaseType.Multiplier:
                    propertyData.multiplier = Mathf.Max(0, data.increaseValue + propertyData.multiplier);
                    break;
                case BuffIncreaseType.Extra:
                    propertyData.additive += data.increaseValue;
                    break;
                case BuffIncreaseType.CorrectionFactor:
                    propertyData.correction = Mathf.Max(0, data.increaseValue + propertyData.correction);
                    break;
                case BuffIncreaseType.Current:
                    if (_propertyType == PropertyTypeEnum.Score)
                    {
                        propertyData.currentValue = Mathf.Clamp(propertyData.currentValue + propertyData.correction * data.increaseValue, 
                            _minValue, _maxValue);
                        break;
                    }
                    if (IsResourceProperty(_propertyType))
                        propertyData.currentValue += data.increaseValue;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (_propertyType != PropertyTypeEnum.Score)
            {
                var newValue = (_propertyData.baseValue * _propertyData.multiplier + _propertyData.additive) * _propertyData.correction;
                if (IsResourceProperty(_propertyType))
                {
                    propertyData.maxCurrentValue = Mathf.Clamp(newValue, _minValue, _maxValue);
                }
                else
                {
                    propertyData.currentValue = Mathf.Clamp(newValue, _minValue, _maxValue);
                } 
            }
            
            return new PropertyCalculator(_propertyType, new PropertyData
            {
                baseValue = propertyData.baseValue,
                additive = propertyData.additive,
                multiplier = propertyData.multiplier,
                correction = propertyData.correction,
                currentValue = propertyData.currentValue,
                maxCurrentValue = propertyData.maxCurrentValue
            }, _maxValue, _minValue);
        }
    }
}