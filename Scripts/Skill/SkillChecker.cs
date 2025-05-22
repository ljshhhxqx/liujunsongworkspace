using System;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Skill
{
    [MemoryPackable(GenerateType.NoGenerate)]
    public partial interface ISkillChecker
    {
        CooldownHeader GetCooldownHeader();
        CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader);
        CommonSkillCheckerHeader GetCommonSkillCheckerHeader();
        bool Check<T>(ref ISkillChecker checker, T t) where T : ISkillCheckerParams;
        bool Execute<T>(ref ISkillChecker checker, T t, Action onHit, Action onMiss) where T : ISkillCheckerParams;
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
    }

    [MemoryPackable]
    public partial struct CommonSkillCheckerParams
    {
        
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
    public partial struct SingleTargetContinuousDamageSkillChecker : ISkillChecker
    {
        [MemoryPackOrder(0)]
        public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)]
        public CommonSkillCheckerHeader CommonSkillCheckerHeader;
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
            return false;
        }

        public bool Execute<T>(ref ISkillChecker checker, T t, Action onHit, Action onMiss) where T : ISkillCheckerParams
        {
            return false;
        }
    }
}