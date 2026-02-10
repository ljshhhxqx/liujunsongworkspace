using System.Linq;
using AOTScripts.Data;
using AOTScripts.Tool;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using UnityEngine;
using ElementGaugeData = AOTScripts.Data.ElementGaugeData;
using ElementState = AOTScripts.Data.ElementState;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerElementCalculator : IPlayerStateCalculator
    {
        public static PlayerElementComponent PlayerElementComponent;
        
        public static void SetPlayerElementComponent(ElementAffinityConfig affinityConfig, TransitionLevelBaseDamageConfig transitionLevelBaseDamageConfig,
            ElementConfig elementConfig)
        {
            PlayerElementComponent = new PlayerElementComponent();
            PlayerElementComponent.AffinityConfig = affinityConfig;
            PlayerElementComponent.TransitionLevelBaseDamageConfig = transitionLevelBaseDamageConfig;
            PlayerElementComponent.ElementConfig = elementConfig;
        }

        public static void UpdatePlayerElementUnits(ref ElementState element, float deltaTime)
        {
            var mainElement = element.MainGaugeData;
            var subElement = element.SubGaugeData;
            if (mainElement.ElementType != ElementType.None)
            {
                mainElement.GaugeUnits = Mathf.Max(0, mainElement.GaugeUnits - mainElement.DecayRate * deltaTime);
                if (mainElement.GaugeUnits <= 0)
                {
                    mainElement.GaugeUnits = default;
                }
            }
            if (subElement.ElementType != ElementType.None)
            {
                subElement.GaugeUnits = Mathf.Max(0, subElement.GaugeUnits - subElement.DecayRate * deltaTime);
                if (subElement.GaugeUnits <= 0)
                {
                    subElement.GaugeUnits = default;
                }
            }
            element.MainGaugeData = mainElement;
            element.SubGaugeData = subElement;
        }

        public static ElementAffinityData AddOrUpdateElement(ref ElementState element, ElementGaugeData newElement, bool isPlayerAction) 
        {
            if (newElement.ElementType == element.MainElementType)
            {
                return default;
            }

            // 玩家主动释放的元素量衰减20%
            if (isPlayerAction) 
            {
                newElement.GaugeUnits *= PlayerElementComponent.JsonDataConfig.DamageData.playerGaugeUnitRatio; 
            }
            var activeStates = element.DoubleType.GetActiveStates();
            
            //元素共存态时(并且新的元素类型不等于当前元素共存态的所有类型)
            if (element.DoubleType != ElementType.None && activeStates.Count() > 1)
            {
                if (newElement.ElementType != element.MainElementType && newElement.ElementType != element.SubGaugeData.ElementType)
                {
                    var reaction = TryTriggerReaction(ref element, newElement);
                    return reaction;
                }

                if (newElement.ElementType == element.SubGaugeData.ElementType ||
                    newElement.ElementType == element.SubGaugeData.ElementType)
                {
                    UpdateElementAttachment(ref element, newElement);
                    return default;
                }
            }

            //只有一个主元素
            if (element.HasMainElement && element.MainGaugeData.ElementType != newElement.ElementType)
            {
                var reaction = TryTriggerReaction(ref element, newElement);
                return reaction;
            }

            // 无反应时更新元素附着
            UpdateElementAttachment(ref element, newElement);
            return default;
        }

        // 更新元素附着
        private static void UpdateElementAttachment(ref ElementState state, ElementGaugeData newElement)
        {
            var max = Mathf.Max(state.MainGaugeData.GaugeUnits, newElement.GaugeUnits);
            var newMainGauge = state.MainGaugeData;
            newMainGauge.GaugeUnits = max;
            state.MainGaugeData = newMainGauge;
        }

        // 触发反应
        public static ElementAffinityData TryTriggerReaction(ref ElementState state, ElementGaugeData newElement)
        {
            ElementAffinityData affinityData;
            if (state.DoubleType != ElementType.None)
            {
                affinityData = PlayerElementComponent.AffinityConfig.GetElementAffinityData(state.DoubleType, newElement.ElementType);
            }
            else
            {
                affinityData = PlayerElementComponent.AffinityConfig.GetElementAffinityData(state.MainGaugeData.ElementType, newElement.ElementType);
            }
            if (affinityData.elementReactionType == ElementReactionType.None)
            {
                return affinityData;
            }
            if (affinityData.reactionConsumeElement.HasAnyState(state.MainGaugeData.ElementType))
            {
                // 计算元素消耗
                var targetConsumed = state.MainGaugeData.GaugeUnits * affinityData.consumeCount;
                var mainGauge = state.MainGaugeData;
                mainGauge.GaugeUnits -= targetConsumed;
                state.MainGaugeData = mainGauge;
            }
            else if (affinityData.reactionConsumeElement.HasAnyState(state.SubGaugeData.ElementType))
            {
                // 计算元素消耗
                var targetConsumed = state.MainGaugeData.GaugeUnits * affinityData.consumeCount;
                var subGauge = state.SubGaugeData;
                subGauge.GaugeUnits -= targetConsumed;
                state.SubGaugeData = subGauge;
            }
            if (state.MainGaugeData.GaugeUnits <= 0) state.MainGaugeData = default;
            if (state.SubGaugeData.GaugeUnits <= 0) state.SubGaugeData = default;

            return affinityData;
        }
    }

    public struct PlayerElementComponent
    {
        public ElementAffinityConfig AffinityConfig;
        public TransitionLevelBaseDamageConfig TransitionLevelBaseDamageConfig;
        public ElementConfig ElementConfig;
        public JsonDataConfig JsonDataConfig;
    }

    // public struct ElementReactionData
    // {
    //     public ElementReactionType ReactionType;
    //     public ElementReactionDamageType ElementReactionDamageType;
    // }
}