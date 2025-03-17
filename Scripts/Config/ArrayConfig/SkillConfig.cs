using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "SkillConfig", menuName = "ScriptableObjects/SkillConfig")]
    public class SkillConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<SkillData> skillData = new List<SkillData>();
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            skillData.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var text = textAsset[i];
                var data = new SkillData();
                data.id = int.Parse(text[0]);
                data.name = text[1];
                data.type = (SkillType) Enum.Parse(typeof(SkillType), text[2]);
                data.damage = float.Parse(text[3]);
                data.damageType = (DamageType) Enum.Parse(typeof(DamageType), text[4]);
                skillData.Add(data);
            }
        }
    }

    [Serializable]
    [JsonSerializable]
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
    
    [Flags]
    public enum DamageType : byte
    {
        None,
        Physical = 1 << 0,
        Elemental  = 1 << 1,
        All = Physical | Elemental
    }
}