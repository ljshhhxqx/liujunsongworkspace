using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "EffectConfig", menuName = "ScriptableObjects/EffectConfig")]
    public class EffectConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<EffectConfigData> effectConfigDatas = new List<EffectConfigData>();
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            effectConfigDatas.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var data = textAsset[i];
                var effectConfigData = new EffectConfigData();
                effectConfigData.effectId = int.Parse(data[0]);
                effectConfigData.effectType = (EffectType) Enum.Parse(typeof(EffectType), data[1]);
                effectConfigData.effectName = data[2];
                effectConfigData.description = data[3];
                effectConfigData.iconPath = data[4];
                effectConfigData.prefabPath = data[5];
                effectConfigDatas.Add(effectConfigData);
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