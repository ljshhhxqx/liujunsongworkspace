using System;
using System.Threading;
using AOTScripts.Data;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Tool.Coroutine;
using MemoryPack;
using Newtonsoft.Json;
using UnityEngine;
using Random = UnityEngine.Random;

namespace HotUpdate.Scripts.Network.Battle
{
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(AttackChecker))]
    [MemoryPackUnion(1, typeof(AttackHitChecker))]
    [MemoryPackUnion(2, typeof(SkillCastChecker))]
    [MemoryPackUnion(3, typeof(SkillHitChecker))]
    [MemoryPackUnion(4, typeof(TakeDamageChecker))]
    [MemoryPackUnion(5, typeof(KillChecker))]
    [MemoryPackUnion(6, typeof(HpChangeChecker))]
    [MemoryPackUnion(7, typeof(MpChangeChecker))]
    [MemoryPackUnion(8, typeof(CriticalHitChecker))]
    [MemoryPackUnion(9, typeof(DodgeChecker))]
    public partial interface IConditionChecker
    {
        ConditionCheckerHeader GetConditionCheckerHeader();
        CooldownHeader GetCooldownHeader();
        CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader);
        bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters;
    }
    
    [MemoryPackable]
    public partial struct CooldownHeader
    {
        [MemoryPackOrder(0)]
        public float Cooldown;
        [MemoryPackOrder(1)]
        public float CurrentTime;
        
        public CooldownHeader(float cooldown)
        {
            Cooldown = cooldown;
            CurrentTime = 0;
        }
        
        public CooldownHeader Update(float deltaTime)
        {
            if(!IsCooldown())
                return this;
            if (CurrentTime > 0)
            {
                CurrentTime = Mathf.Max(0, CurrentTime - deltaTime);
            }

            return new CooldownHeader
            {
                Cooldown = Cooldown,
                CurrentTime = CurrentTime,
            };
        }

        public CooldownHeader Reset()
        {
            CurrentTime = Cooldown;
            return new CooldownHeader
            {
                Cooldown = Cooldown,
                CurrentTime = 0,
            };
        }

        public CooldownHeader TakeEffect(float cooldown)
        {
            if(IsCooldown())
                return this;
            Cooldown = cooldown;
            CurrentTime = cooldown;
            return new CooldownHeader
            {
                Cooldown = cooldown,
                CurrentTime = cooldown,
            };
        }
        
        public bool IsCooldown()
        {
            return CurrentTime > 0;
        }
    }

    [MemoryPackable]
    public partial struct ConditionCheckerHeader
    {
        [MemoryPackOrder(0)]
        public TriggerType TriggerType;
        [MemoryPackOrder(1)]
        public float Interval;
        [MemoryPackOrder(2)]
        public float Probability;
        [MemoryPackOrder(3)] 
        public string CheckParams;
        [MemoryPackOrder(4)]
        public ConditionTargetType TargetType;
        [MemoryPackOrder(5)]
        public int TargetCount;

        public static ConditionCheckerHeader Create(TriggerType triggerType, float interval, float probability,
            IConditionParam checkParams, ConditionTargetType targetType, int targetCount)
        {
            return new ConditionCheckerHeader
            {
                TriggerType = triggerType,
                Interval = interval,
                Probability = probability,
                CheckParams = JsonConvert.SerializeObject(checkParams),
                TargetType = targetType,
                TargetCount = targetCount
            };
        }

        public static IConditionChecker CreateChecker(ConditionCheckerHeader header)
        {
            switch (header.TriggerType)
            {
                case TriggerType.None:
                    return new NoConditionChecker { Header = header };
                case TriggerType.OnAttack:
                    return new AttackChecker { Header = header,  };
                case TriggerType.OnAttackHit:
                    return new AttackHitChecker { Header = header };
                case TriggerType.OnSkillCast:
                    return new SkillCastChecker { Header = header };
                case TriggerType.OnSkillHit:
                    return new SkillHitChecker { Header = header };
                case TriggerType.OnTakeDamage:
                    return new TakeDamageChecker { Header = header };
                case TriggerType.OnKill:
                    return new KillChecker { Header = header };
                case TriggerType.OnHpChange:
                    return new HpChangeChecker { Header = header };
                case TriggerType.OnManaChange:
                    return new MpChangeChecker { Header = header };
                case TriggerType.OnCriticalHit:
                    return new CriticalHitChecker { Header = header };
                case TriggerType.OnDodge:
                    return new DodgeChecker { Header = header };
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public struct CurrentConditionCommonParameters
    {
        public float Probability;
        public TriggerType TriggerType;

        public static CurrentConditionCommonParameters CreateParameters(TriggerType triggerType, float probability)
        {
            return new CurrentConditionCommonParameters
            {
                Probability = probability,
                TriggerType = triggerType
            };
        }
    }

    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(AttackCheckerParameters))]
    [MemoryPackUnion(1, typeof(AttackHitCheckerParameters))]
    [MemoryPackUnion(2, typeof(SkillCastCheckerParameters))]
    [MemoryPackUnion(3, typeof(SkillHitCheckerParameters))]
    [MemoryPackUnion(4, typeof(TakeDamageCheckerParameters))]
    [MemoryPackUnion(5, typeof(KillCheckerParameters))]
    [MemoryPackUnion(6, typeof(HpChangeCheckerParameters))]
    [MemoryPackUnion(7, typeof(MpChangeCheckerParameters))]
    [MemoryPackUnion(8, typeof(CriticalHitCheckerParameters))]
    [MemoryPackUnion(9, typeof(DodgeCheckerParameters))]
    [MemoryPackUnion(10, typeof(DeathCheckerParameters))]
    [MemoryPackUnion(11, typeof(MoveCheckerParameters))]
    public partial interface IConditionCheckerParameters
    {
        CurrentConditionCommonParameters GetCommonParameters();
    }

    [MemoryPackable]
    public partial struct MoveCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]
        public float MoveSpeed;
        [MemoryPackOrder(2)]
        public float MoveDistance;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;

        public static MoveCheckerParameters CreateParameters(TriggerType triggerType, float moveSpeed,
            float moveDistance)
        {
            var probability = Random.Range(0f, 100f);
            return new MoveCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                MoveSpeed = moveSpeed,
                MoveDistance = moveDistance
            };
        }
    }

    [MemoryPackable]
    public partial struct DeathCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;

        public static DeathCheckerParameters CreateParameters(TriggerType triggerType)
        {
            var probability = Random.Range(0f, 100f);
            return new DeathCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability)
            };
        }
    }

    [MemoryPackable]
    public partial struct AttackCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]
        public AttackRangeType AttackRangeType;
        [MemoryPackOrder(2)]
        public float Attack;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;

        public static AttackCheckerParameters CreateParameters(TriggerType triggerType, 
            AttackRangeType attackRangeType, float attack)
        {
            var probability = Random.Range(0f, 100f);
            return new AttackCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                AttackRangeType = attackRangeType,
                Attack = attack
            };
        }
    }

    [MemoryPackable]
    public partial struct AttackHitCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]
        public float HpRatio;
        [MemoryPackOrder(2)]
        public float Damage;
        [MemoryPackOrder(3)]
        public AttackRangeType AttackRangeType;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        public static AttackHitCheckerParameters CreateParameters(TriggerType triggerType, 
            float hpRatio, float damage, AttackRangeType attackRangeType)
        {
            var probability = Random.Range(0f, 100f);
            return new AttackHitCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                HpRatio = hpRatio,
                Damage = damage,
                AttackRangeType = attackRangeType
            };
        }
    }

    [MemoryPackable]
    public partial struct SkillCastCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]
        public float MpRatio;
        [MemoryPackOrder(2)]
        public SkillBaseType SkillBaseType;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        public static SkillCastCheckerParameters CreateParameters(TriggerType triggerType, 
            float mpRatio, SkillBaseType skillBaseType)
        {
            var probability = Random.Range(0f, 100f);
            return new SkillCastCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                MpRatio = mpRatio,
                SkillBaseType = skillBaseType
            };
        }
    }
    
    [MemoryPackable]
    public partial struct SkillHitCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        public float DamageRatio;
        public float MpRatio;
        public SkillBaseType SkillBaseType;
        public float HpRatio;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        
        public static SkillHitCheckerParameters CreateParameters(TriggerType triggerType, 
            float damageRatio, float mpRatio, SkillBaseType skillBaseType, float hpRatio)
        {
            
            var probability = Random.Range(0f, 100f);
            return new SkillHitCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                DamageRatio = damageRatio,
                MpRatio = mpRatio,
                SkillBaseType = skillBaseType,
                HpRatio = hpRatio
            };
        }
    }

    [MemoryPackable]
    public partial struct TakeDamageCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]
        public DamageType DamageType;
        //剩余HP占比
        [MemoryPackOrder(2)]
        public float HpRatio;
        //受伤占最大生命值比例
        [MemoryPackOrder(3)]
        public float DamageRatio;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        
        public static TakeDamageCheckerParameters CreateParameters(TriggerType triggerType, 
            DamageType damageType, float hpRatio, float damageRatio)
        {
            var probability = Random.Range(0f, 100f);
            return new TakeDamageCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                DamageType = damageType,
                HpRatio = hpRatio,
                DamageRatio = damageRatio
            };
        }
    }

    [MemoryPackable]
    public partial struct KillCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        public static KillCheckerParameters CreateParameters(TriggerType triggerType)
        {
            var probability = Random.Range(0f, 100f);
            return new KillCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability)
            };
        }
    }

    [MemoryPackable]
    public partial struct HpChangeCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]
        public float HpRatio;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        public static HpChangeCheckerParameters CreateParameters(TriggerType triggerType, float hpRatio)
        {
            
            var probability = Random.Range(0f, 100f);
            return new HpChangeCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                HpRatio = hpRatio
            };
        }
    }

    [MemoryPackable]
    public partial struct MpChangeCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]

        public float MpRatio;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        public static MpChangeCheckerParameters CreateParameters(TriggerType triggerType, float mpRatio)
        {
            var probability = Random.Range(0f, 100f);
            return new MpChangeCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                MpRatio = mpRatio
            };
        }
    }

    [MemoryPackable]
    public partial struct CriticalHitCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        [MemoryPackOrder(1)]
        public float DamageRatio;
        [MemoryPackOrder(2)]
        public float HpRatio;
        [MemoryPackOrder(3)]
        public DamageType DamageType;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        public static CriticalHitCheckerParameters CreateParameters(TriggerType triggerType, 
            float damageRatio, float hpRatio, DamageType damageType)
        {
            var probability = Random.Range(0f, 100f);
            return new CriticalHitCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability),
                DamageRatio = damageRatio,
                HpRatio = hpRatio,
                DamageType = damageType
            };
        }
    }

    [MemoryPackable]
    public partial struct DodgeCheckerParameters : IConditionCheckerParameters
    {
        [MemoryPackOrder(0)]
        public CurrentConditionCommonParameters CommonParameters;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
        public static DodgeCheckerParameters CreateParameters(TriggerType triggerType)
        {
            var probability = Random.Range(0f, 100f);
            return new DodgeCheckerParameters
            {
                CommonParameters = CurrentConditionCommonParameters.CreateParameters(triggerType, probability)
            };
        }
    }
    
    [MemoryPackable]
    public partial struct NoConditionChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }

        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            return true;
        }
    }

    [MemoryPackable]
    public partial struct AttackChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }

        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is AttackCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is AttackConditionParam attackConditionParam)
                {
                    return attackConditionParam.CheckConditionValid() && this.CheckCommonParamsCondition(t) &&
                           attackConditionParam.attackRangeType == parameters.AttackRangeType &&
                           Mathf.Approximately(attackConditionParam.attack, parameters.Attack);
                }
            }

            return false;
        }
    }

    [MemoryPackable]
    public partial struct AttackHitChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is AttackHitCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is AttackHitConditionParam attackHitConditionParam)
                {
                    return attackHitConditionParam.CheckConditionValid() && this.CheckCommonParamsCondition(t) && attackHitConditionParam.hpRange.IsInRange(parameters.HpRatio) && 
                           attackHitConditionParam.damageRange.IsInRange(parameters.Damage) && attackHitConditionParam.attackRangeType == parameters.AttackRangeType;
                }
            }
            return false;
        }
    }
    
    [MemoryPackable]
    public partial struct SkillCastChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is SkillCastCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is SkillCastConditionParam skillCastConditionParam)
                {
                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t) && skillCastConditionParam.skillBaseType == parameters.SkillBaseType && 
                           skillCastConditionParam.mpRange.IsInRange(parameters.MpRatio);
                }
            }
            return false;
        }
        
    }
    
    [MemoryPackable]
    public partial struct SkillHitChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;

        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is SkillHitCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is SkillHitConditionParam skillHitConditionParam)
                {
                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t) && skillHitConditionParam.skillBaseType == parameters.SkillBaseType && 
                           skillHitConditionParam.mpRange.IsInRange(parameters.MpRatio) && skillHitConditionParam.hpRange.IsInRange(parameters.HpRatio)
                           && skillHitConditionParam.damageRange.IsInRange(parameters.DamageRatio);
                }
            }
            return false;
        }
        
    }
    
    [MemoryPackable]
    public partial struct TakeDamageChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is TakeDamageCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is TakeDamageConditionParam takeDamageConditionParam)
                {
                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t) && takeDamageConditionParam.damageType == parameters.DamageType
                           && takeDamageConditionParam.hpRange.IsInRange(parameters.HpRatio)
                           && takeDamageConditionParam.damageRange.IsInRange(parameters.DamageRatio);
                }
            }
            return false;
        }
    }
    
    [MemoryPackable]
    public partial struct KillChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        [MemoryPackOrder(2)]
        public int CurrentKillCount;
        [MemoryPackOrder(3)]
        public bool IsNotInWindow;
        [MemoryPackIgnore]
        private CancellationTokenSource _cancellationTokenSource;
        
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is KillCheckerParameters && checker is KillChecker killChecker && !killChecker.IsNotInWindow)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is KillConditionParam killConditionParam)
                {
                    _cancellationTokenSource ??= new CancellationTokenSource();
                    killChecker.CurrentKillCount += 1;
                    killChecker.IsNotInWindow = true;
                    DelayInvoker.DelayInvoke(killConditionParam.timeWindow, () =>
                    {
                        killChecker.IsNotInWindow = false;
                    }, null, 0.1f, _cancellationTokenSource.Token);

                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t) && killConditionParam.targetCount == killChecker.CurrentKillCount;
                }
            }
            return false;
        }

        public void Reset(ref IConditionChecker checker)
        {
            if (checker is KillChecker killChecker)
            {
                killChecker.CurrentKillCount = 0;
                killChecker.IsNotInWindow = false;
                killChecker._cancellationTokenSource?.Cancel();
                killChecker._cancellationTokenSource = null;
            }
        }
    }
    
    [MemoryPackable]
    public partial struct HpChangeChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is HpChangeCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is HpChangeConditionParam hpChangeConditionParam)
                {
                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t) && hpChangeConditionParam.hpRange.IsInRange(parameters.HpRatio);
                }
            }
            return false;
        }
    }

    [MemoryPackable]
    public partial struct MpChangeChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is MpChangeCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is MpChangeConditionParam mpChangeConditionParam)
                {
                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t)  && mpChangeConditionParam.mpRange.IsInRange(parameters.MpRatio);
                }
            }
            return false;
        }
    }
    
    [MemoryPackable]
    public partial struct CriticalHitChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is CriticalHitCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is CriticalHitConditionParam criticalHitConditionParam)
                {
                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t) && criticalHitConditionParam.hpRange.IsInRange(parameters.HpRatio) 
                           && criticalHitConditionParam.damageType == parameters.DamageType 
                           && criticalHitConditionParam.damageRange.IsInRange(parameters.DamageRatio);
                }
            }
            return false;
        }
    }
    
    [MemoryPackable]
    public partial struct DodgeChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        
        [MemoryPackOrder(1)]
        public CooldownHeader CooldownHeader;

        public CooldownHeader GetCooldownHeader() => CooldownHeader;

        public CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader)
        {
            CooldownHeader = cooldownHeader;
            return CooldownHeader;
        }
        
        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;
        
        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is DodgeCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is  DodgeConditionParam dodgeConditionParam)
                {
                    return configData.CheckConditionValid() && this.CheckCommonParamsCondition(t);
                }
            }
            return false;
        }
    }

    public static class ConditionExtensions
    {
        public static bool CheckCommonParamsCondition(this IConditionChecker conditionChecker, IConditionCheckerParameters parameters)
        {
            var checkerHeader = conditionChecker.GetConditionCheckerHeader();
            //var configData = JsonConvert.DeserializeObject<IConditionParam>(checkerHeader.CheckParams);
            var commonParams = parameters.GetCommonParameters();
            return commonParams.Probability >= checkerHeader.Probability && commonParams.TriggerType == checkerHeader.TriggerType;
        }
    }
}