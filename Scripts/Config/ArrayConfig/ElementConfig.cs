using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
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

    [JsonSerializable]
    [Serializable]
    public struct ElementConfigData
    {
        public int id;
        public ElementType elementType;
        public float duration;
        public float count;
    }

    /// <summary>
    /// 元素类型 ：火、水、冰、雷、土、风、草；特殊：冰冻、感电、激化
    /// </summary>
    [Flags]
    public enum ElementType : byte
    {
        None,
        Fire = 1 << 0,
        Water = 1 << 1,
        Ice = 1 << 2,
        Thunder = 1 << 3,
        Roil = 1 << 4,
        Wind = 1 << 5,
        Grass = 1 << 6,
        //冻元素(水+冰)
        Frozen = 1 << 1 | 1 << 2,
        //感电元素(水+雷)
        Electrified = 1 << 1 | 1 << 3,
        //激元素(草+雷)
        OriginalEvaporate = 1 << 3 | 1 << 6,
    }

    public enum ElementStrength : byte
    {
        None,
        Weak,
        Strong,
        SuperStrong
    }
}