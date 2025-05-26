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

        public void Execute<T>(T skillCheckerParams, params object[] args) where T : ISkillCheckerParams
        {
            SkillChecker.Execute(ref SkillChecker, skillCheckerParams,args);
        }

        public void Update(params object[] args)
        {
            
        }
    }

}