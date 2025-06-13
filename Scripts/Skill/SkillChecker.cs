using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Skill
{
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(AreaOfRangedSkillChecker))]
    [MemoryPackUnion(1, typeof(SingleTargetFlyEffectSkillChecker))]
    [MemoryPackUnion(2, typeof(DashSkillChecker))]
    [MemoryPackUnion(3, typeof(AreaOfRangedFlySkillChecker))]
    [MemoryPackUnion(4, typeof(AreaOfRangedDelayedSkillChecker))]
    public partial interface ISkillChecker
    {
        bool IsSkillNotInCd();
        bool IsSkillEffect();
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
        public static bool IsSkillNotCd(this ISkillChecker skillChecker)
        {
            var cooldownHeader = skillChecker.GetCooldownHeader();
            return !cooldownHeader.IsCooldown();
        }
    }

    [MemoryPackable]
    public partial struct CommonSkillCheckerHeader
    {
        [MemoryPackOrder(0)]
        public int ConfigId;
        [MemoryPackOrder(1)]
        public float CooldownTime;
        [MemoryPackOrder(2)]
        public string SkillEffectPrefabName;
        [MemoryPackOrder(3)] public float MaxDistance;
        [MemoryPackOrder(4)] public float ExistTime;
        [MemoryPackOrder(5)] public float Radius;
        [MemoryPackOrder(6)] public bool IsAreaOfRanged;
    }

    [MemoryPackable]
    public partial struct SkillCheckerParams
    {
        [MemoryPackOrder(0)]
        public Vector3 PlayerPosition;
        [MemoryPackOrder(1)]
        public Vector3 TargetPosition;
        [MemoryPackOrder(2)]
        public float Radius;
    }

    //技能的生命周期
    [MemoryPackable]
    public partial class SkillEffectLifeCycle
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
        public SkillEffectFlyType SkillEffectFlyType;
        [MemoryPackOrder(7)]
        public Vector3 CurrentPosition;
        [MemoryPackOrder(8)]
        public List<SkillEventData> SkillEventData;

        [MemoryPackIgnore]
        public IColliderConfig ColliderConfig { get; }

        public event Action OnDestroy;

        public SkillEffectLifeCycle(Vector3 origin, Vector3 target, float size, float speed, float expectationTime, SkillEffectFlyType skillEffectFlyType = SkillEffectFlyType.Linear,
            float currentTime = 0)
        {
            Origin = origin;
            Target = target;
            Size = size;
            Speed = speed;
            ExpectationTime = expectationTime;
            CurrentTime = currentTime;
            SkillEffectFlyType = skillEffectFlyType;
            ColliderConfig = new SphereColliderConfig
            {
                Radius = size,
                Center = Vector3.zero
            };
        }

        public SkillEventType RemoveSkillEvent(float currentTime)
        {
            for (int i = 0; i < SkillEventData.Count; i++)
            {
                 var skillEvent = SkillEventData[i];
                 if (skillEvent.UpdateAndCheck(currentTime))
                 {
                     var eventType = skillEvent.SkillEventType;
                     if (eventType == SkillEventType.OnEnd)
                     {
                         OnDestroy?.Invoke();
                         break;
                     }
                     SkillEventData.RemoveAt(i);
                     return eventType;
                 }
            }
            return SkillEventType.None;
        }

        public int[] Update(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            if (CurrentTime >= ExpectationTime)
            {
                return null;
            }
            CurrentTime += deltaTime;
            var eventType = RemoveSkillEvent(CurrentTime);
            if (eventType != SkillEventType.OnHitUpdate)
            {
                return null;
            }
            var distance = Vector3.Distance(Origin, Target);
            var step = Speed * deltaTime;
            if (step > distance)
            {
                step = distance;
            }
            CurrentPosition += (Target - Origin).normalized * step;
            return isHitFunc(CurrentPosition, ColliderConfig);
        }
    }

    //飞行技能、命中后造成单体持续伤害的技能
    [MemoryPackable]
    public partial class SingleTargetFlyEffectSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(3)] 
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        [MemoryPackOrder(4)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        public bool IsSkillNotInCd()
        {
            return this.IsSkillNotCd();
        }

        public bool IsSkillEffect()
        {
            return SkillEffectLifeCycle != null;
        }

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;

        public bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams)
        {
            return this.IsSkillNotCd();
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        public SingleTargetFlyEffectSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            SkillEffectLifeCycle.OnDestroy += Destroy;
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
            SkillEffectLifeCycle = null;
        }

        //释放、飞行、命中后造成伤害
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillEffectLifeCycle.Update(deltaTime, isHitFunc);
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
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;
        public bool IsSkillNotInCd()
        {
            return this.IsSkillNotCd();
        }

        public bool IsSkillEffect()
        {
            return SkillEffectLifeCycle != null;
        }

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;
        
        public DashSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            SkillEffectLifeCycle.OnDestroy += Destroy;
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
            return this.IsSkillNotCd();
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillEffectLifeCycle.Update(deltaTime, isHitFunc);
        }
        
        public void Destroy()
        {
            SkillEffectLifeCycle = null;
        } 
    }
    
    [MemoryPackable]
    public partial struct SkillEventData
    {
        [MemoryPackOrder(0)]
        public SkillEventType SkillEventType;
        [MemoryPackOrder(1)]
        public float FireTime;

        public bool UpdateAndCheck(float currentTime)
        {
            return currentTime >= FireTime;
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
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;

        
        public AreaOfRangedFlySkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            SkillEffectLifeCycle.OnDestroy += Destroy;
        }

        public float GetFlyDistance() => FlyDistance;
        
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public bool IsSkillNotInCd()
        {
            return this.IsSkillNotCd();
        }

        public bool IsSkillEffect()
        {
            return SkillEffectLifeCycle != null;
        }
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
            return this.IsSkillNotCd();
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        public void Destroy()
        {
            SkillEffectLifeCycle = null;
        }
        
        //释放、飞行、命中后造成伤害
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillEffectLifeCycle.Update(deltaTime, isHitFunc);
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
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;

        public AreaOfRangedSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            SkillEffectLifeCycle.OnDestroy += Destroy;
        }

        public float GetFlyDistance() => FlyDistance;
        
        public CooldownHeader GetCooldownHeader() => CooldownHeader;
        public bool IsSkillNotInCd()
        {
            return this.IsSkillNotCd();
        }

        public bool IsSkillEffect()
        {
            return SkillEffectLifeCycle != null;
        }

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
            return this.IsSkillNotCd();
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }

        
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillEffectLifeCycle.Update(deltaTime, isHitFunc);
        }

        public void Destroy()
        {
            SkillEffectLifeCycle = null;
        }
    }
    
    //延时一段时间造成效果的技能(立即在目标处释放)
    [MemoryPackable]
    public partial class AreaOfRangedDelayedSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)] 
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        [MemoryPackOrder(3)] 
        public float FlyDistance;

        
        public AreaOfRangedDelayedSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            SkillEffectLifeCycle.OnDestroy += Destroy;
        }

        public float GetFlyDistance() => FlyDistance;
        public bool IsSkillNotInCd()
        {
            return this.IsSkillNotCd();
        }

        public bool IsSkillEffect()
        {
            return SkillEffectLifeCycle != null;
        }
        
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
            return this.IsSkillNotCd();
        }

        public bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args)
        {
            return CheckExecute(ref checker, skillCheckerParams);
        }
        
        public int[] UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc)
        {
            return SkillEffectLifeCycle.Update(deltaTime, isHitFunc);
        }

        public void Destroy()
        {
            SkillEffectLifeCycle = null;
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