using HotUpdate.Scripts.Network.State;
using MemoryPack;

namespace AOTScripts.Data.State
{
    [MemoryPackable]
    public partial class PlayerSkillState : HotUpdate.Scripts.Network.State.ISyncPropertyState
    {
        [MemoryPackOrder(0)]
        public HotUpdate.Scripts.Network.State.MemoryList<SkillCheckerData> SkillCheckerDatas;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerSkill;


        // public MemoryList<SkillCheckerData> SetSkillCheckerDatas()
        // {
        //     if (SkillCheckers == null || SkillCheckers.Count == 0)
        //     {
        //         //Debug.LogWarning("SkillCheckers is null or empty");
        //         SkillCheckers = new Dictionary<AnimationState, ISkillChecker>();
        //     }
        //     if (SkillCheckerDatas == null || SkillCheckerDatas.Count == 0)
        //     {
        //         SkillCheckerDatas = new MemoryList<SkillCheckerData>();
        //     }
        //     foreach (var animationState in SkillCheckers.Keys)
        //     {
        //         var skillCheckerData = SkillCheckers[animationState];
        //         var commonSkillData = skillCheckerData.GetCommonSkillCheckerHeader();
        //         var skillEffectData = skillCheckerData.GetSkillEffectLifeCycle();
        //         var cooldownData = skillCheckerData.GetCooldownHeader();
        //         SkillCheckerData data = new SkillCheckerData();
        //         data.AnimationState = animationState;
        //         data.AnimationState = animationState;
        //         data.SkillId = commonSkillData.ConfigId;
        //         data.MaxSkillTime = commonSkillData.ExistTime;
        //         data.SkillCooldownTimer = cooldownData.CurrentTime;
        //         data.SkillCooldown = cooldownData.Cooldown;
        //         if (skillEffectData!= null)
        //         {
        //             data.CurrentSkillTime = skillEffectData.CurrentTime;
        //             data.SkillPosition = skillEffectData.CurrentPosition;
        //         }
        //         var index = SkillCheckerDatas.FindIndex(x => x.AnimationState == animationState);
        //         if (index == -1)
        //         {
        //             SkillCheckerDatas.Add(data);
        //         }
        //         else
        //         {
        //             SkillCheckerDatas[index] = data;
        //         }
        //     }
        //     return SkillCheckerDatas;
        // }
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
        [MemoryPackOrder(6)]
        public CompressedVector3 SkillPosition; 
    }
}