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
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerSkill;
    }

}