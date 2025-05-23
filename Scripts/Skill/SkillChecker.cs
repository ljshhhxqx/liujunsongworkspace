using System;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Skill
{
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(SingleTargetContinuousDamageSkillChecker))]
    public partial interface ISkillChecker
    {
        CooldownHeader GetCooldownHeader();
        CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader);
        CommonSkillCheckerHeader GetCommonSkillCheckerHeader();
        bool Check<T>(ref ISkillChecker checker, T t) where T : ISkillCheckerParams;
        bool Execute<T>(ref ISkillChecker checker, T t, Action onHit, Action onMiss) where T : ISkillCheckerParams;
    }

    public static class SkillCheckerExtensions
    {
        public static bool IsSkillCheckOk(this ISkillChecker skillChecker, ISkillCheckerParams skillCheckerParams)
        {
            var cooldownHeader = skillChecker.GetCooldownHeader();
            if (cooldownHeader.IsCooldown())
                return false;
            var commonSkillCheckerHeader = skillCheckerParams.GetCommonSkillCheckerParams();
            var skillCheckHeader = skillChecker.GetCommonSkillCheckerHeader();
            if (commonSkillCheckerHeader.StrengthCalculator.PropertyType != PropertyTypeEnum.Strength
                || commonSkillCheckerHeader.StrengthCalculator.CurrentValue < skillCheckHeader.SkillCost)
            {
                return false;
            }
            return skillChecker.Check(ref skillChecker, skillCheckerParams);
        }
    }

    [MemoryPackable]
    public partial struct CommonSkillCheckerHeader
    {
        [MemoryPackOrder(0)]
        public int ConfigId;
        [MemoryPackOrder(1)]
        public int CooldownTime;
        [MemoryPackOrder(2)]
        public int SkillEffectPrefabId;
        [MemoryPackOrder(3)] public float SkillCost;
        [MemoryPackOrder(4)] public float SkillBaseValue;
        [MemoryPackOrder(5)] public float SkillExtraRatio;
        [MemoryPackOrder(6)] public float MaxDistance;
        [MemoryPackOrder(7)] public float MinDistance;
        [MemoryPackOrder(8)] public float ExistTime;
        [MemoryPackOrder(9)] public PropertyTypeEnum BuffPropertyType;
        [MemoryPackOrder(9)] public PropertyTypeEnum EffectPropertyType;
    }

    [MemoryPackable]
    public partial struct CommonSkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public PropertyCalculator StrengthCalculator;
        [MemoryPackOrder(1)]
        public bool IsLongTouch;
    }
    
    [MemoryPackable]
    public partial struct DistanceCheckerParams
    {
        [MemoryPackOrder(0)]
        public Vector3 PlayerPosition;
        [MemoryPackOrder(1)]
        public Vector3 TargetPosition;
    }
    
    [MemoryPackable]
    public partial struct DashCheckerParams
    {
        [MemoryPackOrder(0)]
        public Vector3 PlayerPosition;
        [MemoryPackOrder(1)]
        public Vector3 TargetPosition;
    }

    //检查技能释放所需要的参数
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(SingleTargetContinuousDamageSkillCheckerParams))]
    [MemoryPackUnion(1, typeof(SingleTargetFlyEffectDamageSkillCheckerParams))]
    [MemoryPackUnion(2, typeof(SingleTargetDamageControlSkillCheckerParams))]
    [MemoryPackUnion(3, typeof(DashSkillCheckerParams))]
    [MemoryPackUnion(4, typeof(SingleTargetHealSkillCheckerParams))]
    [MemoryPackUnion(5, typeof(AreaOfRangedControlContinuousDamageSkillCheckerParams))]
    [MemoryPackUnion(6, typeof(AreaOfRangedControlDamageSkillCheckerParams))]
    [MemoryPackUnion(7, typeof(AreaOfRangedContinuousHealSkillCheckerParams))]
    [MemoryPackUnion(8, typeof(AreaOfRangedFlyEffectDamageSkillCheckerParams))]
    [MemoryPackUnion(9, typeof(DashAreaOfRangedControlDamageSkillCheckerParams))]
    public partial interface ISkillCheckerParams
    {
        CommonSkillCheckerParams GetCommonSkillCheckerParams();
    }

    [MemoryPackable]
    public partial struct SingleTargetContinuousDamageSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct SingleTargetFlyEffectDamageSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct SingleTargetDamageControlSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct DashSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DashCheckerParams DashCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct SingleTargetHealSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct AreaOfRangedControlContinuousDamageSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct AreaOfRangedControlDamageSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct AreaOfRangedContinuousHealSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    [MemoryPackable]
    public partial struct AreaOfRangedFlyEffectDamageSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct DashAreaOfRangedControlDamageSkillCheckerParams : ISkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public CommonSkillCheckerParams CommonSkillCheckerParams;
        [MemoryPackOrder(1)]
        public DistanceCheckerParams DistanceCheckerParams; 
        public CommonSkillCheckerParams GetCommonSkillCheckerParams() => CommonSkillCheckerParams;
    }
    
    [MemoryPackable]
    public partial struct SkillFlyEffectLifeCycle
    { 
        [MemoryPackOrder(0)]
        public Vector3 Origin;
        [MemoryPackOrder(1)]
        public Vector3 Target;
        [MemoryPackOrder(2)]
        public float Size;
        [MemoryPackOrder(3)]
        public float Speed;
        [MemoryPackOrder(4)]
        public float CurrentTime;
        [MemoryPackOrder(5)]
        public float ExpectationTime;
        [MemoryPackOrder(6)] 
        public int EffectCount;
        
    }
    
    [MemoryPackable]
    public partial struct SkillPropertyLifeCycle
    {
        [MemoryPackOrder(0)] 
        public float BaseValue;
        [MemoryPackOrder(1)] 
        public float ExtraRatio;
        [MemoryPackOrder(2)]
        public float Cooldown;
        [MemoryPackOrder(3)]
        public float CurrentTime;
        [MemoryPackOrder(4)]
        public float EffectInterval;
        //造成技能效果时，增益受益于的属性类型
        [MemoryPackOrder(5)] 
        public PropertyTypeEnum BuffPropertyType;
        //对目标造成技能效果时，目标哪一个属性会受到效果
        [MemoryPackOrder(6)] public PropertyTypeEnum TargetPropertyType;

        //MemoryPackConstructor]
        public SkillPropertyLifeCycle(float baseValue, float extraRatio, float cooldown, float interval, 
            PropertyTypeEnum buffPropertyType, PropertyTypeEnum targetPropertyType, float currentTime = 0)
        {
            BaseValue = baseValue;
            ExtraRatio = extraRatio;
            Cooldown = cooldown;
            CurrentTime = currentTime;
            EffectInterval = interval;
            BuffPropertyType = buffPropertyType;
            TargetPropertyType = targetPropertyType;
        }
        
        public bool IsCooldown()
        {
            return CurrentTime > 0;
        }
        
        public SkillPropertyLifeCycle UpdateProperty(BuffOperationType buffOperationType, PropertyCalculator buffCalculator, ref PropertyCalculator targetCalculator, 
            out float damage)
        {
            damage = 0;
            if (!IsCooldown() || EffectInterval == 0)
            {
                return this;
            }
            if (buffCalculator.PropertyType != BuffPropertyType || targetCalculator.PropertyType != TargetPropertyType)
            {
                Debug.LogError("BuffPropertyType or TargetPropertyType is not match");
                return this;
            }
            return TakeEffect(this, buffOperationType, buffCalculator, ref targetCalculator, out damage);
        }

        private static SkillPropertyLifeCycle TakeEffect(SkillPropertyLifeCycle skillPropertyLifeCycle, BuffOperationType buffOperationType, PropertyCalculator buffCalculator, ref PropertyCalculator targetCalculator, 
            out float damage)
        {
            damage = skillPropertyLifeCycle.BaseValue + skillPropertyLifeCycle.ExtraRatio * (skillPropertyLifeCycle.EffectInterval == 0 ? 1 : skillPropertyLifeCycle.EffectInterval);
            skillPropertyLifeCycle.CurrentTime = Math.Max(0, skillPropertyLifeCycle.CurrentTime - skillPropertyLifeCycle.EffectInterval);
            targetCalculator = targetCalculator.UpdateCalculator(targetCalculator, new BuffIncreaseData
            {
                increaseType = BuffIncreaseType.Current,
                increaseValue = damage,
                operationType = buffOperationType,
            });
            return skillPropertyLifeCycle;
        }

        public SkillPropertyLifeCycle Execute(SkillPropertyLifeCycle skillPropertyLifeCycle, BuffOperationType buffOperationType, PropertyCalculator buffCalculator, ref PropertyCalculator targetCalculator, 
            out float damage)
        {
            damage = 0;
            if (!IsCooldown())
            {
                return this;
            }
            
            if (buffCalculator.PropertyType != BuffPropertyType || targetCalculator.PropertyType != TargetPropertyType)
            {
                Debug.LogError("BuffPropertyType or TargetPropertyType is not match");
                return this;
            }
            return TakeEffect(this, buffOperationType, buffCalculator, ref targetCalculator, out damage);
        }
    }

    [MemoryPackable]
    public partial class SingleTargetContinuousDamageSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)]
        public SkillPropertyLifeCycle SkillPropertyLifeCycle;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            return new CooldownHeader
            {
                CurrentTime = cooldownHeader.CurrentTime,
                Cooldown = cooldownHeader.Cooldown,
            };
        }

        public bool Check<T>(ref ISkillChecker checker, T t) where T : ISkillCheckerParams
        {
            return this.IsSkillCheckOk(t);
        }

        public bool Execute<T>(ref ISkillChecker checker, T t, Action onHit, Action onMiss) where T : ISkillCheckerParams
        {
            if (!Check(ref checker, t))
            {
                return false;
            }
            onMiss();
            return false;
        }
    }
}