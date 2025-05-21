using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using HotUpdate.Scripts.Common;
using Newtonsoft.Json;
using OfficeOpenXml;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using UnityEngine.Serialization;
using Random = System.Random;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "BattleEffectConditionConfig",
        menuName = "ScriptableObjects/BattleEffectConditionConfig")]
    public class BattleEffectConditionConfig : ConfigBase
    {
        //[ReadOnly] 
        [SerializeField]
        private List<BattleEffectConditionConfigData> conditionList = new List<BattleEffectConditionConfigData>();
#if UNITY_EDITOR
        [ReadOnly]
        public BattleEffectConditionConfigData effectData;
        public static Random random = new Random();
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
                conditionData.buffWeight = float.Parse(text[7]);
                conditionData.buffIncreaseType = (BuffIncreaseType)Enum.Parse(typeof(BuffIncreaseType), text[8]);
                conditionList.Add(conditionData);
            }
        }
        
        public bool HasCondition(int id)
        {
            return conditionList.Exists(data => data.id == id);
        }
        
        public BattleEffectConditionConfigData GetConditionData(int id)
        {
            return conditionList.Find(data => data.id == id);
        }
        
        public int GetConditionMaxId()
        {
            return conditionList.Count > 0 ? conditionList.Max(data => data.id) : 0;
        }
        
        #if UNITY_EDITOR
        public void AddConditionData(BattleEffectConditionConfigData data)
        {
            if (conditionList.Exists(b => b.id == data.id))
            {
                Debug.LogWarning($"condition id already exists: {data.id}");
                return;
            }
            conditionList.Add(data);
            EditorUtility.SetDirty(this);
        }

        [Button("将scriptable对象写入excel")]
        public void WriteToExcel()
        {
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            var jsonSerializerSettings = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            jsonSerializerSettings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            var excel = Path.Combine(excelAssetReference.Path, $"{configName}.xlsx");
            using (var package = new ExcelPackage(new FileInfo(excel)))
            {
                var worksheet = package.Workbook.Worksheets[0]; // 假设数据在第一个工作表
                int rowCount = worksheet.Dimension.Rows;
                
                const int idCol = 1; // buffId 列
                const int triggerTypeCol = 2;
                const int probabilityCol = 3;
                const int intervalCol = 4;
                const int targetTypeCol = 5;
                const int targetCountCol = 6;
                const int buffWeightCol = 7;
                const int buffIncreaseTypeCol = 8;
                const int conditionParamCol = 9;
                int row = 0;
                var existingIds = new HashSet<int>();
                for (row = 3; row <= rowCount; row++)
                {
                    //var value = worksheet.Cells[row, idCol].GetValue<double>();
                    int buffId = (int)worksheet.Cells[row, idCol].GetValue<double>();
                    existingIds.Add(buffId);
                }

                try
                {
                    // 从第 2 行开始（跳过表头）
                    var newRow = 3;
                    foreach (var configData in conditionList)
                    {
                        worksheet.Cells[newRow, idCol].Value = configData.id;
                        worksheet.Cells[newRow, triggerTypeCol].Value = configData.triggerType.ToString();
                        worksheet.Cells[newRow, probabilityCol].Value = configData.probability;
                        worksheet.Cells[newRow, intervalCol].Value = configData.interval;
                        worksheet.Cells[newRow, targetTypeCol].Value = configData.targetType.ToString();
                        worksheet.Cells[newRow, targetCountCol].Value = configData.targetCount;
                        worksheet.Cells[newRow, buffWeightCol].Value = configData.buffWeight;
                        worksheet.Cells[newRow, buffIncreaseTypeCol].Value = configData.buffIncreaseType.ToString();

                        var json = JsonConvert.SerializeObject(configData.conditionParam, jsonSerializerSettings);
                        worksheet.Cells[newRow, conditionParamCol].Value = json;

                        newRow++;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Error in {row} ");
                    throw;
                }

                // 保存文件
                package.Save();
                Debug.Log("Equipment table updated successfully!");
            }
        }

#endif
        public bool AnalysisDataString(string dataString, out BattleEffectConditionConfigData data)
        {
            data = new BattleEffectConditionConfigData();
            if (string.IsNullOrEmpty(dataString) || dataString.Equals("0"))
            {
                return false;
            }
            var cleanString = dataString.Replace("[PassiveEffect]", "").Trim();
            var parts = cleanString.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);

            // 解析基础字段（固定顺序的前5个参数）
            data.triggerType = Enum.Parse<TriggerType>(parts[0].Trim());// EnumHeaderParser.GetEnumValue<TriggerType>();
            data.probability = float.Parse(parts[1].Replace("概率", "").Replace("%", ""));
            
            var targetPart = parts[2].Split(new[] { "个" }, StringSplitOptions.RemoveEmptyEntries);
            data.targetCount = int.Parse(targetPart[0].Replace("Buff目标对象", ""));
            data.targetType = Enum.Parse<ConditionTargetType>(targetPart[1]);
            
            data.interval = float.Parse(parts[3].Replace("冷却", "").Replace("秒", ""));

            // 解析条件参数（中间部分）
            var paramParts = new List<string>();
            for (int i = 5; i < parts.Length - 2; i++) // 排除最后两个Buff参数
            {
                paramParts.Add(parts[i].Trim());
            }
            
            // 解析最后的Buff参数
            data.buffWeight = float.Parse(parts[^2].Replace("Buff权重", ""));
            data.buffIncreaseType = Enum.Parse<BuffIncreaseType>(
                parts[^1].Replace("Buff增益类型", ""));

            // 处理条件参数
            if (paramParts.Count > 0)
            {
                var paramString = string.Join("、", paramParts);
                data.conditionParam = data.triggerType switch
                {
                    TriggerType.OnAttackHit => new AttackHitConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnSkillCast => new SkillCastConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnTakeDamage => new TakeDamageConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnKill => new KillConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnHpChange => new HpChangeConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnManaChange => new MpChangeConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnCriticalHit => new CriticalHitConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnDodge => new DodgeConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnAttack => new AttackConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnSkillHit => new SkillHitConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    TriggerType.OnDeath => new DeathConditionParam().AnalysisConditionParam<IConditionParam>(paramString),
                    _ => throw new ArgumentException("Unsupported trigger type")
                };
            }

            return true;
        }

        public IConditionParam GetConditionParam(int id)
        {
            return GetConditionData(id).conditionParam;
        }

        #if UNITY_EDITOR
        public static BattleEffectConditionConfigData GenerateConfig(QualityType rarity)
        {
            var config = new BattleEffectConditionConfigData();

            // 随机选择 TriggerType（跳过 None）
            TriggerType[] triggerTypes = Enum.GetValues(typeof(TriggerType)).Cast<TriggerType>().Skip(1).ToArray();
            config.triggerType = triggerTypes[random.Next(triggerTypes.Length)];

            // 获取基础触发频率
            float fType = GetTriggerTypeFrequency(config.triggerType);

            // 根据频率调整 probability 和 interval
            if (fType >= 0.8f) // 高频率 (如 OnAttack, OnHpChange)
            {
                config.probability = random.Next(50, 91); // 0.5 ~ 0.9
                config.interval = random.Next(0, 3); // 0 ~ 2 秒
            }
            else if (fType >= 0.3f) // 中等频率 (如 OnSkillCast, OnDodge)
            {
                config.probability = random.Next(60, 100); // 0.6 ~ 1.0
                config.interval = random.Next(1, 4); // 1 ~ 3 秒
            }
            else // 低频率 (如 OnKill, OnDeath)
            {
                config.probability = random.Next(70, 100); // 0.7 ~ 1.0
                config.interval = random.Next(2, 6); // 2 ~ 5 秒
            }

            // 随机生成 conditionParam
            config.conditionParam = GenerateConditionParam(config.triggerType, random);

            // 随机生成 targetType 和 targetCount
            config.targetType = GenerateTargetType(random);
            config.targetCount = config.targetType.HasAnyState(ConditionTargetType.All) || config.targetType == ConditionTargetType.None
                ? random.Next(1, 6) // 群体目标：1 ~ 5
                : 1; // 单体目标固定为 1

            return config;
        }
        
        public static float CalculateETrigger(BattleEffectConditionConfigData config, float baseFrequency = 1.0f)
        {
            float fType = GetTriggerTypeFrequency(config.triggerType);
            float fTrigger = (config.interval == 0) ? 1.0f : Math.Min(1.0f, baseFrequency / (fType * config.interval));
            float pTrigger = config.probability;
            float cParam = (config.conditionParam == null || !HasValuableParam(config.conditionParam)) ? 1.0f : 1.25f;
            return fTrigger * pTrigger * cParam;
        }
        
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
        #endif

        public static float GetTriggerTypeFrequency(TriggerType type)
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
                case TriggerType.OnMove: return 1.0f;
                default: return 1.0f;
            }
        }

        private static bool HasValuableParam(IConditionParam param)
        {
            // 实现逻辑：检查 param 是否有“有价值数值”
            switch (param)
            {
                case AttackHitConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0 && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min 
                        && conditionParam.damageRange.min > 0 && conditionParam.damageRange.max > 0 && conditionParam.damageRange.max > conditionParam.damageRange.min;
                case SkillCastConditionParam conditionParam:
                    return conditionParam.mpRange.min > 0 && conditionParam.mpRange.max > 0 && conditionParam.mpRange.max > conditionParam.mpRange.min;
                case TakeDamageConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0 && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min &&
                           conditionParam.damageRange.min > 0 && conditionParam.damageRange.max > 0 && conditionParam.damageRange.max > conditionParam.damageRange.min;
                case KillConditionParam conditionParam:
                    return conditionParam.targetCount > 0 && conditionParam.timeWindow > 0;
                case HpChangeConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0 && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min;
                case MpChangeConditionParam conditionParam:
                    return conditionParam.mpRange.min > 0 && conditionParam.mpRange.max > 0 && conditionParam.mpRange.max > conditionParam.mpRange.min;
                case CriticalHitConditionParam conditionParam:
                    return conditionParam.hpRange.min > 0 && conditionParam.hpRange.max > 0 && conditionParam.hpRange.max > conditionParam.hpRange.min &&
                           conditionParam.damageRange.min > 0 && conditionParam.damageRange.max > 0 && conditionParam.damageRange.max > conditionParam.damageRange.min;
                case DodgeConditionParam conditionParam:
                    return conditionParam.dodgeCount > 0 && conditionParam.dodgeRate > 0;
                case AttackConditionParam conditionParam:
                    return true;
                default:
                    return false;
            }
        }
        // 随机生成 ConditionTargetType
        private static ConditionTargetType GenerateTargetType(Random r)
        {
            ConditionTargetType[] basicTypes = { ConditionTargetType.Self, ConditionTargetType.Enemy, ConditionTargetType.Ally, ConditionTargetType.Player, ConditionTargetType.Boss };
            int flagCount = r.Next(1, 3); // 随机组合 1~2 个目标类型
            ConditionTargetType result = basicTypes[r.Next(basicTypes.Length)];
            for (int i = 1; i < flagCount; i++)
            {
                ConditionTargetType nextType = basicTypes[r.Next(basicTypes.Length)];
                if (!result.HasFlag(nextType)) result |= nextType;
            }
            return result;
        }

        // 随机生成 conditionParam
        private static IConditionParam GenerateConditionParam(TriggerType triggerType, Random r)
        {
            switch (triggerType)
            {
                case TriggerType.OnAttackHit:
                    return new AttackHitConditionParam
                    {
                        hpRange = new Range { min = r.Next(0, 50), max = r.Next(50, 101) },
                        damageRange = new Range { min = r.Next(0, 50), max = r.Next(50, 101) },
                        attackRangeType = (AttackRangeType)r.Next(Enum.GetValues(typeof(AttackRangeType)).Length)
                    };
                case TriggerType.OnSkillCast:
                    return new SkillCastConditionParam
                    {
                        mpRange = new Range { min = r.Next(0, 50), max = r.Next(50, 101)  },
                        skillBaseType = (SkillBaseType)r.Next(Enum.GetValues(typeof(SkillBaseType)).Length)
                    };
                case TriggerType.OnTakeDamage:
                    return new TakeDamageConditionParam
                    {
                        hpRange = new Range { min = r.Next(0, 50) , max = r.Next(50, 101) },
                        damageRange = new Range { min = r.Next(0, 50) , max = r.Next(50, 101)  },
                        damageType = (DamageType)r.Next(Enum.GetValues(typeof(DamageType)).Length),
                        damageCastType = (DamageCastType)r.Next(Enum.GetValues(typeof(DamageCastType)).Length)
                    };
                case TriggerType.OnKill:
                    return new KillConditionParam
                    {
                        targetCount = r.Next(1, 4), // 1 ~ 3 个目标
                        timeWindow = r.Next(5, 16) // 5 ~ 15 秒
                    };
                case TriggerType.OnHpChange:
                    return new HpChangeConditionParam
                    {
                        hpRange = new Range { min = r.Next(0, 50) , max = r.Next(50, 101)  }
                    };
                case TriggerType.OnManaChange:
                    return new MpChangeConditionParam
                    {
                        mpRange = new Range { min = r.Next(0, 50), max = r.Next(50, 101)  }
                    };
                case TriggerType.OnCriticalHit:
                    return new CriticalHitConditionParam
                    {
                        hpRange = new Range { min = r.Next(0, 50) / 100f, max = r.Next(50, 101) / 100f },
                        damageRange = new Range { min = r.Next(0, 50) / 100f, max = r.Next(50, 101) / 100f },
                        damageType = (DamageType)r.Next(Enum.GetValues(typeof(DamageType)).Length),
                        damageCastType = (DamageCastType)r.Next(Enum.GetValues(typeof(DamageCastType)).Length)
                    };
                case TriggerType.OnDodge:
                    return new DodgeConditionParam
                    {
                        dodgeCount = r.Next(1, 4), // 1 ~ 3 次
                        dodgeRate = r.Next(10, 51) // 10% ~ 50%
                    };
                case TriggerType.OnAttack:
                    return new AttackConditionParam
                    {
                        attackRangeType = (AttackRangeType)r.Next(Enum.GetValues(typeof(AttackRangeType)).Length),
                        attack = r.Next(10, 101) // 10 ~ 100
                    };
                case TriggerType.OnSkillHit:
                    return new SkillHitConditionParam
                    {
                        damageRange = new Range { min = r.Next(0, 50) , max = r.Next(50, 101)  },
                        skillBaseType = (SkillBaseType)r.Next(Enum.GetValues(typeof(SkillBaseType)).Length),
                        mpRange = new Range { min = r.Next(0, 50) , max = r.Next(50, 101)  },
                        hpRange = new Range { min = r.Next(0, 50) , max = r.Next(50, 101)  }
                    };
                case TriggerType.OnDeath:
                    return new DeathConditionParam();
                default:
                    return null; // 无条件
            }
        }
        
        public string ToLocalizedString(BattleEffectConditionConfigData data, EquipmentPropertyData effect)
        {
            var sb = new StringBuilder();
            if (data.triggerType == TriggerType.None)
            {
                // Buff效果描述
                sb.Append(GetBuffEffectDesc(effect));
                return sb.ToString();
            }
            
            // 基础条件描述
            sb.Append($"在{{{GetTriggerTypeDesc(data.triggerType)}}}时，");
    
            // 条件参数描述（过滤无效条件）
            var conditionDesc = GetConditionDesc(data.conditionParam);
            if (!string.IsNullOrEmpty(conditionDesc)) sb.Append(conditionDesc);
    
            // 概率和冷却
            sb.Append($"有{{{data.probability}%}}的概率为");
    
            // 目标描述
            sb.Append(GetTargetDesc(data.targetCount, data.targetType));
    
            // Buff效果描述
            sb.Append(GetBuffEffectDesc(effect));
    
            // 冷却和持续时间
            sb.Append($"，冷却{{{data.interval}秒}}");

            return sb.ToString();
        }

        private string GetTriggerTypeDesc(TriggerType type)
        {
            return EnumHeaderParser.GetHeader(type) ?? type.ToString();
        }

        private string GetConditionDesc(IConditionParam param)
        {
            if (param == null) return "";
    
            return param switch
            {
                AttackHitConditionParam p => FormatCondition(
                    (p.hpRange.min > 0 || p.hpRange.max < 1, $"生命百分比在{p.hpRange}"),
                    (p.damageRange.min > 0 || p.damageRange.max < 1, $"伤害值在{p.damageRange}"),
                    (p.attackRangeType != default, $"攻击范围：{EnumHeaderParser.GetHeader(p.attackRangeType)}")),
            
                SkillCastConditionParam p => FormatCondition(
                    (p.mpRange.min > 0 || p.mpRange.max < 1, $"消耗MP在{p.mpRange}"),
                    (p.skillBaseType != default, $"使用{EnumHeaderParser.GetHeader(p.skillBaseType)}技能")),
            
                TakeDamageConditionParam p => FormatCondition(
                    (p.damageType != default, $"伤害类型：{EnumHeaderParser.GetHeader(p.damageType)}"),
                    (p.damageRange.min > 0 || p.damageRange.max < 1, $"伤害值在{p.damageRange}"),
                    (p.damageCastType != default, $"来自{EnumHeaderParser.GetHeader(p.damageCastType)}")),
            
                KillConditionParam p => FormatCondition(
                    (p.targetCount > 0, $"击杀{p.targetCount}个目标"),
                    (p.timeWindow > 0, $"{p.timeWindow}秒内")),
            
                CriticalHitConditionParam p => FormatCondition(
                    (p.damageRange.min > 0 || p.damageRange.max < 1, $"暴击伤害在{p.damageRange}"),
                    (p.hpRange.min > 0 || p.hpRange.max < 1, $"生命百分比在{p.hpRange}")),
            
                DodgeConditionParam p => FormatCondition(
                    (p.dodgeCount > 0, $"闪避{p.dodgeCount}次"),
                    (p.dodgeRate > 0, $"闪避率{p.dodgeRate}%")),
            
                _ => ""
            };
        }

        private string FormatCondition(params (bool valid, string desc)[] conditions)
        {
            var validConditions = conditions.Where(c => c.valid).Select(c => c.desc);
            var enumerable = validConditions as string[] ?? validConditions.ToArray();
            return enumerable.Any() ? $"若{string.Join("且", enumerable)}" : "";
        }

        private string GetTargetDesc(int count, ConditionTargetType targetType)
        {
            var targetDesc = EnumHeaderParser.GetHeader(targetType) ?? targetType.ToString();
            return $"{count}个{targetDesc}";
        }

        private string GetBuffEffectDesc(EquipmentPropertyData effect)
        {
            var propName = EnumHeaderParser.GetHeader(effect.propertyType);

            var operation = effect.increaseData.operationType switch
            {
                BuffOperationType.Add => "增加",
                BuffOperationType.Subtract => "减少", 
                BuffOperationType.Multiply => "提升",
                BuffOperationType.Divide => "降低",
                _ => "调整"
            };
            var increaseDesc = effect.increaseData.increaseType switch
            {
                BuffIncreaseType.Base => "基础",
                BuffIncreaseType.Multiplier => "",
                BuffIncreaseType.Extra => "额外",
                BuffIncreaseType.CorrectionFactor => "总",
                BuffIncreaseType.Current => "当前",
                _ => "数值"
            };

            return $"{operation}{{{GetDynamicValueDesc(effect)}的{increaseDesc}{propName}}}";
        }

        private string GetDynamicValueDesc(EquipmentPropertyData effect)
        {
            return effect.increaseData.increaseType switch
            {
                BuffIncreaseType.Multiplier => $"{effect.increaseData.increaseValue:P0}",
                BuffIncreaseType.CorrectionFactor => $"{effect.increaseData.increaseValue:P0}",
                _ => $"{effect.increaseData.increaseValue:F1}"
            };
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
        [Header("Buff施加对象的类型")]
        public ConditionTargetType targetType;
        [Header("Buff额外权重系数")]
        public float buffWeight;
        [Header("Buff增益类型")]
        public BuffIncreaseType buffIncreaseType;
        [Header("Buff施加目标数量")] 
        public int targetCount;
        [SerializeReference]
        [Header("条件参数")] public IConditionParam conditionParam;

        public bool Equals(BattleEffectConditionConfigData other)
        {
            return triggerType == other.triggerType && Mathf.Approximately(probability, other.probability) && Mathf.Approximately(interval, other.interval) &&
                   targetType == other.targetType && targetCount == other.targetCount;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct AttackHitConditionParam : IConditionParam
    {
        [Header("目标类型")]
        public ConditionTargetType targetType;
        [Header("造成伤害占生命值百分比范围")]
        public Range hpRange;
        [Header("造成伤害的范围")]
        public Range damageRange;
        [Header("攻击距离类型")]
        public AttackRangeType attackRangeType;
        public TriggerType GetTriggerType() => TriggerType.OnAttackHit;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min &&
                   hpRange.max <= 1f
                   && damageRange.min >= 0 && damageRange.max >= damageRange.min && damageRange.max <= 1f;
        }

        public string GetConditionDesc()
        {
            return $"目标类型:{targetType}、造成伤害百分比:[{hpRange.min}%,{hpRange.max}%]、造成伤害:[{damageRange.min},{damageRange.max}],攻击范围:{attackRangeType}";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            AttackHitConditionParam result; 
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var targetTypeStr = parts[0].Replace("目标类型:", "");
            result.targetType = (ConditionTargetType)Enum.Parse(typeof(ConditionTargetType), targetTypeStr);
            var damageProperty = parts[1].Replace("造成伤害百分比:", "");
            result.hpRange = Range.GetRange(damageProperty);
            var damageRangeStr = parts[2].Replace("造成伤害:", "");
            result.damageRange = Range.GetRange(damageRangeStr);
            var attackRangeStr = parts[3].Replace("攻击范围:", "");
            result.attackRangeType = (AttackRangeType)Enum.Parse(typeof(AttackRangeType), attackRangeStr);
            return (T)(IConditionParam)result;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct SkillCastConditionParam : IConditionParam
    {
        [Header("消耗MP百分比范围")]
        public Range mpRange;
        [FormerlySerializedAs("skillType")] [Header("技能类型")]
        public SkillBaseType skillBaseType;

        public TriggerType GetTriggerType() => TriggerType.OnSkillCast;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && Enum.IsDefined(typeof(SkillBaseType), skillBaseType) &&
                   mpRange.min >= 0 && mpRange.max >= mpRange.min && mpRange.max <= 1f;
        }

        public string GetConditionDesc()
        {
            return $"技能类型:{skillBaseType}、消耗MP百分比:[{mpRange.min}%,{mpRange.max}%]";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            SkillCastConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var mpProperty = parts[1].Replace("消耗MP百分比:", "");
            result.mpRange = Range.GetRange(mpProperty);
            var skillStr = parts[0].Replace("技能类型:", "");
            result.skillBaseType = (SkillBaseType)Enum.Parse(typeof(SkillBaseType), skillStr);
            return (T)(IConditionParam)result;
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
        public TriggerType GetTriggerType() => TriggerType.OnTakeDamage;

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
            return $"受到伤害类型:{damageStr}、受到伤害百分比:[{damageRange.min}%,{damageRange.max}%]、造成伤害:[{hpRange.min},{hpRange.max}]、伤害来源:{damageCastStr}";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            TakeDamageConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var damageProperty = parts[1].Replace("受到伤害百分比:", "");
            result.hpRange = Range.GetRange(damageProperty);
            var damageRangeStr = parts[2].Replace("受到伤害:", "");
            result.damageRange = Range.GetRange(damageRangeStr);
            var damageTypeStr = parts[0].Replace("受到伤害类型:", "");    
            result.damageType = (DamageType)Enum.Parse(typeof(DamageType), damageTypeStr);
            var damageCastStr = parts[3].Replace("伤害来源:", "");
            result.damageCastType = (DamageCastType)Enum.Parse(typeof(DamageCastType), damageCastStr);
            return (T)(IConditionParam)result;
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
        [Header("目标类型")]
        public ConditionTargetType targetType;
        public TriggerType GetTriggerType() => TriggerType.OnKill;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && targetCount >= 0 && timeWindow >= 0;
        }

        public string GetConditionDesc()
        {
            return $"目标类型:{targetType}、击杀目标数量:{targetCount}、时间窗口:{timeWindow}秒";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            KillConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var targetTypeStr = parts[0].Replace("目标类型:", "");
            result.targetType = (ConditionTargetType)Enum.Parse(typeof(ConditionTargetType), targetTypeStr);
            var targetCountStr = parts[1].Replace("击杀目标数量:", "");
            result.targetCount = int.Parse(targetCountStr);
            var timeWindowStr = parts[2].Replace("时间窗口:", "");
            result.timeWindow = float.Parse(timeWindowStr);
            return (T)(IConditionParam)result;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct HpChangeConditionParam : IConditionParam
    {
        [Header("生命值占最大生命值百分比范围")]
        public Range hpRange;

        public TriggerType GetTriggerType() => TriggerType.OnHpChange;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min &&
                   hpRange.max <= 1f;
        }
        
        public string GetConditionDesc()
        {
            return $"生命值百分比:[{hpRange.min}%,{hpRange.max}%]";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            HpChangeConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var hpProperty = parts[0].Replace("生命值百分比:", "");
            result.hpRange = Range.GetRange(hpProperty);
            return (T)(IConditionParam)result;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct MpChangeConditionParam : IConditionParam
    {
        [Header("魔法值占最大魔法值百分比范围")]
        public Range mpRange;

        public TriggerType GetTriggerType() => TriggerType.OnManaChange;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && mpRange.min >= 0 && mpRange.max >= mpRange.min &&
                   mpRange.max <= 1f;
        }
        public string GetConditionDesc()
        {
            return $"魔法值百分比:[{mpRange.min}%,{mpRange.max}%]";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            MpChangeConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);            
            var mpProperty = parts[0].Replace("魔法值百分比:", "");
            result.mpRange = Range.GetRange(mpProperty);
            return (T)(IConditionParam)result;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct CriticalHitConditionParam : IConditionParam
    {
        [Header("目标类型")]
        public ConditionTargetType targetType;
        [Header("造成伤害占生命值百分比范围")]
        public Range hpRange;
        [Header("伤害值范围")]
        public Range damageRange;
        [Header("伤害类型(物理或元素伤害)")]
        public DamageType damageType;
        [Header("伤害来源")]
        public DamageCastType damageCastType;
        public TriggerType GetTriggerType() => TriggerType.OnCriticalHit;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && hpRange.min >= 0 && hpRange.max >= hpRange.min &&
                   hpRange.max <= 1f &&
                   damageRange.min >= 0 && damageRange.max <= 1f && damageRange.max >= damageRange.min &&
                   Enum.IsDefined(typeof(DamageType), damageType);
        }
        public string GetConditionDesc()
        {
            return $"目标类型:{targetType}、造成伤害类型:{damageType}、造成伤害百分比:[{damageRange.min}%,{damageRange.max}%]、造成伤害:[{hpRange.min},{hpRange.max}]、伤害来源:{damageCastType}";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            CriticalHitConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var targetTypeStr = parts[0].Replace("目标类型:", "");
            result.targetType = (ConditionTargetType)Enum.Parse(typeof(ConditionTargetType), targetTypeStr);
            var damageProperty = parts[2].Replace("造成伤害百分比:", "");
            result.hpRange = Range.GetRange(damageProperty);
            var damageRangeStr = parts[3].Replace("造成伤害:", "");
            result.damageRange = Range.GetRange(damageRangeStr);
            var damageTypeStr = parts[1].Replace("造成伤害类型:", "");
            result.damageType = (DamageType)Enum.Parse(typeof(DamageType), damageTypeStr);
            var damageCastStr = parts[4].Replace("伤害来源:", "");
            result.damageCastType = (DamageCastType)Enum.Parse(typeof(DamageCastType), damageCastStr);
            return (T)(IConditionParam)result;
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
        public TriggerType GetTriggerType() => TriggerType.OnDodge;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && dodgeCount >= 0 && dodgeRate >= 0;
        }
        public string GetConditionDesc()
        {
            return $"闪避次数:{dodgeCount}、闪避率:{dodgeRate}";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            DodgeConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var dodgeCountStr = parts[0].Replace("闪避次数:", "");
            result.dodgeCount = int.Parse(dodgeCountStr);
            var dodgeRateStr = parts[1].Replace("闪避率:", "");
            result.dodgeRate = float.Parse(dodgeRateStr);
            return (T)(IConditionParam)result;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct AttackConditionParam : IConditionParam
    {
        [Header("攻击范围类型")]
        public AttackRangeType attackRangeType;
        [Header("攻击力")]
        public float attack;
        public TriggerType GetTriggerType() => TriggerType.OnAttack;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader() && attack >= 0 &&
                   Enum.IsDefined(typeof(AttackRangeType), attackRangeType);
        }
        public string GetConditionDesc()
        {
            return $"攻击力:{attack}、攻击范围:{attackRangeType}";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            AttackConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var attackStr = parts[0].Replace("攻击力:", "");
            result.attack = float.Parse(attackStr);
            var attackRangeStr = parts[1].Replace("攻击范围:", "");
            result.attackRangeType = (AttackRangeType)Enum.Parse(typeof(AttackRangeType), attackRangeStr);
            return (T)(IConditionParam)result;
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct SkillHitConditionParam : IConditionParam
    {
        [Header("目标类型")]
        public ConditionTargetType targetType;
        [Header("伤害值范围")]
        public Range damageRange;
        [FormerlySerializedAs("skillType")] [Header("技能类型")]
        public SkillBaseType skillBaseType;
        [Header("消耗MP百分比范围")]
        public Range mpRange;
        [Header("造成伤害占生命值最大值百分比范围")]
        public Range hpRange;
        public TriggerType GetTriggerType() => TriggerType.OnSkillHit;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader();
        }
        public string GetConditionDesc()
        {
            return $"目标类型:{targetType}、技能类型:{skillBaseType}、消耗MP百分比:[{mpRange.min}%,{mpRange.max}%]、造成伤害百分比:[{hpRange.min}%,{hpRange.max}%]、造成伤害:[{damageRange.min},{damageRange.max}]";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            SkillHitConditionParam result;
            var parts = str.Split(new[] { '、' }, StringSplitOptions.RemoveEmptyEntries);
            var targetTypeStr = parts[0].Replace("目标类型:", "");
            result.targetType = (ConditionTargetType)Enum.Parse(typeof(ConditionTargetType), targetTypeStr);
            var damageProperty = parts[3].Replace("造成伤害百分比:", "");
            result.hpRange = Range.GetRange(damageProperty);
            var damageRangeStr = parts[4].Replace("造成伤害:", "");
            result.damageRange = Range.GetRange(damageRangeStr);
            var skillStr = parts[1].Replace("技能类型:", "");
            result.skillBaseType = (SkillBaseType)Enum.Parse(typeof(SkillBaseType), skillStr);
            var mpProperty = parts[2].Replace("消耗MP百分比:", "");
            result.mpRange = Range.GetRange(mpProperty);
            return (T)(IConditionParam)result;
        }
    }
    
    [Serializable]
    [JsonSerializable]
    public struct DeathConditionParam : IConditionParam 
    {
        public TriggerType GetTriggerType() => TriggerType.OnDeath;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader();
        }
        public string GetConditionDesc()
        {
            return "";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            return (T)(IConditionParam)new DeathConditionParam();
        }
    }
    
    [Serializable]
    [JsonSerializable]
    public struct MoveConditionParam : IConditionParam
    {
        public TriggerType GetTriggerType() => TriggerType.OnMove;

        public bool CheckConditionValid()
        {
            return this.CheckConditionHeader();
        }
        public string GetConditionDesc()
        {
            return $"";
        }

        public T AnalysisConditionParam<T>(string str) where T : IConditionParam
        {
            if (str == null) return default;
            return (T)(IConditionParam)new DeathConditionParam();
        }    
    }

    public static class ConditionExtension
    {
        public static bool CheckConditionHeader(this IConditionParam conditionParam)
        {
            var header = conditionParam.GetTriggerType();
            return Enum.IsDefined(typeof(TriggerType), header);
        }

        public static T GetConditionParameter<T>(this TriggerType triggerType) where T : IConditionParam
        {
            switch (triggerType)
            {
                case TriggerType.OnAttackHit:
                    return (T)(IConditionParam)new AttackHitConditionParam();
                case TriggerType.OnSkillCast:
                    return (T)(IConditionParam)new SkillCastConditionParam();
                case TriggerType.OnTakeDamage:
                    return (T)(IConditionParam)new TakeDamageConditionParam();
                case TriggerType.OnKill:
                    return (T)(IConditionParam)new KillConditionParam();
                case TriggerType.OnHpChange:
                    return (T)(IConditionParam)new HpChangeConditionParam();
                case TriggerType.OnManaChange:
                    return (T)(IConditionParam)new MpChangeConditionParam();
                case TriggerType.OnCriticalHit:
                    return (T)(IConditionParam)new CriticalHitConditionParam();
                case TriggerType.OnDodge:
                    return (T)(IConditionParam)new DodgeConditionParam();
                case TriggerType.OnAttack:
                    return (T)(IConditionParam)new AttackConditionParam();
                case TriggerType.OnSkillHit:
                    return (T)(IConditionParam)new SkillHitConditionParam();
                case TriggerType.OnDeath:
                    return (T)(IConditionParam)new DeathConditionParam();
                case TriggerType.OnMove:
                    return (T)(IConditionParam)new MoveConditionParam();
                default:
                    return default;
            }
        }
    }
}