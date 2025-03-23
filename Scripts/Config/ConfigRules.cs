using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using AOTScripts.Data;
using CustomEditor.Scripts;
using HotUpdate.Scripts.Config.ArrayConfig;

namespace HotUpdate.Scripts.Config
{
    public static class EffectStringParser
    {
        // 缓存 TriggerParams 类型
        private static readonly Dictionary<TriggerType, Type> TriggerParamTypes;

        static EffectStringParser()
        {
            TriggerParamTypes = new Dictionary<TriggerType, Type>();
            var paramTypes = Assembly.GetExecutingAssembly()
                .GetTypes()
                .Where(t => typeof(IEffectData).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in paramTypes)
            {
                var instance = (IEffectData)Activator.CreateInstance(type);
                TriggerParamTypes[instance.GetTriggerType()] = type;
            }
        }
        public struct ParseResult
        {
            public bool IsValid;
            public object ParsedData;
            public string ErrorMessage;
            public string SuggestedFix;
        }

        public static List<ParseResult> ParseEffectString(string effectDesc)
        {
            var results = new List<ParseResult>();
            string[] effectParts = effectDesc.Split(';');

            foreach (var part in effectParts)
            {
                string trimmedPart = part.Trim();
                ParseResult result;

                if (trimmedPart.StartsWith("[BasicProperty]"))
                {
                    result = ParseBasicProperty(trimmedPart);
                }
                else if (trimmedPart.StartsWith("[PassiveEffect]"))
                {
                    result = ParsePassiveEffect(trimmedPart);
                }
                else
                {
                    result = new ParseResult
                    {
                        IsValid = false,
                        ErrorMessage = $"未知的效果类型标记: {trimmedPart}",
                        SuggestedFix = "请添加正确的标记，如 [BasicProperty] 或 [PassiveEffect]"
                    };
                }

                results.Add(result);
            }

            return results;
        }

        private static ParseResult ParseBasicProperty(string effectDesc)
        {
            string content = effectDesc.Replace("[BasicProperty]", "").Trim();
            var properties = content.Split(',');
            var dataList = new List<BasicPropertyData>();
            string error = "";
            string suggestion = "[BasicProperty]";

            var propertyHeaders = EnumHeaderParser.GetEnumHeaders(typeof(PropertyTypeEnum));

            foreach (var prop in properties)
            {
                var match = Regex.Match(prop.Trim(), @"^(.+?)\+([\d\.]+)(%?)$");
                if (!match.Success)
                {
                    error += $"无效格式: {prop}; ";
                    suggestion += $"{prop}(修复为正确格式),";
                    continue;
                }

                string propName = match.Groups[1].Value;
                string valueStr = match.Groups[2].Value;
                bool isPercent = match.Groups[3].Value == "%";

                if (!EnumHeaderParser.TryGetEnumFromHeader<PropertyTypeEnum>(propName, out var propType))
                {
                    error += $"未知属性: {propName}; ";
                    var closestProp =
                        EnumHeaderParser.GetClosestEnumFromHeader<PropertyTypeEnum>(propName, out string closestHeader);
                    suggestion += $"{closestHeader}+{valueStr}{match.Groups[3].Value},";
                    continue;
                }

                if (!float.TryParse(valueStr, out float value) || value < 0)
                {
                    error += $"无效数值: {valueStr}; ";
                    suggestion += $"{propName}+0{match.Groups[3].Value},";
                    continue;
                }

                var propHeader = EnumHeaderParser.GetHeader(propType);
                dataList.Add(new BasicPropertyData { property = propHeader, value = isPercent ? value / 100f : value });
                suggestion += $"{propName}+{valueStr}{match.Groups[3].Value},";
            }

            suggestion = suggestion.TrimEnd(',');

            return new ParseResult
            {
                IsValid = string.IsNullOrEmpty(error),
                ParsedData = dataList.ToArray(),
                ErrorMessage = error.TrimEnd(';'),
                SuggestedFix = suggestion
            };
        }
        
        public static string GenerateEffectString(BattleEffectConditionConfigData data)
        {
            var triggerName = EnumHeaderParser.GetEnumHeaders(typeof(TriggerType))[data.triggerType];
            var targetName = EnumHeaderParser.GetEnumHeaders(typeof(ConditionTargetType))[data.targetType];
            var paramString = data.conditionParam?.GetConditionDesc() ?? "";

            string effectString = $"[PassiveEffect]{triggerName},概率{data.probability}%,目标{data.targetCount}个{targetName},冷却{data.interval}秒";
            if (!string.IsNullOrEmpty(paramString))
            {
                effectString += $",{paramString}";
            }

            return effectString;
        }

        private static ParseResult ParsePassiveEffect(string effectDesc)
        {
            string content = effectDesc.Replace("[PassiveEffect]", "").Trim();
            var parts = content.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                return new ParseResult
                {
                    IsValid = false,
                    ErrorMessage = $"被动效果格式错误: {content}",
                    SuggestedFix = "[PassiveEffect]战斗开始时,概率30%,目标1个敌人,冷却60秒"
                };
            }

