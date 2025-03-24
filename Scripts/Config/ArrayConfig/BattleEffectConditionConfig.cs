using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using CustomEditor.Scripts;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using Random = System.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "BattleEffectConditionConfig",
        menuName = "ScriptableObjects/BattleEffectConditionConfig")]
    public class BattleEffectConditionConfig : ConfigBase
    {
        [ReadOnly] [SerializeField]
        private List<BattleEffectConditionConfigData> conditionList = new List<BattleEffectConditionConfigData>();
#if UNITY_EDITOR
        public BattleEffectConditionConfigData effectData;
#endif

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            conditionList.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var text = textAsset[i];
                var conditionData = new BattleEffectConditionConfigData();
                conditionData.id = int.Parse(text[0]);
                conditionData.triggerType = (TriggerType)Enum.Parse(typeof(TriggerType), text[1]);
                conditionData.probability = float.Parse(text[2]);
                conditionData.interval = float.Parse(text[3]);
                conditionData.targetType = (ConditionTargetType)Enum.Parse(typeof(ConditionTargetType), text[4]);
                conditionData.targetCount = int.Parse(text[5]);
                conditionData.conditionParam = JsonConvert.DeserializeObject<IConditionParam>(text[6]);
                conditionList.Add(conditionData);
            }
        }
        public BattleEffectConditionConfigData GenerateConfig(int id, Rarity rarity, Random random)
        {
            var config = new BattleEffectConditionConfigData();

            // 稀有度决定权重
            float weightMultiplier = rarity == Rarity.Rare ? 0.5f : 0.75f;

            // 随机选择 TriggerType
            TriggerType[] triggerTypes = Enum.GetValues(typeof(TriggerType)).Cast<TriggerType>().ToArray();
            config.triggerType = triggerTypes[random.Next(1, triggerTypes.Length)]; // 跳过 None

            // 根据触发频率调整其他参数
            float fType = GetTriggerTypeFrequency(config.triggerType);
            if (fType >= 0.8f) // 高频率
            {
                config.probability = (float)(random.Next(50, 91)) / 100f; // 0.5 ~ 0.9
                config.interval = random.Next(0, 3); // 0 ~ 2 秒
            }
            else if (fType >= 0.3f) // 中等频率
            {
                config.probability = (float)(random.Next(60, 100)) / 100f; // 0.6 ~ 1.0
                config.interval = random.Next(1, 4); // 1 ~ 3 秒
            }
            else // 低频率
            {
                config.probability = (float)(random.Next(70, 100)) / 100f; // 0.7 ~ 1.0
                config.interval = random.Next(2, 6); // 2 ~ 5 秒
            }

            // 随机 conditionParam
            config.conditionParam = random.Next(0, 2) == 0 ? null : new GenericConditionParam();

            // 目标类型和数量
            config.targetType = random.Next(0, 2) == 0 ? ConditionTargetType.Single : ConditionTargetType.All;
            config.targetCount = config.targetType == ConditionTargetType.Single ? 1 : random.Next(2, 6);

            config.id = id;
            return config;
        }

        public enum Rarity { Rare, Legendary }

        
        public static float CalculatePassiveMultiplier(BattleEffectConditionConfigData config, float baseValue, float passiveWeight, float baseFrequency = 1.0f)
        {
            // 步骤 1：计算最大值和最小值
            float mMax = passiveWeight / baseValue;
            float mMin = mMax * 0.6f;

            // 步骤 2：计算触发系数
            float fType = GetTriggerTypeFrequency(config.triggerType);
            float fTrigger = (config.interval == 0) ? 1.0f : Math.Min(1.0f, baseFrequency / (fType * config.interval));
            float pTrigger = config.probability;
            float cParam = (config.conditionParam == null || !HasValuableParam(config.conditionParam)) ? 1.0f : 1.25f;
            float eTrigger = fTrigger * pTrigger * cParam;

            // 步骤 3：插值计算实际乘数
            float mActual = mMax - (mMax - mMin) * eTrigger;

            return mActual;
        }

        private static float GetTriggerTypeFrequency(TriggerType type)
        {
            switch (type)
            {
                case TriggerType.None: return 0.0f;
                case TriggerType.OnAttackHit: return 1.0f;
                case TriggerType.OnAttack: return 1.0f;
                case TriggerType.OnSkillHit: return 0.5f;
                case TriggerType.OnSkillCast: return 0.5f;
                case TriggerType.OnTakeDamage: return 0.5f;
                case TriggerType.OnKill: return 0.1f;
                case TriggerType.OnHpChange: return 0.8f;
                case TriggerType.OnManaChange: return 0.6f;
                case TriggerType.OnCriticalHit: return 0.2f;
                case TriggerType.OnDodge: return 0.3f;
                case TriggerType.OnDeath: return 0.05f;
                default: return 1.0f;
            }
        }

        private static bool HasValuableParam(IConditionParam param)
        {
            // 实现逻辑：检查 param 是否有“有价值数值”
            switch (param)
            {
                case AttackHitConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0  && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min 
                        && conditionParam.damageRange.min > 0 && conditionParam.damageRange.max > 0 && conditionParam.damageRange.max > conditionParam.damageRange.min;
                case SkillCastConditionParam conditionParam:
                    return conditionParam.mpRange.min > 0 && conditionParam.mpRange.max > 0 && conditionParam.mpRange.max > conditionParam.mpRange.min;
                case TakeDamageConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0 && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min &&
                           conditionParam.damageRange.min > 0 && conditionParam.damageRange.max > 0 && conditionParam.damageRange.max > conditionParam.damageRange.min;
                case KillConditionParam conditionParam:
                    return conditionParam.targetCount >= 0 && conditionParam.timeWindow >= 0;
                case HpChangeConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0 && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min;
                case MpChangeConditionParam conditionParam:
                    return conditionParam.mpRange.min > 0 && conditionParam.mpRange.max > 0 && conditionParam.mpRange.max > conditionParam.mpRange.min;
                case CriticalHitConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0 && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min &&
                           conditionParam.damageRange.min > 0 && conditionParam.damageRange.max > 0 && conditionParam.damageRange.max > conditionParam.damageRange.min;
                case DodgeConditionParam conditionParam:
                    return conditionParam.dodgeCount >= 0 && conditionParam.dodgeRate >= 0;
                case AttackConditionParam conditionParam:
                    return true;
                default:
                    return false;
            }
        }

    }


    [Serializable]
    [JsonSerializable]
    public struct BattleEffectConditionConfigData
    {
        [Header("Id")] public int id;
        [Header("触发类型")] public TriggerType triggerType;
        [Header("触发概率")] public float probability;
        [Header("触发间隔")] public float interval;
        [Header("目标类型")] public ConditionTargetType targetType;
        [Header("目标数量")] public int targetCount;
        [SerializeReference]
        [Header("条件参数")] public IConditionParam conditionParam;
    }

    [Serializable]
    [JsonSerializable]
    public struct AttackHitConditionParam : IConditionParam
    {
        [Header("造成伤害占生命值百分比范围")]
        public Range hpRange;
        [Header("造成伤害的范围")]
        public Range damageRange;
        [Header("攻击距离类型")]
        public AttackRangeType attackRangeType;

        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min &&
                   hpRange.max <= 1f
                   && damageRange.min >= 0 && damageRange.max >= damageRange.min && damageRange.max <= 1f;
        }

        public string GetConditionDesc()
        {
            var attackRangeStr = EnumHeaderParser.GetHeader(attackRangeType);
            return $"造成伤害百分比:[{hpRange.min},{hpRange.max}]%,造成伤害:[{damageRange.min},{damageRange.max}]%,攻击范围:{attackRangeStr}";
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct SkillCastConditionParam : IConditionParam
    {
        [Header("消耗MP百分比范围")]
        public Range mpRange;
        [Header("技能类型")]
        public SkillType skillType;

        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && Enum.IsDefined(typeof(SkillType), skillType) &&
                   mpRange.min >= 0 && mpRange.max >= mpRange.min && mpRange.max <= 1f;
        }

        public string GetConditionDesc()
        {
            var skillStr = EnumHeaderParser.GetHeader(skillType);
            return $"技能类型:{skillStr},消耗MP百分比:[{mpRange.min},{mpRange.max}]%";
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct TakeDamageConditionParam : IConditionParam
    {
        [Header("造成伤害占生命值百分比范围")]
        public Range hpRange;
        [Header("伤害类型(物理或元素伤害)")]
        public DamageType damageType;
        [Header("伤害来源")]
        public DamageCastType damageCastType;
        [Header("伤害范围")]
        public Range damageRange;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && Enum.IsDefined(typeof(DamageCastType), damageCastType) &&
                   damageRange.min >= 0 && damageRange.max >= damageRange.min && damageRange.max <= 1f &&
                   hpRange.min >= 0 && hpRange.max >= hpRange.min && hpRange.max <= 1f &&
                   Enum.IsDefined(typeof(DamageType), damageType);
        }

        public string GetConditionDesc()
        {
            var damageStr = EnumHeaderParser.GetHeader(damageType);
            var damageCastStr = EnumHeaderParser.GetHeader(damageCastType);
            return $"造成伤害类型:{damageStr},伤害百分比:[{damageRange.min},{damageRange.max}]%,造成伤害:[{hpRange.min},{hpRange.max}]%,伤害来源:{damageCastStr}";
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct KillConditionParam : IConditionParam
    {
        [Header("击杀目标数量")]
        public int targetCount;
        [Header("时间窗口")]
        public float timeWindow;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && targetCount >= 0 && timeWindow >= 0;
        }

        public string GetConditionDesc()
        {
            return $"击杀目标数量:{targetCount},时间窗口:{timeWindow}秒";
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct HpChangeConditionParam : IConditionParam
    {
        [Header("生命值占最大生命值百分比范围")]
        public Range hpRange;

        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min &&
                   hpRange.max <= 1f;
        }
        
        public string GetConditionDesc()
        {
            return $"生命值百分比:[{hpRange.min},{hpRange.max}]%";
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct MpChangeConditionParam : IConditionParam
    {
        [Header("魔法值占最大魔法值百分比范围")]
        public Range mpRange;

        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && mpRange.min >= 0 && mpRange.max >= mpRange.min &&
                   mpRange.max <= 1f;
        }
        public string GetConditionDesc()
        {
            return $"魔法值百分比:[{mpRange.min},{mpRange.max}]%";
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct CriticalHitConditionParam : IConditionParam
    {
        [Header("造成伤害占生命值百分比范围")]
        public Range hpRange;
        [Header("伤害值范围")]
        public Range damageRange;
        [Header("伤害类型(物理或元素伤害)")]
        public DamageType damageType;
        [Header("伤害来源")]
        public DamageCastType damageCastType;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min &&
                   hpRange.max <= 1f &&
                   damageRange.min >= 0 && damageRange.max <= 1f && damageRange.max >= damageRange.min &&
                   Enum.IsDefined(typeof(DamageType), damageType);
        }
        public string GetConditionDesc()
        {
            var damageStr = EnumHeaderParser.GetHeader(damageType);
            var damageCastStr = EnumHeaderParser.GetHeader(damageCastType);
            return $"造成伤害类型:{damageStr},伤害百分比:[{damageRange.min},{damageRange.max}]%,造成伤害:[{hpRange.min},{hpRange.max}]%,伤害来源:{damageCastStr}";
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct DodgeConditionParam : IConditionParam
    {
        [Header("闪避次数")]
        public int dodgeCount;
        [Header("闪避频率")] 
        public float dodgeRate;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && dodgeCount >= 0 && dodgeRate >= 0;
        }
        public string GetConditionDesc()
        {
            return $"闪避次数:{dodgeCount},闪避率:{dodgeRate}%";
        }
    }

    [Serializable]
    public struct AttackConditionParam : IConditionParam
    {
        [Header("攻击范围类型")]
        public AttackRangeType attackRangeType;
        [Header("攻击力")]
        public float attack;
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && attack >= 0 &&
                   Enum.IsDefined(typeof(AttackRangeType), attackRangeType);
        }
        public string GetConditionDesc()
        {
            var attackRangeStr = EnumHeaderParser.GetHeader(attackRangeType);
            return $"攻击力:{attack},攻击范围:{attackRangeStr}";
        }
    }

    [Serializable]
    public struct SkillHitConditionParam : IConditionParam
    {
        public ConditionHeader ConditionHeader;
        [Header("伤害值范围")]
        public Range damageRange;
        [Header("技能类型")]
        public SkillType skillType;
        [Header("消耗MP百分比范围")]
        public Range mpRange;
        [Header("造成伤害占生命值最大值百分比范围")]
        public Range hpRange;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader();
        }
        public string GetConditionDesc()
        {
            var skillStr = EnumHeaderParser.GetHeader(skillType);
            return $"技能类型:{skillStr},消耗MP百分比:[{mpRange.min},{mpRange.max}]%,造成伤害百分比:[{hpRange.min},{hpRange.max}]%,造成伤害:[{damageRange.min},{damageRange.max}]%";
        }
    }
    
    [Serializable]
    public struct DeathConditionParam : IConditionParam 
    {
        public ConditionHeader ConditionHeader;
        public ConditionHeader GetConditionHeader() => ConditionHeader;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader();
        }
        public string GetConditionDesc()
        {
            return "";
        }
    }


    public static class ConditionExtension
    {
        public static bool CheckConditionHeader(this IConditionParam conditionParam)
        {
            var header = conditionParam.GetConditionHeader();
            return Enum.IsDefined(typeof(TriggerType), header.triggerType);
        }

        public static T GetConditionParameter<T>(this TriggerType triggerType) where T : IConditionParam
        {
            switch (triggerType)
            {
                case TriggerType.OnAttackHit:
                    return (T)(object)new AttackHitConditionParam();
                case TriggerType.OnSkillCast:
                    return (T)(object)new SkillCastConditionParam();
                case TriggerType.OnTakeDamage:
                    return (T)(object)new TakeDamageConditionParam();
                case TriggerType.OnKill:
                    return (T)(object)new KillConditionParam();
                case TriggerType.OnHpChange:
                    return (T)(object)new HpChangeConditionParam();
                case TriggerType.OnManaChange:
                    return (T)(object)new MpChangeConditionParam();
                case TriggerType.OnCriticalHit:
                    return (T)(object)new CriticalHitConditionParam();
                case TriggerType.OnDodge:
                    return (T)(object)new DodgeConditionParam();
                case TriggerType.OnAttack:
                    return (T)(object)new AttackConditionParam();
                case TriggerType.OnSkillHit:
                    return (T)(object)new SkillHitConditionParam();
                case TriggerType.OnDeath:
                    return (T)(object)new DeathConditionParam();
                default:
                    return default;
            }
        }
    }
}