using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
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
            // var setting = new JsonSerializerSettings
            // {
            //     NullValueHandling = NullValueHandling.Ignore
            // };
            // setting.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
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
                data.propertyType = Enum.Parse<PropertyTypeEnum>(text[13]);
                data.isFlash = bool.Parse(text[14]);
                data.duration = float.Parse(text[15]);
                data.interval = float.Parse(text[16]);
                data.buffOperationType = Enum.Parse<BuffOperationType>(text[17]);
                data.colliderType = Enum.Parse<ColliderType>(text[18]);
                skillData.Add(data);
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
        public int particleId;
        public float baseValue;
        public float extraRatio;
        public float maxDistance;
        public float radius;
        public bool isAreaOfRange;
        public ControlSkillType controlType;
        public float controlTime;
        public PropertyTypeEnum propertyType;
        public bool isFlash;
        public float duration;
        public float interval;
        public BuffOperationType buffOperationType;
        public ColliderType colliderType;
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