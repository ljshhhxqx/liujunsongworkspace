using System;
using System.Collections.Generic;
using System.Threading;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Skill
{
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(SingleTargetContinuousSkillChecker))]
    [MemoryPackUnion(1, typeof(SingleTargetDamageSkillChecker))]
    [MemoryPackUnion(2, typeof(AreaOfRangedSkillChecker))]
    [MemoryPackUnion(3, typeof(DashSkillChecker))]
    [MemoryPackUnion(4, typeof(AreaOfRangedFlySkillChecker))]
    [MemoryPackUnion(5, typeof(AreaOfRangedContinuousSkillChecker))]
    public partial interface ISkillChecker
    {
        CooldownHeader GetCooldownHeader();
        CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader);
        CommonSkillCheckerHeader GetCommonSkillCheckerHeader();
        float GetFlyDistance();
        bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams);
        bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args);
        void Destroy();
        int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc);
    }

    public static class SkillCheckerExtensions
    {
        public static bool IsSkillNotCdAndCostEnough(this ISkillChecker skillChecker, SkillCheckerParams skillCheckerParams)
        {
            var cooldownHeader = skillChecker.GetCooldownHeader();
            if (cooldownHeader.IsCooldown())
                return false;
            var skillCheckHeader = skillChecker.GetCommonSkillCheckerHeader();
            if (skillCheckerParams.StrengthCalculator.PropertyType != PropertyTypeEnum.Strength
                || skillCheckerParams.StrengthCalculator.CurrentValue < skillCheckHeader.SkillCost)
            {
                return false;
            }
            return skillChecker.CheckExecute(ref skillChecker, skillCheckerParams);
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
        [MemoryPackOrder(10)] public PropertyTypeEnum EffectPropertyType;
        [MemoryPackOrder(11)] public float Radius;
    }

    [MemoryPackable]
    public partial struct SkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public PropertyCalculator StrengthCalculator;
        [MemoryPackOrder(1)]
        public Vector3 PlayerPosition;
        [MemoryPackOrder(2)]
        public Vector3 TargetPosition;
        [MemoryPackOrder(3)]
        public float Radius;
    }
    
    //持续性技能的生命周期
    [MemoryPackable]
    public partial class SkillContinuousLifeCycle
    {
        [MemoryPackOrder(0)]
        public Vector3 Target;
        [MemoryPackOrder(1)]
        public float Size;
        [MemoryPackOrder(2)]
        public float Interval;
        [MemoryPackOrder(3)] 
        public float CurrentTime;
        [MemoryPackOrder(4)] 
        public float MaxTime;
        [MemoryPackIgnore]
        private IColliderConfig _colliderConfig;
        [MemoryPackIgnore]
        public IColliderConfig ColliderConfig => _colliderConfig;

        public SkillContinuousLifeCycle(Vector3 target, float size, float interval, float maxTime,
            float currentTime = 0)
        {
            Target = target;
            Size = size;
            Interval = interval;
            MaxTime = maxTime;
            CurrentTime = currentTime;
            _colliderConfig = new SphereColliderConfig
            {
                Radius = size,
                Center = Vector3.zero
            };
        }
        
        public void Update()
        {
            if (CurrentTime >= MaxTime)
            {
                return;
            }
            CurrentTime += Interval;
        }
    }

    //非持续性、飞行技能的生命周期
    [MemoryPackable]
    public partial class SkillFlyEffectLifeCycle
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
        //预期到达目标位置的时间，如果为0则立即在目标处释放
        [MemoryPackOrder(5)]
        public float ExpectationTime;
        [MemoryPackOrder(6)] 
        public int EffectCount;
        [MemoryPackOrder(7)]
        public SkillEffectFlyType SkillEffectFlyType;
        [MemoryPackOrder(8)]
        public Vector3 CurrentPosition;
        [MemoryPackIgnore]
        private IColliderConfig _colliderConfig;
        [MemoryPackIgnore]
        public IColliderConfig ColliderConfig => _colliderConfig;
        
        public SkillFlyEffectLifeCycle(Vector3 origin, Vector3 target, float size, float speed, float expectationTime, int effectCount, SkillEffectFlyType skillEffectFlyType,
            float currentTime = 0)
        {
            Origin = origin;
            Target = target;
            Size = size;
            Speed = speed;
            ExpectationTime = expectationTime;
            EffectCount = effectCount;
            CurrentTime = currentTime;
            SkillEffectFlyType = skillEffectFlyType;
            _colliderConfig = new SphereColliderConfig
            {
                Radius = size,
                Center = Vector3.zero
            };
        }

        public int[] Update(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            if (CurrentTime >= ExpectationTime  || Vector3.Distance(CurrentPosition, Target) < 0.1f)
            {
                return null;
            }
            CurrentTime += deltaTime;
            var distance = Vector3.Distance(Origin, Target);
            var step = Speed * deltaTime;
            if (step > distance)
            {
                step = distance;
            }
            CurrentPosition += (Target - Origin).normalized * step;
            return isHitFunc(CurrentPosition, _colliderConfig);
        }
    }

    //飞行技能、命中后造成单体持续伤害的技能
    [MemoryPackable]
    public partial class SingleTargetContinuousSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(3)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        [MemoryPackOrder(4)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;

        public bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams)
        {
            return this.IsSkillNotCdAndCostEnough(skillCheckerParams);
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        public SingleTargetContinuousSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,SkillFlyEffectLifeCycle skillFlyEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillFlyEffectLifeCycle = skillFlyEffectLifeCycle;
        }

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            return new CooldownHeader
            {
                CurrentTime = cooldownHeader.CurrentTime,
                Cooldown = cooldownHeader.Cooldown,
            };
        }

        public void Destroy()
        {
            SkillFlyEffectLifeCycle = null;
        }

        //释放、飞行、命中后造成伤害
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
        }
    }
    
    //飞行技能、命中后造成单体伤害的技能(可以有控制技能)
    [MemoryPackable]
    public partial class SingleTargetDamageSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;
        
        public SingleTargetDamageSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillFlyEffectLifeCycle skillFlyEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillFlyEffectLifeCycle = skillFlyEffectLifeCycle;
        }

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            return new CooldownHeader
            {
                CurrentTime = cooldownHeader.CurrentTime,
                Cooldown = cooldownHeader.Cooldown,
            };
        }

        public bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams)
        {
            return this.IsSkillNotCdAndCostEnough(skillCheckerParams);
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        public void Destroy()
        {
            SkillFlyEffectLifeCycle = null;
        }

        //释放、飞行、命中后造成伤害
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
        }
    }
    
    //位移技能
    [MemoryPackable]
    public partial class DashSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;
        
        public DashSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillFlyEffectLifeCycle skillFlyEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillFlyEffectLifeCycle = skillFlyEffectLifeCycle;
        }

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            return new CooldownHeader
            {
                CurrentTime = cooldownHeader.CurrentTime,
                Cooldown = cooldownHeader.Cooldown,
            };
        }

        public bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams)
        {
            return this.IsSkillNotCdAndCostEnough(skillCheckerParams);
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
        }
        
        public void Destroy()
        {
            SkillFlyEffectLifeCycle = null;
        } 
    }
    
    [MemoryPackable]
    public partial class AreaOfRangedFlySkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            return new CooldownHeader
            {
                CurrentTime = cooldownHeader.CurrentTime,
                Cooldown = cooldownHeader.Cooldown,
            };
        }

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;

        
        public bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams)
        {
            return this.IsSkillNotCdAndCostEnough(skillCheckerParams);
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        public void Destroy()
        {
            SkillFlyEffectLifeCycle = null;
        }
        //释放、飞行、命中后造成伤害
       
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
        }
    }
    
    //技能引导后立即在目标出释放的范围技能
    [MemoryPackable]
    public partial class AreaOfRangedSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            return new CooldownHeader
            {
                CurrentTime = cooldownHeader.CurrentTime,
                Cooldown = cooldownHeader.Cooldown,
            };
        }

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;

        
        public bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams)
        {
            return this.IsSkillNotCdAndCostEnough(skillCheckerParams);
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
        }

        public void Destroy()
        {
            SkillFlyEffectLifeCycle = null;
        }
    }
    //技能引导后立即在目标出释放的范围持续技能
    [MemoryPackable]
    public partial class AreaOfRangedContinuousSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            return new CooldownHeader
            {
                CurrentTime = cooldownHeader.CurrentTime,
                Cooldown = cooldownHeader.Cooldown,
            };
        }

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;

        
        public bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams)
        {
            return this.IsSkillNotCdAndCostEnough(skillCheckerParams);
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        
        
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
        }

        public void Destroy()
        {
            SkillFlyEffectLifeCycle = null;
        }
    }
    [MemoryPackable]
    public partial struct PropertyCalculatorData : IEquatable<PropertyCalculatorData>
    {
        [MemoryPackOrder(0)]
        public PropertyCalculator BuffCalculator;
        [MemoryPackOrder(1)]
        public PropertyCalculator TargetCalculator;
        [MemoryPackOrder(2)]
        public BuffOperationType OperationType;
        [MemoryPackOrder(3)] 
        public int PlayerId;
        [MemoryPackOrder(4)] 
        public float Value;

        public bool Equals(PropertyCalculatorData other)
        {
            return BuffCalculator.Equals(other.BuffCalculator) && TargetCalculator.Equals(other.TargetCalculator) && OperationType == other.OperationType && PlayerId == other.PlayerId && Value.Equals(other.Value);
        }

        public override bool Equals(object obj)
        {
            return obj is PropertyCalculatorData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(BuffCalculator, TargetCalculator, (int)OperationType, PlayerId, Value);
        }

        public static bool operator ==(PropertyCalculatorData left, PropertyCalculatorData right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(PropertyCalculatorData left, PropertyCalculatorData right)
        {
            return !left.Equals(right);
        }
    }
}