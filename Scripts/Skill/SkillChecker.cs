﻿using System;
using System.Collections.Generic;
using System.Threading;
using AOTScripts.Data;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using UniRx;
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
        public Vector3 PlayerPosition;
        [MemoryPackOrder(1)]
        public Vector3 TargetPosition;
        [MemoryPackOrder(2)]
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
        public int EffectCount;
        [MemoryPackOrder(7)]
        public SkillEffectFlyType SkillEffectFlyType;
        [MemoryPackOrder(8)]
        public Vector3 CurrentPosition;
        [MemoryPackOrder(9)]
        public List<SkillEventData> SkillEventData;

        [MemoryPackIgnore]
        public IColliderConfig ColliderConfig { get; }

         public event Action OnDestroy;

        public SkillEffectLifeCycle(Vector3 origin, Vector3 target, float size, float speed, float expectationTime, int effectCount, SkillEffectFlyType skillEffectFlyType,
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
    public partial class SingleTargetContinuousSkillChecker : ISkillChecker
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

        public SingleTargetContinuousSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,SkillEffectLifeCycle skillEffectLifeCycle)
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
    
    //飞行技能、命中后造成单体伤害的技能(可以有控制技能)
    [MemoryPackable]
    public partial class SingleTargetDamageSkillChecker : ISkillChecker
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

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;
        
        public SingleTargetDamageSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillEffectLifeCycle skillEffectLifeCycle)
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
    //技能引导后立即在目标出释放的范围持续技能
    [MemoryPackable]
    public partial class AreaOfRangedContinuousSkillChecker : ISkillChecker
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