using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

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
                propertyData.Add(data);
            }
        }
        
        public PropertyConfigData GetPropertyConfigData(PropertyTypeEnum propertyType)
        {
            return propertyData.Find(x => x.propertyType == propertyType);
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
    }
}