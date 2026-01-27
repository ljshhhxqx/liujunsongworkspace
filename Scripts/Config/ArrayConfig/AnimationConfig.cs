using System;
using System.Collections.Generic;
using AOTScripts.Data;
using HotUpdate.Scripts.Config.JsonConfig;
using Newtonsoft.Json;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;
using AnimationEvent = AOTScripts.Data.AnimationEvent;
using AnimationState = AOTScripts.Data.AnimationState;

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
                data.showInHud = bool.Parse(row[12]);
                data.animationSpeed = float.Parse(row[13]);
                data.icon = row[14];
                data.frame = (QualityType) Enum.Parse(typeof(QualityType), row[15]);
                data.isOverrideCost = bool.Parse(row[16]);
                data.isOverrideCooldown = bool.Parse(row[17]);
                animationInfos.Add(data);
            }
        }

        public bool IsStrengthEnough(AnimationState state, float strength, out AnimationState newState, float duration = 0f)
        {
            var animationInfo = GetAnimationInfo(state);
            bool isStrengthEnough;
            newState = state;
            if (animationInfo.animationType == AnimationType.Continuous)
            {
                isStrengthEnough = strength * duration >= duration * animationInfo.cost;
                newState = isStrengthEnough ? newState : animationInfo.noStrengthState;
                return isStrengthEnough;
            }
            isStrengthEnough = strength >= animationInfo.cost;
            newState = isStrengthEnough ? newState : animationInfo.noStrengthState;
            return isStrengthEnough;
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
            //Debug.LogError("AnimationInfo not found for state: " + state);
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

            return animationInfo.animationType == AnimationType.Combo ? animationInfo.animationNames[index] : animationInfo.animationNames[0];
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
        public bool showInHud;
        public float animationSpeed;
        public string icon;
        public QualityType frame;
        public bool isOverrideCost;
        public bool isOverrideCooldown;
        public bool isOverridePriority;
        public bool isOverrideAnimationSpeed;
    }
}