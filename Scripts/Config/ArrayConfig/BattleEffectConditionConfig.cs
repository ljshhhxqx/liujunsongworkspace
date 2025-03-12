using System;
using System.Collections.Generic;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "BattleEffectConditionConfig", menuName = "ScriptableObjects/BattleEffectConditionConfig")]
    public class BattleEffectConditionConfig : ConfigBase
    {
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            
        }
    }

    [Serializable]
    public struct BattleEffectConditionData
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

    public struct ConditionHeader
    {
        public TriggerType TriggerType; 
    }

    [Serializable]
    public struct AttackHitConditionParam : IConditionParam
    {
        public Range hpRange;
        public Range damageRange;
        
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;
        
        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f
                   && damageRange.min >= 0 && damageRange.max >= damageRange.min && damageRange.max <= 1f;
        }
    }

    [Serializable]
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
    public struct TakeDamageConditionParam : IConditionParam
    {
        public Range hpRange;
        public ConditionTargetType targetType;
        public DamageType damageType;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f &&
                   Enum.IsDefined(typeof(ConditionTargetType), targetType) && Enum.IsDefined(typeof(DamageType), damageType);
        }
    }

    [Serializable]
    public struct KillConditionParam : IConditionParam
    {
        public ConditionTargetType targetType;
        public int targetCount;
        public float timeWindow;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && targetCount >= 0 && timeWindow >= 0 && Enum.IsDefined(typeof(ConditionTargetType), targetType);
        }
    }

    [Serializable]
    public struct HPBelowThresholdConditionParam : IConditionParam
    {
        public float hpThreshold;
        public float duration;
        
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpThreshold >= 0 && duration >= 0;
        }
    }

    [Serializable]
    public struct MpBelowThresholdConditionParam : IConditionParam
    {
        public float mpThreshold;
        public float duration;

        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;
        
        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && mpThreshold >= 0 && duration >= 0;
        }
    }

    [Serializable]
    public struct IntervalConditionParam : IConditionParam
    {
        public float interval;
        
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && interval >= 0;
        }
    }

    [Serializable]
    public struct CriticalHitConditionParam : IConditionParam
    {
        public Range hpRange;
        public Range damageRange;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f && 
                   damageRange.min >= 0 && damageRange.max <= 1f && damageRange.max >= damageRange.min;
        }
    }
    
    [Serializable]
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
        None,
        OnAttackHit,         // 攻击命中时
        OnSkillCast,         // 技能释放时
        OnTakeDamage,        // 受到伤害时
        OnKill,              // 击杀敌人时
        OnHPBelowThreshold,  // 血量低于阈值时
        OnMpBelowThreshold,  // 魔法值低于阈值时
        OnInterval,          // 周期性触发
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