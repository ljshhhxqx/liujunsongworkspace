using System;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Config
{
    [CreateAssetMenu(fileName = "DamageConfig", menuName = "ScriptableObjects/DamageConfig")]
    public class DamageConfig : ConfigBase
    {
        [SerializeField]
        private DamageData damageData;

        public float GetDamage(float attackPower, float defense, float criticalRate, float criticalDamageRatio)
        {
            var damageReduction = defense / (defense + damageData.defenseRatio);
            criticalRate = Mathf.Max(0f, Mathf.Min(1f, criticalRate));
            var isCritical = Random.Range(0f, 1f) < criticalRate;
            var damage = attackPower * (1f - damageReduction) * (isCritical? criticalDamageRatio : 1f);
            return damage;
        }


        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            
        }
    }

    [Serializable]
    public struct DamageData
    {
        //防御减伤比率
        public float defenseRatio;
    }
}