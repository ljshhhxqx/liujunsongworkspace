using System.Collections.Generic;
using HotUpdate.Scripts.Network.Battle;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Skill;
using MemoryPack;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial class PlayerSkillState : ISyncPropertyState
    {
        [MemoryPackIgnore] public Dictionary<AnimationState, ISkillChecker> SkillCheckers;
        [MemoryPackOrder(0)]
        public SkillCheckerData[] SkillCheckerDatas;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerSkill;

        public void SetSkillCheckerState()
        {
            for (var i = 0; i < SkillCheckerDatas.Length; i++)
            {
                var skillCheckerData = SkillCheckerDatas[i];
                if (!SkillCheckers.TryGetValue(skillCheckerData.AnimationState, out var skillChecker))
                {
                    Debug.LogError($"SkillChecker {skillCheckerData.AnimationState} not found");
                    continue;
                }
                skillChecker.SetSkillData(skillCheckerData);
            }
        }

        public SkillCheckerData[] SetSkillCheckerDatas()
        {
            var skillCheckerDatas = new SkillCheckerData[SkillCheckers.Count];
            var i = 0;
            foreach (var animationState in SkillCheckers.Keys)
            {
                var skillCheckerData = SkillCheckers[animationState];
                var commonSkillData = skillCheckerData.GetCommonSkillCheckerHeader();
                var skillEffectData = skillCheckerData.GetSkillEffectLifeCycle();
                var cooldownData = skillCheckerData.GetCooldownHeader();
                skillCheckerDatas[i] = new SkillCheckerData
                {
                    AnimationState = animationState,
                    SkillId = commonSkillData.ConfigId,
                    MaxSkillTime = commonSkillData.ExistTime,
                    CurrentSkillTime = skillEffectData.CurrentTime,
                    SkillCooldownTimer = cooldownData.CurrentTime,
                    SkillCooldown = cooldownData.Cooldown,
                    SkillPosition = skillEffectData.CurrentPosition
                };
                i++;
            }
            SkillCheckerDatas = skillCheckerDatas;
            return SkillCheckerDatas;
        }
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