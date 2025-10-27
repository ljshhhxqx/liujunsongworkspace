using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
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
                var configData = new ElementConfigData();
                configData.id = int.Parse(data[0]);
                configData.elementType = (ElementType) Enum.Parse(typeof(ElementType), data[1]);
                configData.duration = float.Parse(data[2]);
                configData.count = float.Parse(data[3]);
                elementConfigData.Add(configData);
            }
        }
    }

    
}