using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationState = AOTScripts.Data.AnimationState;
using PropertyCalculator = AOTScripts.Data.PropertyCalculator;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "SkillConfig", menuName = "ScriptableObjects/SkillConfig")]
    public class SkillConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField] private List<SkillConfigData> skillData = new List<SkillConfigData>();
        
        public Dictionary<int, SkillConfigData> SkillDataDictionary { get; } = new Dictionary<int, SkillConfigData>();

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            skillData.Clear();
            var setting = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            setting.Converters.Add(new StringEnumConverter());
            for (var i = 2; i < textAsset.Count; i++)
            {
                var text = textAsset[i];
                var data = new SkillConfigData();
                data.id = int.Parse(text[0]);
                data.name = text[1];
                data.description = text[2];
                data.cooldown = float.Parse(text[3]);
                data.animationState = (AnimationState)Enum.Parse(typeof(AnimationState),text[4]);
                data.particleName = text[5].Trim();
                data.maxDistance = float.Parse(text[6]);
                data.radius = float.Parse(text[7]);
                data.isAreaOfRange = bool.Parse(text[8]);
                data.costProperty = (PropertyTypeEnum)Enum.Parse(typeof(PropertyTypeEnum),text[9]);
                data.flySpeed = float.Parse(text[10]);
                data.duration = float.Parse(text[11]);
                data.operationType = (OperationType)Enum.Parse(typeof(OperationType),text[12]);
                data.colliderType = (ColliderType)Enum.Parse(typeof(ColliderType),text[13]);
                data.cost = float.Parse(text[14]);
                data.isCostCurrentPercent = bool.Parse(text[15]);
                data.events = JsonConvert.DeserializeObject<SkillConfigEventData[]>(text[16], setting);
                data.conditionTarget = (ConditionTargetType)Enum.Parse(typeof(ConditionTargetType), text[17]);
                data.extraEffects = JsonConvert.DeserializeObject<SkillHitExtraEffectData[]>(text[18], setting);
                data.skillType = (SkillType)Enum.Parse(typeof(SkillType), text[19]);
                skillData.Add(data);
            }
        }
        
        public SkillConfigData GetSkillData(int id)
        {
            if (SkillDataDictionary.TryGetValue(id, out var skillConfigData))
            {
                return skillConfigData;    
            }

            for (int i = 0; i < skillData.Count; i++)
            {
                var data = skillData[i];
                if (data.id == id)
                {
                    SkillDataDictionary.Add(id, data);
                    return data;
                }
            }
            Debug.LogError($"No Skill Data found with id: {id}");
            return default(SkillConfigData);
        }

        public static bool IsSkillCostEnough(SkillConfigData skillData, PropertyCalculator propertyCalculator)
        {
            var propertyType = skillData.costProperty;
            var cost = skillData.cost;
            var isPropertyRight = propertyType == PropertyTypeEnum.None || propertyCalculator.PropertyType == propertyType;
            if (propertyType == PropertyTypeEnum.None)
            {
                return true;
            }

            if (skillData.isCostCurrentPercent)
            {
                return isPropertyRight && propertyCalculator.CurrentValue >= propertyCalculator.CurrentValue * (cost / 100);
            }
            return isPropertyRight && propertyCalculator.CurrentValue >= cost;
        }

        public static string GetSkillValueByBuff(SkillConfigData skillData, PropertyCalculator propertyCalculator)
        {
            // if (propertyCalculator.PropertyType != skillData.buffProperty)
            // {
            //     Debug.LogError($"{skillData.name} buff property type is not match with property calculator property type");
            //     return null;
            // }
            // var desc = new StringBuilder(skillData.description);
            // desc = desc.Replace("{value}", $"{propertyCalculator.CurrentValue * skillData.extraRatio:F0}");
            // return desc.ToString();
            return null;
        }
        

        public static AnimationState GetAnimationState(PlayerItemType playerItemType)
        {
            switch (playerItemType)
            {
                case PlayerItemType.Weapon:
                    return AnimationState.SkillQ;
                case PlayerItemType.Armor:
                    return AnimationState.SkillE;
                default:
                    throw new ArgumentOutOfRangeException(nameof(playerItemType), playerItemType, null);
            }
        }
    }

    [Serializable]
    [JsonSerializable]
    public struct SkillConfigData
    {
        public int id;
        public string name;
        public string description;
        public float cooldown;
        public AnimationState animationState;
        public string particleName;
        public float maxDistance;
        public float radius;
        public bool isAreaOfRange;
        //消耗的属性类型
        public PropertyTypeEnum costProperty;
        public float flySpeed;
        //技能生命周期
        public float duration;
        [FormerlySerializedAs("buffOperationType")] public OperationType operationType;
        public ColliderType colliderType;
        public float cost;
        public bool isCostCurrentPercent;
        public SkillConfigEventData[] events;
        //技能首要选择的目标类型（如果没有目标时系统自选的目标类型）
        public ConditionTargetType conditionTarget;
        //技能附加效果
        public SkillHitExtraEffectData[] extraEffects;
        public SkillType skillType;
        public SkillAudioType controlSkillType;
    }

    public enum SkillAudioType
    {
        Buff,
        Debuff,
        Damage,
        Control,
        Heal,
        
    }

    // [JsonSerializable]
    // [Serializable]
    // public struct SingleTargetDamageSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public DamageType damageType;
    //     public float baseDamageRatio;
    //     public float extraDamageRatio;
    //     public float maxDistance;
    //     
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public SingleTargetDamageSkillData(SkillConfigExtraData skillConfigExtraData, DamageType damageType, float baseDamageRatio, float extraDamageRatio, float maxDistance)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.damageType = damageType;
    //         this.baseDamageRatio = baseDamageRatio;
    //         this.extraDamageRatio = extraDamageRatio;
    //         this.maxDistance = maxDistance;
    //     }
    // }
    //
    // [JsonSerializable]
    // [Serializable]
    // public struct SingleTargetControlSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public float baseControlRatio;
    //     public float extraControlRatio;
    //     public float maxDistance;
    //     public float controlId;
    //     
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public SingleTargetControlSkillData(SkillConfigExtraData skillConfigExtraData, 
    //         float baseControlRatio, float extraControlRatio, float maxDistance, float controlId)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.baseControlRatio = baseControlRatio;
    //         this.extraControlRatio = extraControlRatio;
    //         this.maxDistance = maxDistance;
    //         this.controlId = controlId;
    //     }
    // }
    //
    // [JsonSerializable]
    // [Serializable]
    // public struct SingleTargetHealSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public float baseHealRatio;
    //     public float extraHealRatio;
    //     public float maxDistance;
    //     
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public SingleTargetHealSkillData(SkillConfigExtraData skillConfigExtraData, 
    //         float baseHealRatio, float extraHealRatio, float maxDistance)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.baseHealRatio = baseHealRatio;
    //         this.extraHealRatio = extraHealRatio;
    //         this.maxDistance = maxDistance;
    //     }
    // }
    //
    // [JsonSerializable]
    // [Serializable]
    // public struct SingleContinuousDamageSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public DamageType damageType;
    //     public float baseDamageRatio;
    //     public float extraDamageRatio;
    //     public float duration;
    //     public float interval;
    //     public float maxDistance;
    //     
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public SingleContinuousDamageSkillData(SkillConfigExtraData skillConfigExtraData, DamageType damageType, 
    //         float baseDamageRatio, float extraDamageRatio, float duration, float interval, float maxDistance)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.damageType = damageType;
    //         this.baseDamageRatio = baseDamageRatio;
    //         this.extraDamageRatio = extraDamageRatio;
    //         this.duration = duration;
    //         this.interval = interval;
    //         this.maxDistance = maxDistance;
    //     }
    // }   
    //
    // [JsonSerializable]
    // [Serializable]
    // public struct AreaDamageTargetSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public DamageType damageType;
    //     public float baseDamageRatio;
    //     public float extraDamageRatio;
    //     public float radius;
    //     public float maxDistance;
    //     
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public AreaDamageTargetSkillData(SkillConfigExtraData skillConfigExtraData, DamageType damageType, 
    //         float baseDamageRatio, float extraDamageRatio, float radius, float maxDistance)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.damageType = damageType;
    //         this.baseDamageRatio = baseDamageRatio;
    //         this.extraDamageRatio = extraDamageRatio;
    //         this.radius = radius;
    //         this.maxDistance = maxDistance;
    //     }
    // }
    //
    // [JsonSerializable]
    // [Serializable]
    // public struct AreaTargetControlSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public float baseControlRatio;
    //     public float extraControlRatio;
    //     public float radius;
    //     public float maxDistance;
    //     public float controlId;
    //     
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public AreaTargetControlSkillData(SkillConfigExtraData skillConfigExtraData,
    //         float baseControlRatio, float extraControlRatio, float radius, float maxDistance, float controlId)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.baseControlRatio = baseControlRatio;
    //         this.extraControlRatio = extraControlRatio;
    //         this.radius = radius;
    //         this.maxDistance = maxDistance;
    //         this.controlId = controlId;
    //     }
    // }
    // [JsonSerializable]
    // [Serializable]
    // public struct AreaHealTargetSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public float baseHealRatio;
    //     public float extraHealRatio;
    //     public float radius;
    //     public float maxDistance;
    //     
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public AreaHealTargetSkillData(SkillConfigExtraData skillConfigExtraData,
    //         float baseHealRatio, float extraHealRatio, float radius, float maxDistance)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.baseHealRatio = baseHealRatio;
    //         this.extraHealRatio = extraHealRatio;
    //         this.radius = radius;
    //         this.maxDistance = maxDistance;
    //     }
    // }
    //
    // [JsonSerializable]
    // [Serializable]
    // public struct AreaContinuousDamageSkillData : ISkillConfigExtraData
    // {
    //     public SkillConfigExtraData skillConfigExtraData;
    //     public DamageType damageType;
    //     public float baseDamageRatio;
    //     public float extraDamageRatio;
    //     public float duration;
    //     public float interval;
    //     public float radius;
    //     public float maxDistance;
    //
    //     public SkillConfigExtraData GetExtraData() => skillConfigExtraData;
    //
    //     public AreaContinuousDamageSkillData(SkillConfigExtraData skillConfigExtraData, DamageType damageType,
    //         float baseDamageRatio, float extraDamageRatio, float duration, float interval, float radius,
    //         float maxDistance)
    //     {
    //         this.skillConfigExtraData = skillConfigExtraData;
    //         this.damageType = damageType;
    //         this.baseDamageRatio = baseDamageRatio;
    //         this.extraDamageRatio = extraDamageRatio;
    //         this.duration = duration;
    //         this.interval = interval;
    //         this.radius = radius;
    //         this.maxDistance = maxDistance;
    //     }
    // }
    
}