            string eventName = parts[0].Trim();
            string probStr = parts[1].Replace("概率", "").Replace("%", "").Trim();
            string targetStr = parts[2].Trim();
            string intervalStr = parts[3].Replace("冷却", "").Replace("秒", "").Trim();
            string extraParams = parts.Length > 4 ? string.Join(",", parts.Skip(4)) : "";

            string error = "";
            string suggestion = "[PassiveEffect]";

            var eventHeaders = EnumHeaderParser.GetEnumHeaders(typeof(TriggerType));
            var targetHeaders = EnumHeaderParser.GetEnumHeaders(typeof(ConditionTargetType));

            // 解析触发类型
            if (!EnumHeaderParser.TryGetEnumFromHeader<TriggerType>(eventName, out var triggerType))
            {
                error += $"未知事件: {eventName}; ";
                var closestEvent = EnumHeaderParser.GetClosestEnumFromHeader<TriggerType>(eventName, out string closestEventHeader);
                suggestion += $"{closestEventHeader},";
                triggerType = closestEvent;
            }
            else
            {
                suggestion += $"{eventName},";
            }

            // 解析概率
            if (!float.TryParse(probStr, out float probability) || probability < 0 || probability > 100)
            {
                error += $"无效概率: {probStr}; ";
                suggestion += "概率30%,";
                probability = 30f;
            }
            else
            {
                suggestion += $"概率{probStr}%,";
            }

            // 解析目标
            var targetMatch = Regex.Match(targetStr, @"目标(\d+)个(.+)");
            int targetCount = 1;
            string targetType = "敌人";
            if (targetMatch.Success)
            {
                targetCount = int.TryParse(targetMatch.Groups[1].Value, out int count) && count > 0 ? count : 1;
                targetType = targetMatch.Groups[2].Value;
            }
            else
            {
                error += $"无效目标格式: {targetStr}; ";
            }
            if (!EnumHeaderParser.TryGetEnumFromHeader<ConditionTargetType>(targetType, out var _))
            {
                error += $"未知目标类型: {targetType}; ";
                var closestType = EnumHeaderParser.GetClosestEnumFromHeader<ConditionTargetType>(targetType, out string closestTargetHeader);
                targetType = closestTargetHeader;
            }
            suggestion += $"目标{targetCount}个{targetType},";

            // 解析冷却时间
            if (!float.TryParse(intervalStr, out float interval) || interval < 0)
            {
                error += $"无效冷却时间: {intervalStr}; ";
                suggestion += "冷却60秒";
                interval = 60f;
            }
            else
            {
                suggestion += $"冷却{intervalStr}秒";
            }

            // 解析额外参数
            IEffectData triggerParams = ParseTriggerParams(triggerType, extraParams, ref error, ref suggestion);

            var data = new PassiveEffectData
            {
                eventName = eventName,
                probability = probability,
                targetCount = targetCount,
                targetType = targetType,
                interval = interval,
                EffectData = triggerParams
            };

            return new ParseResult
            {
                IsValid = string.IsNullOrEmpty(error),
                ParsedData = data,
                ErrorMessage = error?.TrimEnd(';'),
                SuggestedFix = suggestion
            };
        }

        private static IEffectData ParseTriggerParams(TriggerType triggerType, string extraParams, ref string error, ref string suggestion)
        {
            if (!TriggerParamTypes.TryGetValue(triggerType, out var paramType) || string.IsNullOrEmpty(extraParams))
            {
                if (!string.IsNullOrEmpty(extraParams))
                    error += $"触发类型 {EnumHeaderParser.GetEnumHeaders(typeof(TriggerType))[triggerType]} 不支持额外参数: {extraParams}; ";
                if (paramType != null) 
                    return Activator.CreateInstance(paramType) as IEffectData; // 返回默认实例
            }

            var paramDict = ParseKeyValuePairs(extraParams);
            if (paramType == typeof(AttackHitConditionParam))
            {
                
            }

            // if (paramType == typeof(BattleStartTriggerParams))
            // {
            //     return new BattleStartTriggerParams();
            // }
            // else if (paramType == typeof(OnHitTriggerParams))
            // {
            //     float damageReduction = 0f;
            //     if (paramDict.TryGetValue("伤害减免", out var valueStr) && float.TryParse(valueStr, out damageReduction))
            //         suggestion += $",伤害减免+{damageReduction}";
            //     else
            //     {
            //         error += $"无效伤害减免: {paramDict.GetValueOrDefault("伤害减免")}; ";
            //         suggestion += ",伤害减免+0";
            //     }
            //     return new OnHitTriggerParams(damageReduction);
            // }
            // else if (paramType == typeof(AttackHitTriggerParams))
            // {
            //     float attackPower = 0f, attackRadius = 0f;
            //     if (paramDict.TryGetValue("攻击力", out var powerStr) && float.TryParse(powerStr, out attackPower))
            //         suggestion += $",攻击力+{attackPower}";
            //     else
            //     {
            //         error += $"无效攻击力: {paramDict.GetValueOrDefault("攻击力")}; ";
            //         suggestion += ",攻击力+0";
            //     }
            //     if (paramDict.TryGetValue("攻击范围", out var radiusStr) && float.TryParse(radiusStr, out attackRadius))
            //         suggestion += $",攻击范围+{attackRadius}";
            //     else
            //     {
            //         error += $"无效攻击范围: {paramDict.GetValueOrDefault("攻击范围")}; ";
            //         suggestion += ",攻击范围+0";
            //     }
            //     return new AttackHitTriggerParams(attackPower, attackRadius);
            // }
            // else if (paramType == typeof(HealthBelow50TriggerParams))
            // {
            //     float healthRecoverySpeed = 0f;
            //     if (paramDict.TryGetValue("生命回复速度", out var speedStr) && float.TryParse(speedStr, out healthRecoverySpeed))
            //         suggestion += $",生命回复速度+{healthRecoverySpeed}";
            //     else
            //     {
            //         error += $"无效生命回复速度: {paramDict.GetValueOrDefault("生命回复速度")}; ";
            //         suggestion += ",生命回复速度+0";
            //     }
            //     return new HealthBelow50TriggerParams(healthRecoverySpeed);
            // }

            return null; // 未识别的类型
        }

