using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Skill;
using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial class PlayerSkillState : ISyncPropertyState
    {
        [MemoryPackOrder(0)] public CooldownHeader CooldownHeader;
        [MemoryPackOrder(1)] public int CurrentSkillConfigId;
        [MemoryPackIgnore] public ISkillChecker SkillChecker;
    }

}