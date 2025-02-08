using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Data.PredictSystem.Data;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.Data.PredictSystem.State
{
    [Serializable]
    public struct PlayerPropertyState : IPropertyState
    {
        public Dictionary<PropertyTypeEnum, PropertyCalculator> Properties;
        public bool IsEqual(IPropertyState other, float tolerance = 0.01f)
        {
            if (other is not PlayerPropertyState otherState)
                return false;
            foreach (var kvp in Properties)
            {
                if (!otherState.Properties.TryGetValue(kvp.Key, out var otherCalculator))
                    return false;

                // 只比较最终计算值
                float currentDiff = Mathf.Abs(kvp.Value.CurrentValue - otherCalculator.CurrentValue);
                float maxDiff = Mathf.Abs(kvp.Value.MaxCurrentValue - otherCalculator.MaxCurrentValue);

                if (currentDiff > tolerance || maxDiff > tolerance)
                {
                    Debug.Log($"Property {kvp.Key} difference detected: " +
                              $"Current={currentDiff}, Max={maxDiff}, Tolerance={tolerance}");
                    return false;
                }
            }

            return true;
        }
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

        public float GetPropertyValue(BuffIncreaseType increaseType)
        {
            switch (increaseType)
            {
                case BuffIncreaseType.Base:
                    return _propertyData.baseValue;
                case BuffIncreaseType.Multiplier:
                    return _propertyData.multiplier;
                case BuffIncreaseType.Extra:
                    return _propertyData.additive;
                case BuffIncreaseType.CorrectionFactor:
                    return _propertyData.correction;
                case BuffIncreaseType.Current:
                    return _propertyData.currentValue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(increaseType), increaseType, null);
            }
        }

        private readonly PropertyTypeEnum _propertyType;
        private PropertyData _propertyData;
        private float _maxValue;
        private float _minValue;
        public PropertyData PropertyDataValue => _propertyData;
        public float MaxValue => _maxValue;
        public float MinValue => _minValue;
        public PropertyCalculator(PropertyTypeEnum propertyType, PropertyData propertyData, float maxValue, float minValue)
        {
            _propertyType = propertyType;
            _propertyData = propertyData;
            _maxValue = maxValue;
            _minValue = minValue;
        }
        
        public bool IsResourceProperty()
        {
            return _propertyType.GetConsumeType() == PropertyConsumeType.Consume;
        }

        public PropertyCalculator UpdateCurrentValue(float value)
        {
            return new PropertyCalculator(_propertyType, new PropertyData
            {
                baseValue = _propertyData.baseValue,
                additive = _propertyData.additive,
                multiplier = _propertyData.multiplier,
                correction = _propertyData.correction,
                currentValue = Mathf.Clamp(value + _propertyData.currentValue, _minValue, 
                    IsResourceProperty() ? _propertyData.maxCurrentValue : _maxValue),
                maxCurrentValue = _propertyData.maxCurrentValue
            }, _maxValue, _minValue);
        }

        public PropertyCalculator UpdateCalculator(List<BuffIncreaseData> data)
        {
            if (data == null || data.Count == 0)
                return this;

            var propertyCalculator = this;
            foreach (var buff in data)
            {
                propertyCalculator = UpdateCalculator(propertyCalculator, buff);
            }

            return propertyCalculator;
        }

        public PropertyCalculator UpdateCalculator(PropertyCalculator calculator, BuffIncreaseData data)
        {
            if (_propertyType == PropertyTypeEnum.Score)
            {
                return HandleScoreUpdate(calculator, data);
            }

            var propertyData = calculator._propertyData;
            
            // 根据增益类型更新相应的值
            switch (data.increaseType)
            {
                case BuffIncreaseType.Base:
                    propertyData.baseValue = Mathf.Max(0, ApplyOperation(
                        propertyData.baseValue, 
                        data.increaseValue, 
                        data.operationType));
                    break;
                    
                case BuffIncreaseType.Multiplier:
                    propertyData.multiplier = Mathf.Max(0, ApplyOperation(
                        propertyData.multiplier, 
                        data.increaseValue, 
                        data.operationType));
                    break;
                    
                case BuffIncreaseType.Extra:
                    propertyData.additive = ApplyOperation(
                        propertyData.additive, 
                        data.increaseValue, 
                        data.operationType);
                    break;
                    
                case BuffIncreaseType.CorrectionFactor:
                    propertyData.correction = Mathf.Max(0, ApplyOperation(
                        propertyData.correction, 
                        data.increaseValue, 
                        data.operationType));
                    break;
                    
                case BuffIncreaseType.Current:
                    if (IsResourceProperty())
                    {
                        propertyData.currentValue = ApplyOperation(
                            propertyData.currentValue, 
                            data.increaseValue, 
                            data.operationType);
                    }
                    break;
            }

            // 计算最终值
            var newValue = (propertyData.baseValue * propertyData.multiplier + 
                propertyData.additive) * propertyData.correction;

            if (IsResourceProperty())
            {
                propertyData.maxCurrentValue = Mathf.Clamp(newValue, _minValue, _maxValue);
                propertyData.currentValue = Mathf.Clamp(propertyData.currentValue, 
                    _minValue, Mathf.Min(newValue, _maxValue));
            }
            else
            {
                propertyData.currentValue = Mathf.Clamp(newValue, _minValue, _maxValue);
            }

            return new PropertyCalculator(_propertyType, propertyData, _maxValue, _minValue);
        }
        
        private float ApplyOperation(float original, float value, BuffOperationType operation)
        {
            return operation switch
            {
                BuffOperationType.Add => original + value,
                BuffOperationType.Subtract => original - value,
                BuffOperationType.Multiply => original * value,
                BuffOperationType.Divide => value != 0 ? original / value : original,
                _ => original + value // 默认加法
            };
        }

        private PropertyCalculator HandleScoreUpdate(PropertyCalculator calculator, 
            BuffIncreaseData data)
        {
            if (data.increaseType != BuffIncreaseType.Current)
                return calculator;

            var propertyData = calculator._propertyData;
            var newValue = ApplyOperation(
                propertyData.currentValue,
                propertyData.correction * data.increaseValue,
                data.operationType);

            propertyData.currentValue = Mathf.Clamp(newValue, _minValue, _maxValue);

            return new PropertyCalculator(_propertyType, propertyData, _maxValue, _minValue);
        }
    }
}