        private static Dictionary<string, string> ParseKeyValuePairs(string extraParams)
        {
            var dict = new Dictionary<string, string>();
            var pairs = extraParams.Split(',');

            foreach (var pair in pairs)
            {
                var match = Regex.Match(pair.Trim(), @"^(.+?)\+([\d\.]+)$");
                if (match.Success)
                {
                    dict[match.Groups[1].Value] = match.Groups[2].Value;
                }
            }

            return dict;
        }
    }

    // 基础属性效果的数据结构
    [Serializable]
    public struct BasicPropertyData
    {
        public string property;
        public float value;
    }

    // 被动效果的数据结构
    [Serializable]
    public struct PassiveEffectData
    {
        public string eventName;
        public float probability; // 0-100
        public int targetCount;
        public string targetType;
        public float interval; // 秒
        public IEffectData EffectData;// => new PassiveEffectData { eventName = eventName, probability = probability, targetCount = targetCount, targetType = targetType, interval = interval };
    }

    public interface IEffectData
    {
        public TriggerType GetTriggerType();

        public static TriggerType GetTriggerType(string str)
        {
            if (!EnumHeaderParser.TryGetEnumFromHeader<TriggerType>(str, out var propType))
            {
                return propType;
            }
            return TriggerType.None;
        }

        public static string GetTriggerTypeStr(TriggerType triggerType)
        {
            return EnumHeaderParser.GetHeader(triggerType);
        }
    }

    [Serializable]
    public struct AttackHitConditionParam : IEffectData
    {
        public string damageRange;
        public string hpRange;
        public string attackRangeType;
        public TriggerType GetTriggerType() => TriggerType.OnAttackHit;
    }

    [Serializable]
    public struct AttackConditionParam : IEffectData
    {
        public string attack;
        public string attackRangeType;
        public TriggerType GetTriggerType() => TriggerType.OnAttack;
    }

    [Serializable]
    public struct SkillCastConditionParam : IEffectData
    {
        public string skillType;
        public TriggerType GetTriggerType() => TriggerType.OnSkillCast;
    }
    
    [Serializable]
    public struct SkillHitConditionParam : IEffectData
    {
        public string skillType;
        public string damageRange;
        public string hpRange;
        public string mpRange;
        public TriggerType GetTriggerType() => TriggerType.OnSkillHit;
    }

    [Serializable]
    public struct TakeDamageConditionParam : IEffectData
    {
        public string hpRange;
        public string damageType;
        public string damageCastType;
        public string damageRange;
        public TriggerType GetTriggerType() => TriggerType.OnTakeDamage;
    }
    
    [Serializable]
    public struct KillConditionParam : IEffectData
    {
        public int targetCount;
        public float timeWindow;
        public TriggerType GetTriggerType() => TriggerType.OnKill;
    }
    
    [Serializable]
    public struct HpChangeConditionParam : IEffectData
    {
        public string hpRange;
        public TriggerType GetTriggerType() => TriggerType.OnHpChange;
    }
    
    [Serializable]
    public struct MpChangeConditionParam : IEffectData
    {
        public string mpRange;
        public TriggerType GetTriggerType() => TriggerType.OnManaChange;
    }

    [Serializable]
    public struct CriticalHitConditionParam : IEffectData
    {
        public string hpRange;
        public string damageRange;
        public string damageType;
        public string damageCastType;
        public TriggerType GetTriggerType() => TriggerType.OnCriticalHit;
    }
    
    [Serializable]
    public struct DeathConditionParam : IEffectData
    {
        public TriggerType GetTriggerType() => TriggerType.OnDeath;
    }

    [Serializable]
    public struct DodgeConditionParam : IEffectData
    {
        public TriggerType GetTriggerType() => TriggerType.OnDodge;
    }
}