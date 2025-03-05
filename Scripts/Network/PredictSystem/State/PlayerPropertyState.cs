using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial struct PlayerPredictablePropertyState : IPredictablePropertyState
    {
        // 使用显式字段存储键集合
        [MemoryPackOrder(0)] 
        private PropertyTypeEnum[] _propertyTypes;

        // 使用并行数组提升访问效率
        [MemoryPackOrder(1)]
        private PropertyCalculator[] _calculators;
        
        [MemoryPackOrder(2)]
        public bool IsInvisible;

        // 添加字典缓存字段
        [MemoryPackIgnore]
        private Dictionary<PropertyTypeEnum, PropertyCalculator> _propertiesCache;

        public Dictionary<PropertyTypeEnum, PropertyCalculator> Properties
        {
            get
            {
                if (_propertiesCache == null)
                {
                    RebuildCache();
                }
                return _propertiesCache;
            }
            set => _propertiesCache = value;
        }

        [MemoryPackOnSerializing]
        private void OnSerializing()
        {
            // 同步更新缓存
            if (_propertiesCache != null)
            {
                _propertyTypes = _propertiesCache.Keys.ToArray();
                _calculators = _propertiesCache.Values.ToArray();
            }
        }

        [MemoryPackOnDeserialized]
        private void OnDeserialized()
        {
            RebuildCache();
        }

        private void RebuildCache()
        {
            _propertiesCache = new Dictionary<PropertyTypeEnum, PropertyCalculator>(
                _calculators?.Length ?? 0);

            if (_calculators != null && _propertyTypes != null)
            {
                for (int i = 0; i < _calculators.Length; i++)
                {
                    // 添加重复键检测
                    if (_propertiesCache.ContainsKey(_propertyTypes[i]))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate property type: {_propertyTypes[i]}");
                    }
                    _propertiesCache[_propertyTypes[i]] = _calculators[i];
                }
            }
        }

        // 修改属性访问方式
        public PropertyCalculator GetCalculator(PropertyTypeEnum type)
        {
            return Properties.GetValueOrDefault(type);
        }

        public void SetCalculator(PropertyTypeEnum type, PropertyCalculator calculator)
        {
            Properties[type] = calculator;
            // 标记数据已修改
            _propertyTypes = null;
            _calculators = null;
        }
        
        public bool IsEqual(IPredictablePropertyState other, float tolerance = 0.01f)
        {
            if (other is not PlayerPredictablePropertyState otherState)
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
    
    [MemoryPackable]
    public partial struct PropertyCalculator
    {
        [MemoryPackable]
        public partial struct PropertyData
        {
            [MemoryPackOrder(0)] 
            public float BaseValue;
            [MemoryPackOrder(1)] 
            public float Additive;
            [MemoryPackOrder(2)] 
            public float Multiplier;
            [MemoryPackOrder(3)] 
            public float Correction;
            [MemoryPackOrder(4)] 
            public float CurrentValue;
            [MemoryPackOrder(5)] 
            public float MaxCurrentValue;
        }
        
        public float CurrentValue => _propertyData.CurrentValue;
        public float MaxCurrentValue => _propertyData.MaxCurrentValue;

        public float GetPropertyValue(BuffIncreaseType increaseType)
        {
            switch (increaseType)
            {
                case BuffIncreaseType.Base:
                    return _propertyData.BaseValue;
                case BuffIncreaseType.Multiplier:
                    return _propertyData.Multiplier;
                case BuffIncreaseType.Extra:
                    return _propertyData.Additive;
                case BuffIncreaseType.CorrectionFactor:
                    return _propertyData.Correction;
                case BuffIncreaseType.Current:
                    return _propertyData.CurrentValue;
                default:
                    throw new ArgumentOutOfRangeException(nameof(increaseType), increaseType, null);
            }
        }

        [MemoryPackOrder(0)] 
        private readonly PropertyTypeEnum _propertyType;
        [MemoryPackOrder(1)] 
        private PropertyData _propertyData;
        [MemoryPackOrder(2)] 
        private float _maxValue;
        [MemoryPackOrder(3)] 
        private float _minValue;
        [MemoryPackOrder(4)]
        private bool _isResourceProperty;
        public PropertyData PropertyDataValue => _propertyData;
        public float MaxValue => _maxValue;
        public float MinValue => _minValue;
        public PropertyTypeEnum PropertyType => _propertyType;
        public PropertyCalculator(PropertyTypeEnum propertyType, PropertyData propertyData, float maxValue, float minValue, bool isResourceProperty)
        {
            _propertyType = propertyType;
            _propertyData = propertyData;
            _maxValue = maxValue;
            _minValue = minValue;
            _isResourceProperty = isResourceProperty;
        }
        
        public bool IsResourceProperty()
        {
            return _isResourceProperty;
        }

        public PropertyCalculator UpdateCurrentValue(float value)
        {
            return new PropertyCalculator(_propertyType, new PropertyData
            {
                BaseValue = _propertyData.BaseValue,
                Additive = _propertyData.Additive,
                Multiplier = _propertyData.Multiplier,
                Correction = _propertyData.Correction,
                CurrentValue = Mathf.Clamp(value + _propertyData.CurrentValue, _minValue, 
                    IsResourceProperty() ? _propertyData.MaxCurrentValue : _maxValue),
                MaxCurrentValue = _propertyData.MaxCurrentValue
            }, _maxValue, _minValue, _isResourceProperty);
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
                    propertyData.BaseValue = Mathf.Max(0, ApplyOperation(
                        propertyData.BaseValue, 
                        data.increaseValue, 
                        data.operationType));
                    break;
                    
                case BuffIncreaseType.Multiplier:
                    propertyData.Multiplier = Mathf.Max(0, ApplyOperation(
                        propertyData.Multiplier, 
                        data.increaseValue, 
                        data.operationType));
                    break;
                    
                case BuffIncreaseType.Extra:
                    propertyData.Additive = ApplyOperation(
                        propertyData.Additive, 
                        data.increaseValue, 
                        data.operationType);
                    break;
                    
                case BuffIncreaseType.CorrectionFactor:
                    propertyData.Correction = Mathf.Max(0, ApplyOperation(
                        propertyData.Correction, 
                        data.increaseValue, 
                        data.operationType));
                    break;
                    
                case BuffIncreaseType.Current:
                    if (IsResourceProperty())
                    {
                        propertyData.CurrentValue = ApplyOperation(
                            propertyData.CurrentValue, 
                            data.increaseValue, 
                            data.operationType);
                    }
                    break;
            }

            // 计算最终值
            var newValue = (propertyData.BaseValue * propertyData.Multiplier + 
                propertyData.Additive) * propertyData.Correction;

            if (IsResourceProperty())
            {
                propertyData.MaxCurrentValue = Mathf.Clamp(newValue, _minValue, _maxValue);
                propertyData.CurrentValue = Mathf.Clamp(propertyData.CurrentValue, 
                    _minValue, Mathf.Min(newValue, _maxValue));
            }
            else
            {
                propertyData.CurrentValue = Mathf.Clamp(newValue, _minValue, _maxValue);
            }

            return new PropertyCalculator(_propertyType, propertyData, _maxValue, _minValue, _isResourceProperty);
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
                propertyData.CurrentValue,
                propertyData.Correction * data.increaseValue,
                data.operationType);

            propertyData.CurrentValue = Mathf.Clamp(newValue, _minValue, _maxValue);

            return new PropertyCalculator(_propertyType, propertyData, _maxValue, _minValue, _isResourceProperty);
        }
    }
}