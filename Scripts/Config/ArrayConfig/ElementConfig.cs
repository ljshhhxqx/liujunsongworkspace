using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ElementConfig", menuName = "ScriptableObjects/ElementConfig")]
    public class ElementConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<ElementConfigData> elementConfigData = new List<ElementConfigData>();
        
        public ElementConfigData GetElementConfigData(int id)
        {
            foreach (var elementConfig in elementConfigData)
            {
                if (elementConfig.id == id)
                {
                    return elementConfig;
                }
            }

            return new ElementConfigData();
        }
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            elementConfigData.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var weaponConfig = new ElementConfigData();
                weaponConfig.id = int.Parse(data[0]);
                weaponConfig.elementType = (ElementType) Enum.Parse(typeof(ElementType), data[1]);
                weaponConfig.duration = float.Parse(data[2]);
                weaponConfig.count = float.Parse(data[3]);
                elementConfigData.Add(weaponConfig);
            }
        }
    }

    [Serializable]
    public struct ElementConfigData
    {
        public int id;
        public ElementType elementType;
        public float duration;
        public float count;
    }

    /// <summary>
    /// 元素类型 ：火、水、冰、雷、土、风
    /// </summary>
    public enum ElementType : byte
    {
        None,
        Fire,
        Water,
        Ice,
        Thunder,
        Roil,
        Wind,
    }
}