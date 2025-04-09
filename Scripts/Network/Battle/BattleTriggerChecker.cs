using System;
using System.Threading;
using AOTScripts.Data;
using Cysharp.Threading.Tasks;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Tool.Coroutine;
using MemoryPack;
using Newtonsoft.Json;
using Tool.Coroutine;
using UnityEngine;

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
        float GetCdTime();
        float SetCdTime(float cdTime);
        ConditionCheckerHeader GetConditionCheckerHeader();
        bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters;

        public static void UpdateCd(ref IConditionChecker checker, float deltaTime)
        {
            var cdTime = checker.GetCdTime();
            if (cdTime > 0)
            {
                cdTime = checker.SetCdTime(Mathf.Max(0, cdTime - deltaTime));
                if (cdTime <= 0)
                {
                    checker.SetCdTime(0);
                }
            }
        }

        public static void TakeEffect(ref IConditionChecker checker)
        {
            var header = checker.GetConditionCheckerHeader();
            checker.SetCdTime(header.Interval);
        }

        public static IConditionChecker CreateChecker(ConditionCheckerHeader header)
        {
            switch (header.TriggerType)
            {
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
    }

    public struct CurrentConditionCommonParameters
    {
        public float Probability;
        public ConditionTargetType ConditionTargetType;
        public int TargetCount;
        public TriggerType TriggerType;
    }

    public interface IConditionCheckerParameters
    {
        CurrentConditionCommonParameters GetCommonParameters();
    }

    public struct AttackCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;
        public AttackRangeType AttackRangeType;
        public float Attack;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct AttackHitCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;
        public float HpRatio;
        public float Damage;
        public AttackRangeType AttackRangeType;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct SkillCastCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;
        public float MpRatio;
        public SkillType SkillType;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }
    
    public struct SkillHitCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;
        public float DamageRatio;
        public float MpRatio;
        public SkillType SkillType;
        public float HpRatio;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct TakeDamageCheckerParameters : IConditionCheckerParameters
    {
        
        public CurrentConditionCommonParameters CommonParameters;
        public DamageType DamageType;
        public float HpRatio;
        public float DamageRatio;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct KillCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct HpChangeCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;
        public float HpRatio;

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct MpChangeCheckerParameters : IConditionCheckerParameters
    {
        
        public CurrentConditionCommonParameters CommonParameters;
        public float MpRatio { get; set; }

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct CriticalHitCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;

        public float DamageRatio { get; set; }
        public float HpRatio { get; set; }
        public DamageType DamageType { get; set; }

        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    public struct DodgeCheckerParameters : IConditionCheckerParameters
    {
        public CurrentConditionCommonParameters CommonParameters;
        public CurrentConditionCommonParameters GetCommonParameters() => CommonParameters;
    }

    [MemoryPackable]
    public partial struct AttackChecker : IConditionChecker
    {
        [MemoryPackOrder(0)]
        public ConditionCheckerHeader Header;
        [MemoryPackOrder(1)]
        private float _cdTime;
        public float GetCdTime() => _cdTime;

        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
        }

        public ConditionCheckerHeader GetConditionCheckerHeader() => Header;

        public bool Check<T>(ref IConditionChecker checker, T t) where T : IConditionCheckerParameters
        {
            if (t is AttackCheckerParameters parameters)
            {
                var header = GetConditionCheckerHeader();
                var configData = JsonConvert.DeserializeObject<IConditionParam>(header.CheckParams);
                if (configData != null && configData is AttackConditionParam attackConditionParam)
                {
                    return this.CheckCommonParamsCondition(t) &&
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t) && attackHitConditionParam.hpRange.IsInRange(parameters.HpRatio) && 
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t) && skillCastConditionParam.skillType == parameters.SkillType && 
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t) && skillHitConditionParam.skillType == parameters.SkillType && 
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t) && takeDamageConditionParam.damageType == parameters.DamageType
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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

                    return this.CheckCommonParamsCondition(t) && killConditionParam.targetCount == killChecker.CurrentKillCount;
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t) && hpChangeConditionParam.hpRange.IsInRange(parameters.HpRatio);
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t)  && mpChangeConditionParam.mpRange.IsInRange(parameters.MpRatio);
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t) && criticalHitConditionParam.hpRange.IsInRange(parameters.HpRatio) 
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
        private float _cdTime;
        public float GetCdTime() => _cdTime;
        public float SetCdTime(float cdTime)
        {
            _cdTime = cdTime;
            return _cdTime;
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
                    return this.CheckCommonParamsCondition(t);
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
            var configData = JsonConvert.DeserializeObject<IConditionParam>(checkerHeader.CheckParams);
            var commonParams = parameters.GetCommonParameters();
            return commonParams.Probability >= checkerHeader.Probability && commonParams.TargetCount >= checkerHeader.TargetCount &&
                   commonParams.ConditionTargetType.HasAllStates(checkerHeader.TargetType);
        }
    }
}