using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Skill;
using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial class PlayerSkillState : ISyncPropertyState
    {
        [MemoryPackIgnore] public Dictionary<AnimationState, ISkillChecker> SkillCheckers;
        [MemoryPackOrder(0)]
        public SkillCheckerData[] SkillCheckerDatas;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerSkill;
    }

    [MemoryPackable]
    public partial struct SkillCheckerData
    {
        [MemoryPackOrder(0)]
        public AnimationState AnimationState;
        [MemoryPackOrder(1)]
        public int SkillId;
        [MemoryPackOrder(2)]
        public float MaxSkillTime;
        [MemoryPackOrder(3)]
        public float CurrentSkillTime;
        [MemoryPackOrder(4)]
        public float SkillCooldownTimer;
        [MemoryPackOrder(5)]
        public float SkillCooldown;
    }
}