using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "BattleEffectConditionConfig", menuName = "ScriptableObjects/BattleEffectConditionConfig")]
    public class BattleEffectConditionConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<BattleEffectConditionConfigData> conditionList = new List<BattleEffectConditionConfigData>();
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            conditionList.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var text = textAsset[i];
                var conditionData = new BattleEffectConditionConfigData();
                conditionData.id = int.Parse(text[0]);
                conditionData.triggerType = (TriggerType) Enum.Parse(typeof(TriggerType), text[1]);
                conditionData.probability = float.Parse(text[2]);
                conditionData.effectType = (EffectType) Enum.Parse(typeof(EffectType), text[3]);
                conditionData.controlTime = float.Parse(text[4]);
                conditionData.interval = float.Parse(text[5]);
                conditionData.extraData = JsonConvert.DeserializeObject<BuffExtraData>(text[6]);
                conditionData.targetType = (ConditionTargetType) Enum.Parse(typeof(ConditionTargetType), text[7]);
                conditionData.targetCount = int.Parse(text[8]);
                conditionData.ConditionParam = JsonConvert.DeserializeObject<IConditionParam>(text[9]);
                conditionList.Add(conditionData);
            }
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct BattleEffectConditionConfigData
    {
        public int id;
        public TriggerType triggerType;
        public float probability;
        public EffectType effectType;
        public float controlTime;
        public float interval;
        public BuffExtraData extraData;
        public ConditionTargetType targetType;
        public int targetCount;
        public IConditionParam ConditionParam;
    }

    public interface IConditionParam
    {
        ConditionHeader GetConditionHeader();
        bool CheckConditionValid();
    }

    [Serializable]
    [JsonSerializable]
    public struct ConditionHeader
    {
        public TriggerType TriggerType; 
    }

    public enum AttackRangeType : byte
    {
        None,
        Melee,
        Ranged,
    }

    [Serializable]
    [JsonSerializable]
    public struct AttackHitConditionParam : IConditionParam
    {
        public Range hpRange;
        public Range damageRange;
        public AttackRangeType attackRangeType;
        
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;
        
        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f
                   && damageRange.min >= 0 && damageRange.max >= damageRange.min && damageRange.max <= 1f;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct SkillCastConditionParam : IConditionParam
    {
        public Range mpRange;
        public SkillType skillType; 
        
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && Enum.IsDefined(typeof(SkillType), skillType) && mpRange.min >= 0 && mpRange.max >= mpRange.min && mpRange.max <= 1f;
        }
    }
    
    [Serializable]
    [JsonSerializable]
    public struct TakeDamageConditionParam : IConditionParam
    {
        public Range hpRange;
        public DamageType damageType;
        public Range damageRange;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f && Enum.IsDefined(typeof(DamageType), damageType);
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct KillConditionParam : IConditionParam
    {
        public int targetCount;
        public float timeWindow;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && targetCount >= 0 && timeWindow >= 0;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct HpChangeConditionParam : IConditionParam
    {
        public Range hpRange;
        
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct MpChangeConditionParam : IConditionParam
    {
        public Range mpRange;

        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;
        
        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && mpRange.min >= 0 && mpRange.max >= mpRange.min && mpRange.max <= 1f;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct CriticalHitConditionParam : IConditionParam
    {
        public Range hpRange;
        public Range damageRange;
        public DamageType damageType;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f && 
                   damageRange.min >= 0 && damageRange.max <= 1f && damageRange.max >= damageRange.min && Enum.IsDefined(typeof(DamageType), damageType);
        }
    }
    
    [Serializable]
    [JsonSerializable]
    public struct DodgeConditionParam : IConditionParam
    {
        public int dodgeCount;
        public float dodgeRate;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && dodgeCount >= 0 && dodgeRate >= 0;
        }
    }
    
    [Serializable]
    public struct AttackConditionParam : IConditionParam
    {
        public AttackRangeType attackRangeType;
        public float attack;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && attack >= 0 && Enum.IsDefined(typeof(AttackRangeType), attackRangeType);
        }
    }
    
    [Serializable]
    public struct SkillHitConditionParam : IConditionParam
    {
        public ConditionHeader ConditionHeader;
        public Range damageRange;
        public SkillType skillType;
        public Range mpRange;
        public Range hpRange;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()        
        {
            return this.CheckConditionHeader();
        }
    }
    

    public static class ConditionExtension
    {
        public static bool CheckConditionHeader(this IConditionParam conditionParam)
        {
            var header = conditionParam.GetConditionHeader();
            return Enum.IsDefined(typeof(TriggerType), header.TriggerType);
        }
    }

    public enum TriggerType : byte
    {
        None,                //只有基础条件
        OnAttackHit,         // 攻击命中时
        OnAttack,            // 攻击时
        OnSkillHit,          // 技能命中时
        OnSkillCast,         // 技能释放时
        OnTakeDamage,        // 受到伤害时
        OnKill,              // 击杀敌人时
        OnHPChange,          // 血量低于阈值时
        OnMpChange,          // 魔法值低于阈值时
        OnCriticalHit,       // 暴击时
        OnDodge              // 闪避成功时
    }

    [Flags]
    public enum ConditionTargetType : byte
    {
        None,
        Self = 1 << 0,
        Enemy = 1 << 1,
        Ally = 1 << 2,
        Player = 1 << 3,
        Boss = Enemy | 1 << 4,
        EnemyPlayer = Enemy | Player,
        AllyPlayer = Ally | Player,
    }
}