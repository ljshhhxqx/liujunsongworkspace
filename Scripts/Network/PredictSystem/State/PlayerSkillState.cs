using System.Collections.Generic;
using HotUpdate.Scripts.Skill;
using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial class PlayerSkillState : ISyncPropertyState
    {
        [MemoryPackIgnore] public Dictionary<string, ISkillChecker> SkillCheckers;
    }

}