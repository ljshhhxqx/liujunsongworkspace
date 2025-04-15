using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using Sirenix.OdinInspector;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;
using Random = System.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "PropertyConfig", menuName = "ScriptableObjects/PropertyConfig")]
    public class PropertyConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<PropertyConfigData> propertyData;
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            propertyData.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var data = new PropertyConfigData();
                data.propertyType = (PropertyTypeEnum) Enum.Parse(typeof(PropertyTypeEnum), row[0]);
                data.baseValue = float.Parse(row[1]);
                data.minValue = float.Parse(row[2]);
                data.maxValue = float.Parse(row[3]);
                data.consumeType = (PropertyConsumeType) Enum.Parse(typeof(PropertyConsumeType), row[4]);
                data.isHandleWithCorrectFactor = bool.Parse(row[5]);
                data.description = row[6];
                data.animationState = (AnimationState) Enum.Parse(typeof(AnimationState), row[7]);
                data.isHundredPercent = bool.Parse(row[8]);
                propertyData.Add(data);
            }
        }
        
        public PropertyConfigData GetPropertyConfigData(PropertyTypeEnum propertyType)
        {
            return propertyData.Find(x => x.propertyType == propertyType);
        }
        
        public bool IsHundredPercent(PropertyTypeEnum type)
        {
            return propertyData.Find(x => x.propertyType == type).isHundredPercent;
        }

        public PropertyConsumeType GetPropertyConsumeType(PropertyTypeEnum type)
        {
            return propertyData.Find(x => x.propertyType == type).consumeType;
        }
        
        public string GetDescription(PropertyTypeEnum type)
        {
            return propertyData.Find(x => x.propertyType == type).description;
        }
        
        public bool IsHandleWithCorrectFactor(PropertyTypeEnum type)
        {
            return propertyData.Find(x => x.propertyType == type).isHandleWithCorrectFactor;
        }

        public Dictionary<PropertyTypeEnum, float> GetPlayerBaseProperties()
        {
            return propertyData.ToDictionary(x => x.propertyType, x => x.baseValue);
        }

        public Dictionary<PropertyTypeEnum, float> GetPlayerMaxProperties()
        {
            return propertyData.ToDictionary(x => x.propertyType, x => x.maxValue);
        }

        public Dictionary<PropertyTypeEnum, float> GetPlayerMinProperties()
        {
            return propertyData.ToDictionary(x => x.propertyType, x => x.minValue);
        }

        public AttackConfigData GetAttackBaseParams()
        {
            var attackRange = propertyData.Find(x => x.propertyType == PropertyTypeEnum.AttackAngle).baseValue;
            var attackRadius = propertyData.Find(x => x.propertyType == PropertyTypeEnum.AttackRadius).baseValue;
            var attackHeight = propertyData.Find(x => x.propertyType == PropertyTypeEnum.AttackHeight).baseValue;
            return new AttackConfigData(attackRadius, attackRange, attackHeight);
        }
        
        public (float, float) GetMinMaxProperty(PropertyTypeEnum type)
        {
            var property = propertyData.Find(x => x.propertyType == type);
            return (property.minValue, property.maxValue);  
        }

        public PropertyTypeEnum GetPropertyType(AnimationState animationState)
        {
            return propertyData.Find(x => x.animationState == animationState).propertyType;
        }

        public float GetBaseValue(PropertyTypeEnum type)
        {
            return propertyData.Find(x => x.propertyType == type).baseValue;
        }

        public float CalculatePropertyValue(PropertyTypeEnum type, float multiplier)
        {
            var baseValue = GetBaseValue(type);
            var configData = propertyData.Find(x => x.propertyType == type);
            return configData.isHundredPercent ? baseValue * multiplier * 0.01f : baseValue * multiplier;
        }

        public IEnumerable<(PropertyTypeEnum, BuffIncreaseType, float)> GetItemDescriptionProperties(string itemPropertyDescription)
        {
            if (string.IsNullOrEmpty(itemPropertyDescription))
            {
                yield break;
            }
            var strs = itemPropertyDescription.Split(',');
            foreach (var str in strs)
            {
                var strs1 = str.Split('|');
                var increaseTypeStr = strs1[0].Trim().Replace("[", "").Replace("]", "");
                var increaseType = (BuffIncreaseType) Enum.Parse(typeof(BuffIncreaseType), increaseTypeStr);
                var propertyStr = strs1[1].Trim();
                var property = propertyData.Find(x => str.Contains(x.description));
                if (property.description != null)
                {
                    var value = propertyStr.Split('+')[1];
                    if (value.Contains("%"))
                    {
                        value = value.Replace("%", "");
                        yield return (property.propertyType, increaseType, float.Parse(value) / 100f);
                        continue;
                    }
                    yield return (property.propertyType, increaseType, float.Parse(value));
                }
                else
                {
                    throw new Exception($"提供的“{itemPropertyDescription}”中没有找到属性描述“{str}”");
                }
            }
        }
    }
    
    [Serializable]
    public struct PropertyConfigData
    {
        public PropertyTypeEnum propertyType;
        public float baseValue;
        public float minValue;
        public float maxValue;
        public PropertyConsumeType consumeType;
        public bool isHandleWithCorrectFactor;
        public string description;
        //关联的动画状态
        public AnimationState animationState;
        public bool isHundredPercent;
        public bool showInHud;
    }
}