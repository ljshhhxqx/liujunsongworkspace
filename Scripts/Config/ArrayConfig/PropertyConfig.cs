using System;
using System.Collections.Generic;
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
    }
    
    [Serializable]
    public struct PropertyConfigData
    {
        public PropertyTypeEnum propertyType;
        public float baseValue;
        public float minValue;
        public float maxValue;
    }
}