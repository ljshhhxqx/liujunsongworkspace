using System;
using System.Collections.Generic;
using System.Text;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.PredictSystem.State;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "SkillConfig", menuName = "ScriptableObjects/SkillConfig")]
    public class SkillConfig : ConfigBase
    {
        [ReadOnly] [SerializeField] private List<SkillConfigData> skillData = new List<SkillConfigData>();

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            skillData.Clear();
            var setting = new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            };
            setting.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
            for (var i = 2; i < textAsset.Count; i++)
            {
                var text = textAsset[i];
                var data = new SkillConfigData();
                data.id = int.Parse(text[0]);
                data.name = text[1];
                data.description = text[2];
                data.cooldown = float.Parse(text[3]);
                data.animationState = Enum.Parse<AnimationState>(text[4]);
                data.particleId = int.Parse(text[5]);
                data.baseValue = float.Parse(text[6]);
                data.extraRatio = float.Parse(text[7]);
                data.maxDistance = float.Parse(text[8]);
                data.radius = float.Parse(text[9]);
                data.isAreaOfRange = bool.Parse(text[10]);
                data.controlType = Enum.Parse<ControlSkillType>(text[11]);
                data.controlTime = float.Parse(text[12]);
                data.costProperty = Enum.Parse<PropertyTypeEnum>(text[13]);
                data.isFly = bool.Parse(text[14]);
                data.duration = float.Parse(text[15]);
                data.buffOperationType = Enum.Parse<BuffOperationType>(text[16]);
                data.colliderType = Enum.Parse<ColliderType>(text[17]);
                data.buffProperty = Enum.Parse<PropertyTypeEnum>(text[18]);
                data.effectProperty = Enum.Parse<PropertyTypeEnum>(text[19]);
                data.cost = float.Parse(text[20]);
                data.isCostCurrentPercent = bool.Parse(text[21]);
                data.events = JsonConvert.DeserializeObject<SkillConfigEventData[]>(text[22], setting);
                skillData.Add(data);
            }
        }
        
        public SkillConfigData GetSkillData(int id)
        {
            return skillData.Find(data => data.id == id);
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
            if (propertyCalculator.PropertyType != skillData.buffProperty)
            {
                Debug.LogError($"{skillData.name} buff property type is not match with property calculator property type");
                return null;
            }
            var desc = new StringBuilder(skillData.description);
            desc = desc.Replace("{value}", $"{propertyCalculator.CurrentValue * skillData.extraRatio:F0}");
            return desc.ToString();
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
        public int particleId;
        public float baseValue;
        public float extraRatio;
        public float maxDistance;
        public float radius;
        public bool isAreaOfRange;
        //命中的玩家施加什么控制
        public ControlSkillType controlType;
        //命中的玩家控制时间
        public float controlTime;
        //消耗的属性类型
        public PropertyTypeEnum costProperty;
        public bool isFly;
        //技能生命周期
        public float duration;
        public BuffOperationType buffOperationType;
        public ColliderType colliderType;
        //该技能的数值被施法者的什么属性类型影响
        public PropertyTypeEnum buffProperty;
        //该技能作用于目标的什么属性类型
        public PropertyTypeEnum effectProperty;
        public float cost;
        public bool isCostCurrentPercent;
        public SkillConfigEventData[] events;
    }
    
    [Serializable]
    [JsonSerializable]
    public struct SkillConfigEventData
    {
        public SkillEventType skillEventType;
        public float fireTime;

        public bool UpdateAndCheck(float currentTime)
        {
            return currentTime >= fireTime;
        }
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
    
    //技能主动触发的事件
    public enum SkillEventType
    {
        None = 0,
        OnCast,
        OnHitUpdate,
        OnEnd,
    }
}