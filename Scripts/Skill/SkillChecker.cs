using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Battle;
using MemoryPack;

namespace HotUpdate.Scripts.Skill
{
    [MemoryPackable(GenerateType.NoGenerate)]
    public partial interface ISkillChecker
    {
        CooldownHeader GetCooldownHeader();
        CooldownHeader SetCooldownHeader(CooldownHeader cooldownHeader);
        SkillConfigData GetSkillConfigData();
        SkillConfigData SetSkillConfigData(SkillConfigData skillConfigData);
        bool Check<T>(ref ISkillChecker checker, T t) where T : ISkillCheckerParams;
        bool Execute<T>(ref ISkillCheckerParams skillCheckerParams) where T : ISkillCheckerParams;
    }


    [MemoryPackable(GenerateType.NoGenerate)]
    public partial interface ISkillCheckerParams
    {
        
    }
}