using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Skill
{
    public interface ISkillChecker
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
        Vector3 GetSkillEffectPosition();
        SkillEffectLifeCycle GetSkillEffectLifeCycle();
        void SetSkillData(SkillCheckerData skillCheckerData);
    }

    public static class SkillCheckerExtensions
    {
        public static bool IsSkillNotCd(this ISkillChecker skillChecker)
        {
            var cooldownHeader = skillChecker.GetCooldownHeader();
            return !cooldownHeader.IsCooldown();
        }
    }

    public struct CommonSkillCheckerHeader
    {
        public int ConfigId;
        public float CooldownTime;
        public string SkillEffectPrefabName;
        public float MaxDistance;
        public float ExistTime;
        public float Radius;
        public bool IsAreaOfRanged;
        public AnimationState AnimationState;
    }

    public struct SkillCheckerParams
    {
        public Vector3 PlayerPosition;
        public Vector3 TargetPosition;
        public float Radius;
    }

    //技能的生命周期
    public class SkillEffectLifeCycle
    { 
        public Vector3 Origin;
        public Vector3 Target;
        public float Size;
        public float Speed;
        public float CurrentTime;
        public float ExpectationTime;
        public SkillEffectFlyType SkillEffectFlyType;
        public Vector3 CurrentPosition;
        public List<SkillEventData> SkillEventData;

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

    public class SingleTargetFlyEffectSkillChecker : ISkillChecker
    {
        public CooldownHeader CooldownHeader;
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        public bool IsSkillNotInCd()
        {
            return this.IsSkillNotCd();
        }
        public SkillEffectLifeCycle GetSkillEffectLifeCycle() => SkillEffectLifeCycle;
        public void SetSkillData(SkillCheckerData skillCheckerData)
        {
            SkillEffectLifeCycle.CurrentPosition = skillCheckerData.SkillPosition.ToVector3();
            SkillEffectLifeCycle.CurrentTime = skillCheckerData.CurrentSkillTime;
            CooldownHeader = new CooldownHeader
            {
                CurrentTime = skillCheckerData.SkillCooldownTimer,
                Cooldown = skillCheckerData.SkillCooldown,
            };
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
            if (SkillEffectLifeCycle != null)
            {
                SkillEffectLifeCycle.OnDestroy += Destroy;
            }
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

        public Vector3 GetSkillEffectPosition() => SkillEffectLifeCycle.CurrentPosition;
    }
    
    //位移技能
    public class DashSkillChecker : ISkillChecker
    {
        public CooldownHeader CooldownHeader;
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        public float FlyDistance;
        public float GetFlyDistance() => FlyDistance;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;
        public bool IsSkillNotInCd()
        {
            return this.IsSkillNotCd();
        }
        public void SetSkillData(SkillCheckerData skillCheckerData)
        {
            SkillEffectLifeCycle.CurrentPosition = skillCheckerData.SkillPosition.ToVector3();
            SkillEffectLifeCycle.CurrentTime = skillCheckerData.CurrentSkillTime;
            CooldownHeader = new CooldownHeader
            {
                CurrentTime = skillCheckerData.SkillCooldownTimer,
                Cooldown = skillCheckerData.SkillCooldown,
            };
        }

        public bool IsSkillEffect()
        {
            return SkillEffectLifeCycle != null;
        }
        public Vector3 GetSkillEffectPosition() => SkillEffectLifeCycle.CurrentPosition;
        public SkillEffectLifeCycle GetSkillEffectLifeCycle() => SkillEffectLifeCycle;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;
        
        public DashSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            if (SkillEffectLifeCycle != null)
            {
                SkillEffectLifeCycle.OnDestroy += Destroy;
            }
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
    
    public struct SkillEventData
    {
        public SkillEventType SkillEventType;
        public float FireTime;

        public bool UpdateAndCheck(float currentTime)
        {
            return currentTime >= FireTime;
        }
    }
    
    public class AreaOfRangedFlySkillChecker : ISkillChecker
    {
        public CooldownHeader CooldownHeader;
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        public float FlyDistance;
        public SkillEffectLifeCycle GetSkillEffectLifeCycle() => SkillEffectLifeCycle;

        public void SetSkillData(SkillCheckerData skillCheckerData)
        {
            SkillEffectLifeCycle.CurrentPosition = skillCheckerData.SkillPosition.ToVector3();
            SkillEffectLifeCycle.CurrentTime = skillCheckerData.CurrentSkillTime;
            CooldownHeader = new CooldownHeader
            {
                CurrentTime = skillCheckerData.SkillCooldownTimer,
                Cooldown = skillCheckerData.SkillCooldown,
            };
        }
        
        public AreaOfRangedFlySkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            if (SkillEffectLifeCycle != null)
            {
                SkillEffectLifeCycle.OnDestroy += Destroy;
            }
        }

        public float GetFlyDistance() => FlyDistance;
        
        public CooldownHeader GetCooldownHeader() => CooldownHeader;
        public Vector3 GetSkillEffectPosition() => SkillEffectLifeCycle.CurrentPosition;

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
    
    public class AreaOfRangedSkillChecker : ISkillChecker
    {
        public CooldownHeader CooldownHeader;
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        public float FlyDistance;
        public SkillEffectLifeCycle GetSkillEffectLifeCycle() => SkillEffectLifeCycle;

        public void SetSkillData(SkillCheckerData skillCheckerData)
        {
            SkillEffectLifeCycle.CurrentPosition = skillCheckerData.SkillPosition.ToVector3();
            SkillEffectLifeCycle.CurrentTime = skillCheckerData.CurrentSkillTime;
            CooldownHeader = new CooldownHeader
            {
                CurrentTime = skillCheckerData.SkillCooldownTimer,
                Cooldown = skillCheckerData.SkillCooldown,
            };
        }
        public AreaOfRangedSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            if (SkillEffectLifeCycle != null)
            {
                SkillEffectLifeCycle.OnDestroy += Destroy;
            }
        }

        public float GetFlyDistance() => FlyDistance;
        public Vector3 GetSkillEffectPosition() => SkillEffectLifeCycle.CurrentPosition;
        
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
    public class AreaOfRangedDelayedSkillChecker : ISkillChecker
    {
        public CooldownHeader CooldownHeader;
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        public SkillEffectLifeCycle SkillEffectLifeCycle;
        public float FlyDistance;
        public Vector3 GetSkillEffectPosition() => SkillEffectLifeCycle.CurrentPosition;
        public SkillEffectLifeCycle GetSkillEffectLifeCycle() => SkillEffectLifeCycle;
        
        public void SetSkillData(SkillCheckerData skillCheckerData)
        {
            SkillEffectLifeCycle.CurrentPosition = skillCheckerData.SkillPosition.ToVector3();
            SkillEffectLifeCycle.CurrentTime = skillCheckerData.CurrentSkillTime;
            CooldownHeader = new CooldownHeader
            {
                CurrentTime = skillCheckerData.SkillCooldownTimer,
                Cooldown = skillCheckerData.SkillCooldown,
            };
        }
        public AreaOfRangedDelayedSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader, SkillEffectLifeCycle skillEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillEffectLifeCycle = skillEffectLifeCycle;
            if (SkillEffectLifeCycle != null)
            {
                SkillEffectLifeCycle.OnDestroy += Destroy;
            }
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
    
    public struct PropertyCalculatorData : IEquatable<PropertyCalculatorData>
    {
        public PropertyCalculator BuffCalculator;
        public PropertyCalculator TargetCalculator;
        public BuffOperationType OperationType;
        public int PlayerId;
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