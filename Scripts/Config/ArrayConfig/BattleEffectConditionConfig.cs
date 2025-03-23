using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using CustomEditor.Scripts;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "BattleEffectConditionConfig", menuName = "ScriptableObjects/BattleEffectConditionConfig")]
    public class BattleEffectConditionConfig : ConfigBase
    {
        [ReadOnly] [SerializeField]
        private List<BattleEffectConditionConfigData> conditionList = new List<BattleEffectConditionConfigData>();

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
                conditionData.effectType = (EffectType)Enum.Parse(typeof(EffectType), text[3]);
                conditionData.controlTime = float.Parse(text[4]);
                conditionData.interval = float.Parse(text[5]);
                conditionData.extraData = JsonConvert.DeserializeObject<BuffExtraData>(text[6]);
                conditionData.targetType = (ConditionTargetType)Enum.Parse(typeof(ConditionTargetType), text[7]);
                conditionData.targetCount = int.Parse(text[8]);
                conditionData.conditionParam = JsonConvert.DeserializeObject<IConditionParam>(text[9]);
                conditionList.Add(conditionData);
            }
        }

#if UNITY_EDITOR
        [SerializeField] private BattleEffectConditionConfigData effectData;
        public BattleEffectConditionConfigData EffectData => effectData;

        // 用于在 Inspector 中编辑数据
        public void SetEffectData(BattleEffectConditionConfigData data)
        {
            effectData = data;
        }

        [UnityEditor.CustomEditor(typeof(BattleEffectConditionConfig))]
        public class BattleEffectConditionConfigEditor : Editor
        {
            private SerializedProperty _serializedProperty;
            private readonly Dictionary<TriggerType, Type> _conditionParams = new Dictionary<TriggerType, Type>();
            //private TriggerType _selectedIndex = 0;

            private void OnEnable()
            {
                _serializedProperty = serializedObject.FindProperty("effectData");
                _conditionParams.Clear();
                var triggerTypes = Enum.GetValues(typeof(TriggerType));
                for (int i = 0; i < triggerTypes.Length; i++)
                {
                    var triggerType = (TriggerType)triggerTypes.GetValue(i);
                    if (triggerType == TriggerType.None) continue;
                    var conditionParamType = triggerType.GetConditionParameter<IConditionParam>();
                    _conditionParams.Add(triggerType, conditionParamType.GetType());
                }
            }
            public override void OnInspectorGUI()
            {
                serializedObject.Update();
                EditorGUI.BeginChangeCheck();
                
                // 触发类型
                var triggerTypeProp = _serializedProperty.FindPropertyRelative("triggerType");
                EditorGUILayout.PropertyField(triggerTypeProp, new GUIContent("触发类型"));

                // 概率
                var probabilityProp = _serializedProperty.FindPropertyRelative("probability");
                probabilityProp.floatValue = EditorGUILayout.Slider("概率 (%)", probabilityProp.floatValue, 0f, 100f);

                // 目标数量
                var targetCountProp = _serializedProperty.FindPropertyRelative("targetCount");
                targetCountProp.intValue = EditorGUILayout.IntField("目标数量", Mathf.Max(1, targetCountProp.intValue));

                // 目标类型
                var targetTypeProp = _serializedProperty.FindPropertyRelative("targetType");
                EditorGUILayout.PropertyField(targetTypeProp, new GUIContent("目标类型"));

                // 冷却时间
                var intervalProp = _serializedProperty.FindPropertyRelative("interval");
                intervalProp.floatValue = EditorGUILayout.FloatField("冷却时间 (秒)", Mathf.Max(0f, intervalProp.floatValue));

                // 动态参数编辑
                var triggerParamsProp = _serializedProperty.FindPropertyRelative("conditionParam");
                TriggerType selectedTrigger = (TriggerType)triggerTypeProp.enumValueIndex;

                if (_conditionParams.TryGetValue(selectedTrigger, out var paramType))
                {
                    if (triggerParamsProp.managedReferenceValue == null || 
                        triggerParamsProp.managedReferenceValue.GetType() != paramType)
                    {
                        triggerParamsProp.managedReferenceValue = Activator.CreateInstance(paramType);
                    }
                    EditorGUILayout.PropertyField(triggerParamsProp, new GUIContent("额外参数"), true);
                }
                else
                {
                    triggerParamsProp.managedReferenceValue = null;
                    EditorGUILayout.LabelField("该触发类型无额外参数");
                }

                if (EditorGUI.EndChangeCheck())
                {
                    serializedObject.ApplyModifiedProperties();
                }

                // 生成字符串按钮
                if (GUILayout.Button("生成效果字符串"))
                {
                    string effectString = GenerateEffectString((BattleEffectConditionConfig)target);
                    EditorGUIUtility.systemCopyBuffer = effectString; // 复制到剪贴板
                    Debug.Log($"生成的效果字符串: {effectString} (已复制到剪贴板)");
                }
                // 标记为脏，确保修改被保存
                if (GUI.changed)
                {
                    EditorUtility.SetDirty(target);
                }
            }

            private string GenerateEffectString(BattleEffectConditionConfig asset)
            {
                var data = asset.EffectData;
                string triggerName = EnumHeaderParser.GetEnumHeaders(typeof(TriggerType))[data.triggerType];
                string targetName = EnumHeaderParser.GetEnumHeaders(typeof(ConditionTargetType))[data.targetType];
                string paramString = data.conditionParam?.GetConditionDesc() ?? "";

                string effectString = $"[PassiveEffect]{triggerName},概率{data.probability}%,目标{data.targetCount}个{targetName},冷却{data.interval}秒";
                if (!string.IsNullOrEmpty(paramString))
                {
                    effectString += $",{paramString}";
                }

                return effectString;
            }
#endif
        }
    }
    
    
    [Serializable]
    [JsonSerializable]
    public struct BattleEffectConditionConfigData
    {
        [Header("Id")] public int id;
        [Header("触发类型")] public TriggerType triggerType;
        [Header("触发概率")] public float probability;
        [Header("控制效果类型")] public EffectType effectType;
        [Header("控制时间")] public float controlTime;
        [Header("触发间隔")] public float interval;
        [Header("Buff")] public BuffExtraData extraData;
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