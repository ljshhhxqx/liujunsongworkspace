using System;
using System.Collections.Generic;
using AOTScripts.Data;
using HotUpdate.Scripts.Common;
using Sirenix.OdinInspector;
using UnityEngine;
using UnityEngine.Serialization;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "ElementAffinityConfig", menuName = "ScriptableObjects/ElementAffinityConfig")]
    public class ElementAffinityConfig : ConfigBase
    {
        [ReadOnly]
        [SerializeField]
        private List<ElementAffinityData> elementAffinityData = new List<ElementAffinityData>();
        
        public ElementAffinityData GetElementAffinityData(ElementType elementType, ElementType affinityType)
        {
            return elementAffinityData.Find(data => data.attackType == elementType && data.defendType == affinityType);    
        }
        
        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            // elementAffinityData.Clear();
            // for (var i = 2; i < textAsset.Count; i++)
            // {
            //     var data = textAsset[i];
            //     var affinityData = new ElementAffinityData();
            //     affinityData.attackType = (ElementType) Enum.Parse(typeof(ElementType), data[0]);
            //     affinityData.defendType = (ElementType) Enum.Parse(typeof(ElementType), data[1]);
            //     affinityData.multiplier = float.Parse(data[2]);
            //     affinityData.consumeCount = float.Parse(data[3]);
            //     affinityData.elementReactionType = (ElementReactionType) Enum.Parse(typeof(ElementReactionType), data[4]);
            //     if (Enum.TryParse<ElementType>(data[5], out var type))
            //     {
            //         affinityData.reactionConsumeElement = type;
            //     }
            //     else
            //     {
            //         var strs = data[5].Split(',');
            //         affinityData.reactionConsumeElement = ElementType.None;
            //         foreach (var str in strs)
            //         {
            //             affinityData.reactionConsumeElement.AddState((ElementType) Enum.Parse(typeof(ElementReactionDamageType), str));
            //         }
            //     }
            //     if (Enum.TryParse<EffectType>(data[6], out var reactionDamageType))
            //     {
            //         affinityData.effectType = reactionDamageType;
            //     }
            //     else
            //     {
            //         var strs = data[6].Split(',');
            //         
            //         affinityData.effectType = EffectType.None;
            //         foreach (var str in strs)
            //         {
            //             affinityData.effectType.AddState((EffectType) Enum.Parse(typeof(EffectType), str));
            //         }
            //     }
            //     elementAffinityData.Add(affinityData);
            // }
        }
        
        public float GetMultiplier(ElementType attackType, ElementType defendType)
        {
            foreach (var data in elementAffinityData)
            {
                if (data.attackType == attackType && data.defendType == defendType)
                {
                    return data.multiplier;
                }
            }

            return 1f;
        }
        
        public float GetConsumeCount(ElementType attackType, ElementType defendType)
        {
            foreach (var data in elementAffinityData)
            {
                if (data.attackType == attackType && data.defendType == defendType)
                {
                    return data.consumeCount;
                }
            }

            return 0f;
        }
        
        public ElementReactionDamageType GetElementEffectType(ElementType attackType, ElementType defendType)
        {
            foreach (var data in elementAffinityData)
            {
                if (data.attackType == attackType && data.defendType == defendType)
                {
                    return data.elementReactionDamageType;
                }
            }

            return ElementReactionDamageType.None;
        }
        
        public ElementReactionType GetElementReactionType(ElementType attackType, ElementType defendType)
        {
            foreach (var data in elementAffinityData)
            {   
                if (data.attackType == attackType && data.defendType == defendType)
                {
                    return data.elementReactionType;
                }
            }

            return ElementReactionType.None;
        }
    }

    [Serializable]
    public struct ElementAffinityData
    {
        public ElementType attackType;
        public ElementType defendType;
        //伤害倍率
        public float multiplier;
        public float consumeCount;
        public ElementReactionType elementReactionType;
        public ElementReactionDamageType elementReactionDamageType;
        //共存态时实际消耗的元素
        public ElementType reactionConsumeElement;
        public EffectType effectType;
    }

    public enum ElementReactionType : byte
    {
        // 不属于任何反应
        None,
        // 蒸发 (火+水 / 水+火)
        Vaporize,
        // 冻结 (水+冰)
        Freeze,
        // 融化 (火+冰 / 冰+火)
        Melt,
        // 超导 (雷+冰)
        SuperConduct,
        // 感电 (雷+水)
        Electrify,
        // 超载 (火+雷)
        Overload,
        // 扩散 (风+火/水/雷/冰)
        Diffusion,
        // 碎冰 (冻结后被钝器、岩属性攻击等打破)
        Blizzard,
        // 结晶 (岩+火/水/雷/冰)
        Crystallize,
        // 绽放 (草+水)
        Bloom,
        // 超绽放 (绽放+雷)
        SuperBloom,
        // 烈绽放 (绽放+火)
        BoomBloom,
        // 原激化 (草+雷)
        OriginalEvaporate,
        // 超激化 (原激化+雷)
        SuperEvaporate,
        // 蔓激化 (原激化+草)
        SeedEvaporate,
        // 燃烧 (火+草)
        Burning
    }

    public enum ElementReactionDamageType : byte
    {
        //不计算额外伤害
        None,
        //增幅
        Amplification,
        //剧变
        Transition,
        //todo : 其他计算伤害的反应(请完善)
    }
}