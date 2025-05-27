using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Skill;
using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial class PlayerSkillState : ISyncPropertyState
    {
        [MemoryPackOrder(0)] public CooldownHeader CooldownHeader;
        [MemoryPackIgnore] public ISkillChecker SkillChecker;

        public void Execute(SkillCheckerParams skillCheckerParams, params object[] args)
        {
            SkillChecker.Execute(ref SkillChecker, skillCheckerParams,args);
        }

        public void Update(params object[] args)
        {
            
        }
    }

}