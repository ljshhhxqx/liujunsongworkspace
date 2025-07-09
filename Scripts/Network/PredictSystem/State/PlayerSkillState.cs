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
        [MemoryPackOrder(0)]
        public MemoryList<SkillCheckerData> SkillCheckerDatas;
        
        [MemoryPackIgnore]
        public Dictionary<AnimationState, ISkillChecker> SkillCheckers;
        public PlayerSyncStateType GetStateType() => PlayerSyncStateType.PlayerSkill;

        public void SetSkillCheckerState()
        {
            if (SkillCheckerDatas == null || SkillCheckerDatas.Count == 0)
            {
                //Debug.LogWarning("SkillCheckers or SkillCheckerDatas is null");
                return;
            }
            for (var i = 0; i < SkillCheckerDatas.Count; i++)
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

        public MemoryList<SkillCheckerData> SetSkillCheckerDatas()
        {
            if (SkillCheckers == null || SkillCheckers.Count == 0)
            {
                Debug.LogWarning("SkillCheckers is null or empty");
                return null;
            }
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
            SkillCheckerDatas = new MemoryList<SkillCheckerData>(skillCheckerDatas);
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