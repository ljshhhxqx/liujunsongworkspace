using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ElementAffinityConfig", menuName = "ScriptableObjects/ElementAffinityConfig")]
    public class ElementAffinityConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<ElementAffinityData> elementAffinityData = new List<ElementAffinityData>();
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            elementAffinityData.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var weaponConfig = new ElementAffinityData();
                weaponConfig.attackType = (ElementType) Enum.Parse(typeof(ElementType), data[0]);
                weaponConfig.defendType = (ElementType) Enum.Parse(typeof(ElementType), data[1]);
                weaponConfig.multiplier = float.Parse(data[2]);
                elementAffinityData.Add(weaponConfig);
            }
        }
        
        public float GetMultiplier(ElementType attackType, ElementType defendType)
        {
            foreach (var data in elementAffinityData)
            {
                if (data.attackType == attackType && data.defendType == defendType)
                {
                    return data.multiplier;
                }
            }

            return 1f;
        }
        
        public float GetConsumeCount(ElementType attackType, ElementType defendType)
        {
            foreach (var data in elementAffinityData)
            {
                if (data.attackType == attackType && data.defendType == defendType)
                {
                    return data.consumeCount;
                }
            }

            return 0f;
        }
    }

    [Serializable]
    public struct ElementAffinityData
    {
        public ElementType attackType;
        public ElementType defendType;
        public float multiplier;
        public float consumeCount;
    }
}