using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "EffectConfig", menuName = "ScriptableObjects/EffectConfig")]
    public class EffectConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<EffectConfigData> effectConfigData = new List<EffectConfigData>();
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            effectConfigData.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var configData = new EffectConfigData();
                configData.effectId = int.Parse(data[0]);
                configData.effectType = (EffectType) Enum.Parse(typeof(EffectType), data[1]);
                configData.effectName = data[2];
                configData.description = data[3];
                configData.iconPath = data[4];
                configData.prefabPath = data[5];
                effectConfigData.Add(configData);
            }
        }
    }

    [JsonSerializable]
    [Serializable]
    public struct EffectConfigData
    {
        public int effectId;
        public EffectType effectType;
        public string effectName;
        public string description;
        public string iconPath;
        public string prefabPath;
    }

    
}