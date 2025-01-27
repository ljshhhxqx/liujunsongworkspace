using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using Sirenix.OdinInspector;
using UnityEngine;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "AnimationConfig", menuName = "ScriptableObjects/AnimationConfig")]
    public class AnimationConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<AnimationInfo> animationInfos = new List<AnimationInfo>();

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            animationInfos.Clear();
            for (int i = 2; i < textAsset.Count; i++)
            {
                var row = textAsset[i];
                var data = new AnimationInfo();
                data.state = (AnimationState) Enum.Parse(typeof(AnimationState), row[0]);
                data.cost = float.Parse(row[1]);
                data.cooldown = float.Parse(row[2]);
                data.priority = int.Parse(row[3]);
                data.animationType = (AnimationType) Enum.Parse(typeof(AnimationType), row[4]);
                data.canBeInterrupted = bool.Parse(row[5]);
                data.actionType = (ActionType) Enum.Parse(typeof(ActionType), row[6]);
                animationInfos.Add(data);
            }
        }
        
        public AnimationInfo GetAnimationInfo(AnimationState state)
        {
            foreach (var animationInfo in animationInfos)
            {
                if (animationInfo.state == state)
                {
                    return animationInfo;
                }
            }
            Debug.LogError("AnimationInfo not found for state: " + state);
            return new AnimationInfo();
        }

        public float GetPlayerAnimationCost(AnimationState state)
        {
            return GetAnimationInfo(state).cost;
        }
        
        public float GetPlayerAnimationCooldown(AnimationState state)
        {
            return GetAnimationInfo(state).cooldown;
        }
        
        public ActionType GetActionType(AnimationState state)
        {
            return GetAnimationInfo(state).actionType;
        }
    }

    [Serializable]
    public struct AnimationInfo
    {
        public AnimationState state;
        public float cost;
        public float cooldown;
        public int priority;
        public AnimationType animationType;
        public bool canBeInterrupted;
        public ActionType actionType;
    }
    
    public enum AnimationType
    {
        Continuous,  // 持续性动画（待机、移动、奔跑）
        Single,      // 一次性动画（跳跃、翻滚、受击、死亡）
        Combo        // 连击动画（攻击）
    }
}