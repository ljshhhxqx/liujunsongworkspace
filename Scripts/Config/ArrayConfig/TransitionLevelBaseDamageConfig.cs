using System;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "PropertyConfig", menuName = "ScriptableObjects/PropertyConfig")]
    public class TransitionLevelBaseDamageConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<TransitionLevelBaseDamageData> frameConfigDatas = new List<TransitionLevelBaseDamageData>();
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            frameConfigDatas.Clear();
            var values = new List<(ElementReactionType, float)>();
            for (var i = 2; i < textAsset.Count; i++)
            {
                values.Clear();
                var data = textAsset[i];
                var levelBaseDamageData = new TransitionLevelBaseDamageData();
                levelBaseDamageData.level = int.Parse(data[0]);
                for (var j = 1; j < data.Length; j++)
                {
                    var elementEffectType = (ElementReactionType)(j + 3);
                    var baseValue = float.Parse(data[j]);
                    values.Add((elementEffectType, baseValue));
                }
                levelBaseDamageData.BaseValues = values;
                frameConfigDatas.Add(levelBaseDamageData);
            }
        }
        
        public float GetBaseDamage(int level, ElementReactionType elementReactionType)
        {
            var levelBaseDamageData = frameConfigDatas.Find(data => data.level == level);
            var baseValue = levelBaseDamageData.BaseValues.Find(baseValue => baseValue.Item1 == elementReactionType);
            return baseValue.Item2;
        }
    }

    [Serializable]
    public struct TransitionLevelBaseDamageData
    {
        public int level;
        public List<(ElementReactionType, float)> BaseValues;
    }
}