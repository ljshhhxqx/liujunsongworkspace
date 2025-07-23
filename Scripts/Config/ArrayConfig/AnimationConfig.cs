using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.JsonConfig;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationState = HotUpdate.Scripts.Config.JsonConfig.AnimationState;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "AnimationConfig", menuName = "ScriptableObjects/AnimationConfig")]
    public class AnimationConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<AnimationInfo> animationInfos = new List<AnimationInfo>();

        public List<AnimationInfo> AnimationInfos => animationInfos;
        public Dictionary<AnimationState, AnimationInfo> AnimationInfosDictionary { get; } = new Dictionary<AnimationState, AnimationInfo>();

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
                data.animationNames = JsonConvert.DeserializeObject<string[]>(row[7]);
                data.keyframeData = JsonConvert.DeserializeObject<KeyframeData[]>(row[8]);
                data.isClearVelocity = bool.Parse(row[9]);
                data.cooldownType = (CooldownType) Enum.Parse(typeof(CooldownType), row[10]);
                data.noStrengthState = (AnimationState) Enum.Parse(typeof(AnimationState), row[11]);
                animationInfos.Add(data);
            }
        }

        public bool IsStrengthEnough(AnimationState state, float strength, out AnimationState newState, float duration = 0f)
        {
            var animationInfo = GetAnimationInfo(state);
            newState = animationInfo.noStrengthState;
            if (animationInfo.animationType == AnimationType.Continuous)
            {
                return strength * duration >= duration * animationInfo.cost;
            }
            return strength >= animationInfo.cost;
        }

        public AnimationInfo GetAnimationInfo(AnimationState state)
        {
            if (AnimationInfosDictionary.TryGetValue(state, out var info))
            {
                return info;
            }
            foreach (var animationInfo in animationInfos)
            {
                if (animationInfo.state == state)
                {
                    AnimationInfosDictionary.Add(state, animationInfo);
                    return animationInfo;
                }
            }
            Debug.LogError("AnimationInfo not found for state: " + state);
            return default;
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
        
        public string GetAnimationName(AnimationState state, int index = 0)
        {
            var animationInfo = GetAnimationInfo(state);
            if (animationInfo.animationNames == null || animationInfo.animationNames.Length <= index)
            {
                Debug.LogError("AnimationNames not found for state: " + state + " index: " + index);
                return null;
            }
            return animationInfo.animationType == AnimationType.Combo ? animationInfo.animationNames[0] : animationInfo.animationNames[index];
        }
        
        public string[] GetAnimationNames(AnimationState state)
        {
            return GetAnimationInfo(state).animationNames;
        }

        // [Button]
        // private KeyframeData[] GetKeyframes()
        // {
        //     var keyframes = new List<KeyframeData>();
        //     keyframes.Add(new KeyframeData(0.05f, "OnRollStart", false, 0f, 0.1f, true));
        //     keyframes.Add(new KeyframeData(0.5f, "OnRollEnd", false, 0f, 0.1f, true));
        //     
        //     Debug.Log(JsonConvert.SerializeObject(keyframes));
        //     return keyframes.ToArray();
        //     // foreach (var animationInfo in animationInfos)
        //     // {
        //     //     if (animationInfo.keyframeData!= null)
        //     //     {
        //     //         keyframes.AddRange(animationInfo.keyframeData);
        //     //     }
        //     // }
        //     // return keyframes.ToArray();
        // }
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
        public string[] animationNames;
        public KeyframeData[] keyframeData;
        public bool isClearVelocity;
        public CooldownType cooldownType;
        //体力不足时，转为此状态
        public AnimationState noStrengthState;
    }

    public enum ActionType
    {
        None,
        Movement,       // 移动类动作：立即响应 + 状态和解
        Interaction,    // 交互类动作：需要服务器验证
        Animation,      // 动画过渡：由状态机自动触发
    }
    
    public enum AnimationType
    {
        Continuous,  // 持续性动画（待机、移动、奔跑）
        Single,      // 一次性动画（跳跃、翻滚、受击、死亡）
        Combo        // 连击动画（攻击）
    }

    public enum CooldownType
    {
        Normal,
        KeyFrame,
        Combo,
        KeyFrameAndCombo,
        None
    }

    public enum AnimationEvent
    {
        OnRollStart,
        OnRollStop,
        OnAttack,
        OnSkillCastE,
        OnSkillCastQ,
    }

    // 关键帧数据结构
    [Serializable]
    public struct KeyframeData
    {
        [Tooltip("事件触发时间（秒）")]
        public float triggerTime;
        [Tooltip("事件类型标识符")]
        public AnimationEvent eventType;
        [Tooltip("触发后是否重置冷却")]
        public bool resetCooldown;
        [Tooltip("触发后重置冷却的窗口时间(如果为0，那么将无法产生连招效果)")]
        public float resetCooldownWindowTime;
        [Tooltip("允许触发的时间误差")]
        [Range(0f, 0.3f)]
        public float tolerance;
        [Tooltip("是否在服务器验证")]
        public bool serverValidate;
        
        public KeyframeData(float triggerTime, AnimationEvent eventType, bool resetCooldown, float resetCooldownWindowTime, float tolerance, bool serverValidate)
        {
            this.triggerTime = triggerTime;
            this.eventType = eventType;
            this.resetCooldownWindowTime = resetCooldownWindowTime;
            this.tolerance = tolerance;
            this.resetCooldown = resetCooldown;
            this.serverValidate = serverValidate;
        }
    }
}