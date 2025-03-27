using System;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerItemCalculator: IPlayerStateCalculator
    {
        public Random Random { get; } = new Random();
        public static PlayerItemConstant Constant { get; private set; }
        public static void SetConstant(PlayerItemConstant constant)
        {
            Constant = constant;
        }
        
        // public static EquipmentPassiveEffectData GetPassiveEffectData(GameItemData itemConfig, Random random)
        // {
        //     var effect = new EquipmentPassiveEffectData();
        //
        //     // 随机选择 PropertyType
        //     var properties = Enum.GetValues(typeof(PropertyTypeEnum)).Cast<PropertyTypeEnum>().ToArray();
        //     effect.propertyType = properties[random.Next(properties.Length)];
        //
        //     // 计算基础值和最大权重
        //     var baseValue = Constant.PropertyConfig.GetBaseValue(effect.propertyType);
        //     var maxWeight = itemConfig.weight;
        //     var conditionId = itemConfig.itemType == PlayerItemType.Armor ? Constant.ArmorConfig.GetArmorBattleConditionID(itemConfig.id)
        //         : Constant.WeaponConfig.GetWeaponBattleConditionID(itemConfig.id);
        //     var condition = Constant.ConditionConfig.GetConditionData(conditionId);
        //
        //     // 计算 E_trigger（固定）
        //     float eTrigger = BattleEffectConditionConfig.CalculateETrigger(condition);
        //
        //     // 计算乘数
        //     var mMax = maxWeight / baseValue;
        //     var mMin = mMax * 0.6f; // 假设最小乘数为最大乘数的 60%
        //     effect.increaseData.increaseType = BuffIncreaseType.Multiplier;
        //     effect.increaseData.increaseValue = mMax - (mMax - mMin) * eTrigger;
        //
        //     return effect;
        // }
        // public Equipment GenerateEquipment(
        //     float totalGold, 
        //     EquipmentPart part,
        //     QualityType quality,
        //     PropertyValueData config)
        // {
        //     var equipment = new Equipment();
        //     
        //     // 计算品质修正后的总价值
        //     float qualityFactor = config.qualityRatioData
        //         .First(q => q.qualityType == quality).ratio;
        //     float actualValue = totalGold * qualityFactor;
        //
        //     // 拆分主被动预算
        //     (float mainBudget, float passiveBudget) = SplitBudget(actualValue, quality);
        //     
        //     // 生成主属性
        //     equipment.MainAttributes = GenerateMainAttributes(
        //         mainBudget, 
        //         part, 
        //         config.propertyWeightData,
        //         config.propertyIncreaseValue
        //     );
        //
        //     // 生成被动属性（需要排除主属性类型）
        //     equipment.PassiveAttributes = GeneratePassiveAttributes(
        //         passiveBudget,
        //         part,
        //         equipment.MainAttributes,
        //         config.propertyWeightData,
        //         config.propertyIncreaseValue
        //     );
        //
        //     return equipment;
        // }
        //
        // private (float main, float passive) SplitBudget(float total, QualityType quality)
        // {
        //     float passiveRatio = quality == QualityType.Common ? 0 : 0.4f;
        //     float variance = Random.Range(-0.1f, 0.1f);
        //     passiveRatio = Mathf.Clamp(passiveRatio * (1 + variance), 0.24f, 0.4f);
        //     
        //     return (
        //         main: total * (1 - passiveRatio),
        //         passive: total * passiveRatio
        //     );
        // }
        //
        // private List<Property> GenerateMainAttributes(
        //     float budget,
        //     EquipmentPart part,
        //     PropertyWeightData[] weightConfig,
        //     PropertyIncreaseValue[] valueConfig)
        // {
        //     var attributes = new List<Property>();
        //     var partWeights = weightConfig.First(w => w.equipmentPart == part);
        //     
        //     // 确保必选最高权重属性
        //     var mustHave = partWeights.propertyWeightList
        //         .OrderByDescending(w => w.weight)
        //         .First();
        //     
        //     // 生成必须属性
        //     var mustProp = GenerateSingleAttribute(
        //         ref budget, 
        //         mustHave.propertyType,
        //         mustHave.buffIncreaseType,
        //         valueConfig
        //     );
        //     attributes.Add(mustProp);
        //
        //     // 生成其他属性
        //     while (budget > 0 && attributes.Count < 3)
        //     {
        //         var candidate = WeightedRandom.Select(
        //             partWeights.propertyWeightList
        //                 .Where(w => !attributes.Any(a => 
        //                     a.Type == w.propertyType && 
        //                     a.IncreaseType == w.buffIncreaseType))
        //                 .ToDictionary(w => w, w => w.weight)
        //         );
        //         
        //         var prop = GenerateSingleAttribute(
        //             ref budget, 
        //             candidate.propertyType,
        //             candidate.buffIncreaseType,
        //             valueConfig
        //         );
        //         attributes.Add(prop);
        //     }
        //
        //     var orderedWeights = partWeights.propertyWeightList
        //         .OrderByDescending(w => w.weight)
        //         .ToList();
        //
        //     // 至少包含前N个高权重属性（N=1或2）
        //     int requiredCount = Random.value < 0.8f ? 1 : 2;
        //     foreach (var weight in orderedWeights.Take(requiredCount))
        //     {
        //         if (attributes.Any(a => 
        //                 a.Type == weight.propertyType && 
        //                 a.IncreaseType == weight.buffIncreaseType)) continue;
        //
        //         var prop = GenerateSingleAttribute(
        //             ref budget, 
        //             weight.propertyType,
        //             weight.buffIncreaseType,
        //             valueConfig
        //         );
        //         attributes.Add(prop);
        //     }
        //     
        //     return attributes;
        // }
        //
        // private Property GenerateSingleAttribute(
        //     ref float remainingBudget,
        //     PropertyTypeEnum type,
        //     BuffIncreaseType increaseType,
        //     PropertyIncreaseValue[] valueConfig)
        // {
        //     var costData = valueConfig
        //         .First(v => v.propertyType == type)
        //         .propertyIncreaseValueList
        //         .First(d => d.buffIncreaseType == increaseType);
        //
        //     float maxValue = remainingBudget / costData.value;
        //     float actualValue = maxValue * Random.Range(0.7f, 1.0f);
        //     
        //     remainingBudget -= actualValue * costData.value;
        //     
        //     return new Property {
        //         Type = type,
        //         IncreaseType = increaseType,
        //         Value = actualValue
        //     };
        // }
        //     
        //     public bool IsClient { get; }
        //     
        //     public bool Validate(
        //         List<Property> properties,
        //         EquipmentPart part,
        //         QualityType quality,
        //         PropertyValueData config,
        //         float originalGold)
        //     {
        //         // 计算总消耗
        //         float totalCost = properties.Sum(p => 
        //             GetPropertyCost(p, config.propertyIncreaseValue)
        //         );
        //
        //         // 计算理论最大值
        //         float qualityFactor = config.qualityRatioData
        //             .First(q => q.qualityType == quality).ratio;
        //         float maxAllowed = originalGold * qualityFactor * 1.1f; // 允许10%误差
        //     
        //         // 检查权重最大属性是否存在
        //         var mustHave = config.propertyWeightData
        //             .First(w => w.equipmentPart == part)
        //             .propertyWeightList
        //             .OrderByDescending(w => w.weight)
        //             .First();
        //     
        //         bool hasCore = properties.Any(p => 
        //             p.Type == mustHave.propertyType && 
        //             p.IncreaseType == mustHave.buffIncreaseType
        //         );
        //
        //         return totalCost <= maxAllowed && hasCore;
        //     }
        //
        //     private float GetPropertyCost(
        //         Property prop, 
        //         PropertyIncreaseValue[] valueConfig)
        //     {
        //         var costPerUnit = valueConfig
        //             .First(v => v.propertyType == prop.Type)
        //             .propertyIncreaseValueList
        //             .First(d => d.buffIncreaseType == prop.IncreaseType)
        //             .value;
        //
        //         return prop.Value * costPerUnit;
        //     }
        //     public bool Validate(
        //         List<Property> properties,
        //         EquipmentPart part,
        //         QualityType quality,
        //         PropertyValueData config,
        //         float originalGold)
        //     {
        //         // 计算总消耗
        //         float totalCost = properties.Sum(p => 
        //             GetPropertyCost(p, config.propertyIncreaseValue)
        //         );
        //
        //         // 计算理论最大值
        //         float qualityFactor = config.qualityRatioData
        //             .First(q => q.qualityType == quality).ratio;
        //         float maxAllowed = originalGold * qualityFactor * 1.1f; // 允许10%误差
        //
        //         // 检查权重最大属性是否存在
        //         var mustHave = config.propertyWeightData
        //             .First(w => w.equipmentPart == part)
        //             .propertyWeightList
        //             .OrderByDescending(w => w.weight)
        //             .First();
        //
        //         bool hasCore = properties.Any(p => 
        //             p.Type == mustHave.propertyType && 
        //             p.IncreaseType == mustHave.buffIncreaseType
        //         );
        //
        //         return totalCost <= maxAllowed && hasCore;
        //     }
        //
        //     private float GetPropertyCost(
        //         Property prop, 
        //         PropertyIncreaseValue[] valueConfig)
        //     {
        //         var costPerUnit = valueConfig
        //             .First(v => v.propertyType == prop.Type)
        //             .propertyIncreaseValueList
        //             .First(d => d.buffIncreaseType == prop.IncreaseType)
        //             .value;
        //
        //         return prop.Value * costPerUnit;
        //     }
    }
    

    public class PlayerItemComponent
    {
    }

    public struct PlayerItemConstant
    {
        public ItemConfig ItemConfig;
        public WeaponConfig WeaponConfig;
        public ArmorConfig ArmorConfig;
        public PropertyConfig PropertyConfig;
        public BattleEffectConditionConfig ConditionConfig;
    }
}