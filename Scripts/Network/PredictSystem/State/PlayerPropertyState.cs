﻿using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using JetBrains.Annotations;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial struct PlayerPredictablePropertyState : ISyncPropertyState
    {
        [MemoryPackOrder(0)] public MemoryDictionary<PropertyTypeEnum, PropertyCalculator> MemoryProperty;
        [MemoryPackOrder(1)] public SubjectedStateType SubjectedState;
        [MemoryPackOrder(2)] public PlayerPropertyState PlayerState;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerProperty;

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine($"SubjectedState: {SubjectedState}");
            sb.AppendLine($"PlayerState: {PlayerState}");
            sb.AppendLine($"MemoryProperty:");
            foreach (var kvp in MemoryProperty)
            {
                sb.AppendLine($"  {kvp.Key.ToString()}: {kvp.Value.CurrentValue} ({kvp.Value.MaxCurrentValue})");
            }


            return sb.ToString();
        }

        // 修改属性访问方式
        public PropertyCalculator GetCalculator(PropertyTypeEnum type)
        {
            return MemoryProperty.GetValueOrDefault(type);
        }

        public void SetCalculator(PropertyTypeEnum type, PropertyCalculator calculator)
        {
            MemoryProperty[type] = calculator;
        }
        
        public bool IsEqual(ISyncPropertyState other, float tolerance = 0.01f)
        {
            if (other is not PlayerPredictablePropertyState otherState || MemoryProperty == null || otherState.MemoryProperty == null)
                return false;
            foreach (var kvp in MemoryProperty)
            {
                if (!otherState.MemoryProperty.TryGetValue(kvp.Key, out var otherCalculator))
                    return false;

                // 只比较最终计算值
                float currentDiff = Mathf.Abs(kvp.Value.CurrentValue - otherCalculator.CurrentValue);
                float maxDiff = Mathf.Abs(kvp.Value.MaxCurrentValue - otherCalculator.MaxCurrentValue);

                if (currentDiff > tolerance || maxDiff > tolerance)
                {
                    // Debug.Log($"Property {kvp.Key} difference detected: " +
                    //           $"Current={currentDiff}, Max={maxDiff}, Tolerance={tolerance}");
                    return false;
                }
            }

            return true;
        }
    }
    [MemoryPackable]
    public partial struct PlayerPropertyState
    {
        // public bool IsSprinting;
        // public bool IsJumping;
        // public bool IsInputMovement;
        // public PlayerEnvironmentState PlayerEnvironmentState; 
        [MemoryPackOrder(0)]
        public float CurrentMoveSpeed;
    }
    
    [MemoryPackable]
    public partial struct PropertyCalculator : IEquatable<PropertyCalculator>
    {
        /// <summary>
        /// 属性 = math.clamp(（基础值 * 乘数 + 附加值 ）* 修正系数,  最小值, 最大值)
        /// </summary>
        [MemoryPackable]
        public partial struct PropertyData : IEquatable<PropertyData>
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

            public bool Equals(PropertyData other)
            {
                return BaseValue.Equals(other.BaseValue) && Additive.Equals(other.Additive) && Multiplier.Equals(other.Multiplier) && Correction.Equals(other.Correction) && CurrentValue.Equals(other.CurrentValue) && MaxCurrentValue.Equals(other.MaxCurrentValue);
            }

            public override bool Equals(object obj)
            {
                return obj is PropertyData other && Equals(other);
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(BaseValue, Additive, Multiplier, Correction, CurrentValue, MaxCurrentValue);
            }

            public static bool operator ==(PropertyData left, PropertyData right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(PropertyData left, PropertyData right)
            {
                return !left.Equals(right);
            }
        }

        public static List<(BuffIncreaseType, float)> GetDifferences(PropertyCalculator oldCalculator,
            PropertyCalculator newCalculator)
        {
            var differences = new List<(BuffIncreaseType, float)>();
            if (oldCalculator.Equals(newCalculator))
            {
                return differences;
            }
            
            differences.Add((BuffIncreaseType.Current, newCalculator._propertyData.CurrentValue - oldCalculator._propertyData.CurrentValue));
            differences.Add((BuffIncreaseType.Multiplier, newCalculator._propertyData.Multiplier - oldCalculator._propertyData.Multiplier));
            differences.Add((BuffIncreaseType.Extra, newCalculator._propertyData.Additive - oldCalculator._propertyData.Additive));
            differences.Add((BuffIncreaseType.CorrectionFactor, newCalculator._propertyData.Correction - oldCalculator._propertyData.Correction));
            differences.Add((BuffIncreaseType.Base, newCalculator._propertyData.BaseValue - oldCalculator._propertyData.BaseValue));
            differences.Add((BuffIncreaseType.Max, newCalculator._propertyData.MaxCurrentValue - oldCalculator._propertyData.MaxCurrentValue));
            differences.RemoveAll(x => x.Item2 == 0);
            
            return differences;
        }

        [MemoryPackIgnore]
        public float CurrentValue => _propertyData.CurrentValue;
        [MemoryPackIgnore]
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

        public PropertyCalculator SetPropertyValue(BuffIncreaseType increaseType, float newValue)
        {
            switch (increaseType)
            {
                case BuffIncreaseType.Base:
                    _propertyData.BaseValue = newValue;
                    break;
                case BuffIncreaseType.Multiplier:
                    _propertyData.Multiplier = newValue;
                    break;
                case BuffIncreaseType.Extra:
                    _propertyData.Additive = newValue;
                    break;
                case BuffIncreaseType.CorrectionFactor:
                    _propertyData.Correction = newValue;
                    break;
                case BuffIncreaseType.Current:
                    if (IsResourceProperty())
                    {
                        _propertyData.CurrentValue = newValue;
                        Debug.Log($"SetPropertyValue NewValue: {newValue}");
                        return new PropertyCalculator(_propertyType, _propertyData, _maxValue, _minValue, _isResourceProperty);
                    }
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(increaseType), increaseType, null);
            }

            // 计算最终值
            var currentValue = (_propertyData.BaseValue * _propertyData.Multiplier + _propertyData.Additive) * _propertyData.Correction;

            if (IsResourceProperty())
            {
                _propertyData.MaxCurrentValue = Mathf.Clamp(currentValue, _minValue, _maxValue);
                _propertyData.CurrentValue = Mathf.Clamp(_propertyData.CurrentValue, _minValue, 
                    Mathf.Min(currentValue, _maxValue));
            }
            else
            {
                _propertyData.CurrentValue = Mathf.Clamp(currentValue, _minValue, _maxValue);
            }

            return new PropertyCalculator(_propertyType, _propertyData, _maxValue, _minValue, _isResourceProperty);
        }

        [MemoryPackOrder(0)] 
        private PropertyTypeEnum _propertyType;
        [MemoryPackOrder(1)] 
        private PropertyData _propertyData;
        [MemoryPackOrder(2)] 
        private float _maxValue;
        [MemoryPackOrder(3)] 
        private float _minValue;
        [MemoryPackOrder(4)]
        private bool _isResourceProperty;
        [MemoryPackIgnore]
        public PropertyData PropertyDataValue => _propertyData;
        [MemoryPackIgnore]
        public float MaxValue => _maxValue;
        [MemoryPackIgnore]
        public float MinValue => _minValue;
        [MemoryPackIgnore]
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

        public bool IsBaseData()
        {
            return _propertyType is PropertyTypeEnum.Speed or PropertyTypeEnum.Health or PropertyTypeEnum.Attack
                or PropertyTypeEnum.Gold or PropertyTypeEnum.Experience;
        }

        public bool IsPercentage()
        {
            return _propertyType is PropertyTypeEnum.AttackSpeed or PropertyTypeEnum.CriticalRate
                or PropertyTypeEnum.CriticalDamageRatio;
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
        public PropertyCalculator UpdateCurrentValueByRatio(float ratio)
        {
            var newValue = _propertyData.CurrentValue * ratio;
            return new PropertyCalculator(_propertyType, new PropertyData
            {
                BaseValue = _propertyData.BaseValue,
                Additive = _propertyData.Additive,
                Multiplier = _propertyData.Multiplier,
                Correction = _propertyData.Correction,
                CurrentValue = Mathf.Clamp(newValue, _minValue, 
                    IsResourceProperty() ? _propertyData.MaxCurrentValue : _maxValue),
                MaxCurrentValue = _propertyData.MaxCurrentValue
            }, _maxValue, _minValue, _isResourceProperty);
        }

        public PropertyCalculator UpdateCalculator(List<BuffIncreaseData> data, bool isReverse = false)
        {
            if (data == null || data.Count == 0)
                return this;

            var propertyCalculator = this;
            foreach (var buff in data)
            {
                propertyCalculator = UpdateCalculator(propertyCalculator, buff, isReverse);
            }

            return propertyCalculator;
        }

        public PropertyCalculator UpdateCalculator(PropertyCalculator calculator, BuffIncreaseData data, bool isReverse = false)
        {
            if (_propertyType == PropertyTypeEnum.Gold || _propertyType == PropertyTypeEnum.Score)
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
                        data.operationType, isReverse));
                    break;
                    
                case BuffIncreaseType.Multiplier:
                    propertyData.Multiplier = Mathf.Max(0, ApplyOperation(
                        propertyData.Multiplier, 
                        data.increaseValue, 
                        data.operationType, isReverse));
                    break;
                    
                case BuffIncreaseType.Extra:
                    propertyData.Additive = ApplyOperation(
                        propertyData.Additive, 
                        data.increaseValue, 
                        data.operationType, isReverse);
                    break;
                    
                case BuffIncreaseType.CorrectionFactor:
                    propertyData.Correction = Mathf.Max(0, ApplyOperation(
                        propertyData.Correction, 
                        data.increaseValue, 
                        data.operationType, isReverse));
                    break;
                    
                case BuffIncreaseType.Current:
                    if (IsResourceProperty())
                    {
                        propertyData.CurrentValue = ApplyOperation(
                            propertyData.CurrentValue, 
                            data.increaseValue, 
                            data.operationType, isReverse);
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
        
        private float ApplyOperation(float original, float value, BuffOperationType operation, bool isReverse = false)
        {
            return operation switch
            {
                BuffOperationType.Add => isReverse ? original - value :  original + value,
                BuffOperationType.Subtract => isReverse ? original + value : original - value,
                BuffOperationType.Multiply => isReverse ? original / value : original * value,
                BuffOperationType.Divide => isReverse ? original * value : original / value,
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

        public bool Equals(PropertyCalculator other)
        {
            return _propertyType == other._propertyType && _propertyData.Equals(other._propertyData) && _maxValue.Equals(other._maxValue) && _minValue.Equals(other._minValue) && _isResourceProperty == other._isResourceProperty;
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyCalculator other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)_propertyType, _propertyData, _maxValue, _minValue, _isResourceProperty);
        }

        public static bool operator ==(PropertyCalculator left, PropertyCalculator right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PropertyCalculator left, PropertyCalculator right)
        {
            return !left.Equals(right);
        }
    }
    
    /// <summary>
    /// 挂载在玩家身上或者敌人身上的元素状态
    /// </summary>
    [MemoryPackable]
    public partial struct ElementState
    {
        [MemoryPackOrder(0)]
        public ElementGaugeData MainGaugeData;
        
        [MemoryPackOrder(1)]
        public ElementGaugeData SubGaugeData;

        [MemoryPackOrder(2)] 
        //是否是元素生物
        public ElementType MainElementType;
        
        [MemoryPackOrder(3)]
        //元素生物的MainElementType对应的元素减伤比例(非元素生物为0)
        public float ElementDamageReduction;
        
        [MemoryPackOrder(4)]
        public float ElementShieldHealth;

        [MemoryPackOrder(5)]
        //如果为共存元素，当前的元素状态
        public ElementType DoubleType; 
        
        [MemoryPackIgnore]
        public bool HasMainElement => MainGaugeData.ElementType != ElementType.None;

        [MemoryPackIgnore]
        public bool HasSubElement => MainGaugeData.ElementType != ElementType.None;
    }

    /// <summary>
    /// 元素反应的最小数据结构
    /// </summary>
    [MemoryPackable]
    public partial struct ElementGaugeData : IEquatable<ElementGaugeData>
    {
        [MemoryPackOrder(0)]
        public int Id;
        [MemoryPackOrder(1)]
        public ElementType ElementType;
        [MemoryPackOrder(2)]
        public float GaugeUnits;
        [MemoryPackOrder(3)]
        public ElementStrength Strength;
        [MemoryPackOrder(4)] 
        public float DecayRate;

        public bool Equals(ElementGaugeData other)
        {
            return Id == other.Id && ElementType == other.ElementType && GaugeUnits.Equals(other.GaugeUnits) && Strength == other.Strength && DecayRate.Equals(other.DecayRate);
        }

        public override bool Equals([CanBeNull] object obj)
        {
            return obj is ElementGaugeData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(Id, (int)ElementType, GaugeUnits, (int)Strength, DecayRate);
        }
    }
    
    [MemoryPackable]
    public partial struct PlayerSubjectedState
    {
        [MemoryPackOrder(0)]
        public ImmutableList<SubjectedState> SubjectedStates;
        
        public bool HasState(SubjectedStateType stateType)
        {
            return SubjectedStates.Any(s => s.SubjectedStateType == stateType);
        }

        public PlayerSubjectedState AddState(SubjectedState state)
        {
            SubjectedStates = SubjectedStates.Add(state);
            var playerState = new PlayerSubjectedState();
            playerState.SubjectedStates = SubjectedStates;
            return playerState;
        }
        public PlayerSubjectedState RemoveState(SubjectedStateType stateType)
        {
            SubjectedStates = SubjectedStates.RemoveAll(s => s.SubjectedStateType == stateType);
            var playerState = new PlayerSubjectedState();
            playerState.SubjectedStates = SubjectedStates;
            return playerState;
        }

        public PlayerSubjectedState UpdateState(float deltaTime)
        {
            for (var i = SubjectedStates.Count - 1; i <= 0; i--)
            {
                var state = SubjectedStates[i];
                state.RemainingDuration -= deltaTime;
                if (state.RemainingDuration <= 0)
                {
                    SubjectedStates = SubjectedStates.RemoveAt(i);
                }
                else
                {
                    SubjectedStates = SubjectedStates.SetItem(i, state); 
                }
            }
            var playerState = new PlayerSubjectedState();
            playerState.SubjectedStates = SubjectedStates;
            return playerState;
        }

    }

    [MemoryPackable]
    public partial struct SubjectedState
    {
        [MemoryPackOrder(0)]
        public SubjectedStateType SubjectedStateType;
        [MemoryPackOrder(1)]
        public float Duration;
        [MemoryPackOrder(2)]
        public float RemainingDuration;
    }

    [Flags]
    public enum SubjectedStateType : byte
    {
        None = 0,
        IsInvisible = 1 << 0,
        IsFrozen = 1 << 1,
        IsElectrified = 1 << 2,
        IsBlowup = 1 << 3,
        IsStunned = 1 << 4,
        IsDead = 1 << 5,
        IsCantMoved = 1 << 6,
    }
}