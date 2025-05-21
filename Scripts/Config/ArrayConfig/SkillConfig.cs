using System;
using System.Collections.Generic;
using AOTScripts.CustomAttribute;
using AOTScripts.Data;
using Mirror;
using Newtonsoft.Json;
using UnityEngine;
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
            for (var i = 2; i < textAsset.Count; i++)
            {
                var text = textAsset[i];
                var data = new SkillConfigData();
                data.id = int.Parse(text[0]);
                data.name = text[1];
                data.description = text[2];
                data.cooldown = float.Parse(text[3]);
                data.animationState = JsonConvert.DeserializeObject<AnimationState>(text[4]);
                data.particleId = int.Parse(text[5]);
                data.damageData = JsonConvert.DeserializeObject<DamageSkillData>(text[6]);
                data.healData = JsonConvert.DeserializeObject<HealSkillData>(text[7]);
                data.controlData = JsonConvert.DeserializeObject<ControlSkillData>(text[8]);
                data.moveData = JsonConvert.DeserializeObject<MoveSkillData>(text[9]);
                data.continuousEffectData = JsonConvert.DeserializeObject<ContinuousEffectSkillData>(text[10]);
                data.areaEffectData = JsonConvert.DeserializeObject<AreaEffectSkillData>(text[11]);
                data.singleEffectData = JsonConvert.DeserializeObject<SingleEffectSkillData>(text[12]);
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
        public DamageSkillData damageData;
        public HealSkillData healData;
        public ControlSkillData controlData;
        public MoveSkillData moveData;
        public ContinuousEffectSkillData continuousEffectData;
        public AreaEffectSkillData areaEffectData;
        public SingleEffectSkillData singleEffectData;
    }

    [Serializable]
    [JsonSerializable]
    public struct DamageSkillData
    {
        public float baseDamageRatio;
        public float extraDamageRatio;
        //吃到伤害增益的属性
        public PropertyTypeEnum propertyType;
        public DamageType damageType;
    }

    [Serializable]
    [JsonSerializable]
    public struct HealSkillData
    {
        public float baseHealRatio;
        public float extraHealRatio;
        //吃到治疗增益的属性
        public PropertyTypeEnum propertyType;
        //恢复属性
        public PropertyTypeEnum healPropertyType;
    }

    [Serializable]
    [JsonSerializable]
    public struct ControlSkillData
    {
        public int controlId;
    }

    [Serializable]
    [JsonSerializable]
    public struct MoveSkillData
    {
        public float maxMoveDistance;
        //是否是瞬移，不是则需要一段位移过程
        public bool isFlash;
        //指定的目标类型，没有则可以选择地面或墙壁
        public ConditionTargetType conditionTarget;
    }

    [Serializable]
    [JsonSerializable]
    public struct ContinuousEffectSkillData
    {
        public float duration;
        public float interval;
    }

    [Serializable]
    [JsonSerializable]
    public struct AreaEffectSkillData
    {
        public float radius;
        public float maxDistance;
    }

    [Serializable]
    [JsonSerializable]
    public struct SingleEffectSkillData
    {
        public float maxDistance;
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