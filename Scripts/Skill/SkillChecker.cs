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
    [MemoryPackUnion(0, typeof(SingleTargetContinuousDamageSkillChecker))]
    [MemoryPackUnion(1, typeof(SingleTargetDamageSkillChecker))]
    [MemoryPackUnion(2, typeof(AreaOfRangedSkillChecker))]
    [MemoryPackUnion(3, typeof(DashSkillChecker))]
    [MemoryPackUnion(4, typeof(AreaOfRangedFlySkillChecker))]
    public partial interface ISkillChecker
    {
        CooldownHeader GetCooldownHeader();
        CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader);
        CommonSkillCheckerHeader GetCommonSkillCheckerHeader();
        bool CheckExecute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams);
        bool Execute(ref ISkillChecker checker, SkillCheckerParams skillCheckerParams, params object[] args);
        void Destroy();
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
        [MemoryPackOrder(9)] public PropertyTypeEnum EffectPropertyType;
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
    
    [MemoryPackable]
    public partial class SkillPropertyLifeCycle
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
        [MemoryPackOrder(6)] 
        public PropertyTypeEnum TargetPropertyType;

        //MemoryPackConstructor]
        public SkillPropertyLifeCycle(float baseValue, float extraRatio, float cooldown, float effectInterval, 
            PropertyTypeEnum buffPropertyType, PropertyTypeEnum targetPropertyType, float currentTime = 0)
        {
            BaseValue = baseValue;
            ExtraRatio = extraRatio;
            Cooldown = cooldown;
            CurrentTime = currentTime;
            EffectInterval = effectInterval;
            BuffPropertyType = buffPropertyType;
            TargetPropertyType = targetPropertyType;
        }
        
        public bool IsCooldown()
        {
            return CurrentTime > 0;
        }
        
        public bool UpdateProperty(BuffOperationType buffOperationType, PropertyCalculator buffCalculator, ref PropertyCalculator targetCalculator, 
            out float damage)
        {
            damage = 0;
            if (!IsCooldown() || EffectInterval == 0)
            {
                return false;
            }
            if (buffCalculator.PropertyType != BuffPropertyType || targetCalculator.PropertyType != TargetPropertyType)
            {
                Debug.LogError("BuffPropertyType or TargetPropertyType is not match");
                return false;
            }
            TakeEffect(this, buffOperationType, buffCalculator, ref targetCalculator, out damage);
            return true;
        }

        private static void TakeEffect(SkillPropertyLifeCycle skillPropertyLifeCycle, BuffOperationType buffOperationType, PropertyCalculator buffCalculator, ref PropertyCalculator targetCalculator, 
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
        }

        public void Execute(BuffOperationType buffOperationType, PropertyCalculator buffCalculator, ref PropertyCalculator targetCalculator, 
            out float damage)
        {
            damage = 0;
            if (!IsCooldown())
            {
                return;
            }
            
            if (buffCalculator.PropertyType != BuffPropertyType || targetCalculator.PropertyType != TargetPropertyType)
            {
                Debug.LogError("BuffPropertyType or TargetPropertyType is not match");
                return;
            }
            TakeEffect(this, buffOperationType, buffCalculator, ref targetCalculator, out damage);
        }
    }

    //飞行技能、命中后造成单体持续伤害的技能
    [MemoryPackable]
    public partial class SingleTargetContinuousDamageSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
        [MemoryPackOrder(2)] 
        public SkillPropertyLifeCycle SkillPropertyLifeCycle;
        [MemoryPackOrder(3)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;

        [MemoryPackIgnore] public CancellationToken Token;
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

        public SingleTargetContinuousDamageSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,
            SkillPropertyLifeCycle skillPropertyLifeCycle, SkillFlyEffectLifeCycle skillFlyEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillPropertyLifeCycle = skillPropertyLifeCycle;
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
            SkillPropertyLifeCycle = null;
            SkillFlyEffectLifeCycle = null;
        }

        //释放、飞行、命中后造成伤害
        public PropertyCalculatorData UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc, Func<int, PropertyCalculatorData> getPropertyCalculatorDataFunc)
        {
            var hitPlayer = SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
            if (hitPlayer.Length == 0)
            {
                return null;
            }
            return getPropertyCalculatorDataFunc(hitPlayer[0]);
        }

        public bool UpdateDamage(PropertyCalculatorData propertyCalculatorData, out float damage)
        {
            return SkillPropertyLifeCycle.UpdateProperty(propertyCalculatorData.OperationType, propertyCalculatorData.BuffCalculator, ref propertyCalculatorData.TargetCalculator, out damage);
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
        public SkillPropertyLifeCycle SkillPropertyLifeCycle;
        [MemoryPackOrder(3)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;
        
        public SingleTargetDamageSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,
            SkillPropertyLifeCycle skillPropertyLifeCycle, SkillFlyEffectLifeCycle skillFlyEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillPropertyLifeCycle = skillPropertyLifeCycle;
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
            SkillPropertyLifeCycle = null;
            SkillFlyEffectLifeCycle = null;
        }

        //释放、飞行、命中后造成伤害
        public PropertyCalculatorData UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc, Func<int, PropertyCalculatorData> getPropertyCalculatorDataFunc)
        {
            var hitPlayer = SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
            var propertyCalculatorData = getPropertyCalculatorDataFunc(hitPlayer[0]);
            if (SkillPropertyLifeCycle.UpdateProperty(propertyCalculatorData.OperationType, propertyCalculatorData.BuffCalculator, ref propertyCalculatorData.TargetCalculator, out var damage))
            {
                return propertyCalculatorData;
            }
            return null;
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
        public SkillPropertyLifeCycle SkillPropertyLifeCycle;
        [MemoryPackOrder(3)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CommonSkillCheckerHeader GetCommonSkillCheckerHeader() => CommonSkillCheckerHeader;
        
        public DashSkillChecker(CooldownHeader cooldownHeader, CommonSkillCheckerHeader commonSkillCheckerHeader,
            SkillPropertyLifeCycle skillPropertyLifeCycle, SkillFlyEffectLifeCycle skillFlyEffectLifeCycle)
        {
            CooldownHeader = cooldownHeader;
            CommonSkillCheckerHeader = commonSkillCheckerHeader;
            SkillPropertyLifeCycle = skillPropertyLifeCycle;
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
            SkillPropertyLifeCycle = null;
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
        public SkillPropertyLifeCycle SkillPropertyLifeCycle;
        [MemoryPackOrder(3)] 
        public SkillFlyEffectLifeCycle SkillFlyEffectLifeCycle;
        
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
            SkillPropertyLifeCycle = null;
            SkillFlyEffectLifeCycle = null;
        }
        //释放、飞行、命中后造成伤害
        public List<PropertyCalculatorData> UpdateFly(float deltaTime, Func<Vector3, IColliderConfig, int[]> isHitFunc, Func<int, PropertyCalculatorData> getPropertyCalculatorDataFunc)
        {
            var hitPlayer = SkillFlyEffectLifeCycle.Update(deltaTime, isHitFunc);
            if (hitPlayer == null || hitPlayer.Length == 0)
            {
                return null;
            }

            var propertyCalculatorData = new List<PropertyCalculatorData>();
            for (int i = 0; i < hitPlayer.Length; i++)
            {
                var calculatorData = getPropertyCalculatorDataFunc(i);
                if (SkillPropertyLifeCycle.UpdateProperty(calculatorData.OperationType, calculatorData.BuffCalculator, ref calculatorData.TargetCalculator, out var damage))
                {
                    propertyCalculatorData.Add(calculatorData);
                }
            }
            
            return propertyCalculatorData;
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
        public SkillPropertyLifeCycle SkillPropertyLifeCycle;
        [MemoryPackOrder(3)] 
        public SkillContinuousLifeCycle SkillFlyEffectLifeCycle;
        
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
            if (CheckExecute(ref checker, skillCheckerParams))
            {
                int[] hitPlayers = null;
                foreach (var arg in args)
                {
                    if (arg is Func<Vector3, IColliderConfig, int[]> isHitFunc)
                    {
                        var colliderConfig = GamePhysicsSystem.CreateColliderConfig(ColliderType.Sphere, Vector3.zero,
                            Vector3.zero, skillCheckerParams.Radius);
                        hitPlayers = isHitFunc(skillCheckerParams.TargetPosition, colliderConfig);
                    }

                    if (arg is Func<int, PropertyCalculatorData> getPropertyCalculatorDataFunc && hitPlayers != null && hitPlayers.Length > 0)
                    {
                        foreach (var hitPlayer in hitPlayers)
                        {
                            var propertyCalculatorData = getPropertyCalculatorDataFunc(hitPlayer);
                            SkillPropertyLifeCycle.UpdateProperty(propertyCalculatorData.OperationType, propertyCalculatorData.BuffCalculator, ref propertyCalculatorData.TargetCalculator, out var damage);
                        }
                    }
                }
                return true;
            }
            return false;
        }

        public void Destroy()
        {
            SkillPropertyLifeCycle = null;
            SkillFlyEffectLifeCycle = null;
        }
    }
    
    [MemoryPackable]
    public partial class PropertyCalculatorData
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
    }
}