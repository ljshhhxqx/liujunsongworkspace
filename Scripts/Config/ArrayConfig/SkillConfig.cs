using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "SkillConfig", menuName = "ScriptableObjects/SkillConfig")]
    public class SkillConfig : ConfigBase
    {
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            
        }
    }

    [Serializable]
    public struct SkillData
    {
        public int id;
        public string name;
        public SkillType type;
        public float damage;
        public DamageType damageType;
    }

    public enum SkillType : byte
    {
        Skill1,
        Skill2,
        Skill3,
        Skill4,
        Skill5,
        Skill6,
        Skill7,
        Skill8,
        Skill9,
        Skill10,
    }
    
    public enum DamageType : byte
    {
        None,
        Physical,
        Elemental,
    }
}