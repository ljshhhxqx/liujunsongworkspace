using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Tool;
using Newtonsoft.Json;
using UnityEngine;

namespace HotUpdate.Scripts.Config.ArrayConfig
{
    [CreateAssetMenu(fileName = "TimedBuffConfig", menuName = "ScriptableObjects/TimedBuffConfig")]
    public class TimedBuffConfig : ConfigBase
    {
        [SerializeField]
        private List<TimedBuffConfigData> timedBuffs;
        private Dictionary<BuffSourceType, Dictionary<int, TimedBuffConfigData>> _sourceTypeDictionary = new Dictionary<BuffSourceType, Dictionary<int, TimedBuffConfigData>>();

        public Dictionary<BuffSourceType, Dictionary<int, TimedBuffConfigData>> SourceTypeDictionary
        {
            get
            {
                if (_sourceTypeDictionary.Count == 0)
                {
                    foreach (var timedBuffData in timedBuffs)
                    {
                        if (!_sourceTypeDictionary.TryGetValue(timedBuffData.sourceType, out var value))
                        {
                            value = new Dictionary<int, TimedBuffConfigData>();
                            _sourceTypeDictionary.Add(timedBuffData.sourceType, value);
                        }
                        value.Add(timedBuffData.buffId, timedBuffData);
                    }
                }
                return _sourceTypeDictionary;
            }
        }

        protected override void ReadFromCsv(List<string[]> textAsset)
        {
            timedBuffs.Clear();
            for (var i = 2; i < textAsset.Count; i++)
            {
                var text = textAsset[i];
                var timedBuffData = new TimedBuffConfigData();
                timedBuffData.buffId = int.Parse(text[0]);
                timedBuffData.propertyType = (PropertyTypeEnum) Enum.Parse(typeof(PropertyTypeEnum), text[1]);
                timedBuffData.duration = JsonConvert.DeserializeObject<Range>(text[2]);
                timedBuffData.sourceType = (BuffSourceType) Enum.Parse(typeof(BuffSourceType), text[3]);
                timedBuffData.increaseType = (BuffIncreaseType) Enum.Parse(typeof(BuffIncreaseType), text[4]);
                timedBuffData.increaseRange = JsonConvert.DeserializeObject<Range>(text[5]);
                timedBuffData.isPermanent = bool.Parse(text[6]);
                timedBuffs.Add(timedBuffData);
                
            }
        }

        public int GetNoUnionSpeedBuffId()
        {
            var noUnionBuffs = timedBuffs.FindAll(x =>x.sourceType == BuffSourceType.Auto);
            if (noUnionBuffs.Count == 0)
            {
                Debug.LogError("NoUnion buff not found in TimedBuffConfig");
                return default;
            }
            
            return noUnionBuffs.First(x => x.propertyType == PropertyTypeEnum.Speed).buffId;
        }

        public HashSet<int> GetRandomBuffs(BuffSourceType sourceType, int count)
        {
            if (!SourceTypeDictionary.TryGetValue(sourceType, out var value))
            {
                Debug.LogError($"BuffSourceType {sourceType} not found in TimedBuffConfig");
                return null;
            }
            var buffs = value.Values;
            var randomBuffs = buffs.RandomSelects(count);
            return randomBuffs.Select(x => x.buffId).ToHashSet();
        }
        
        public int GetRandomBuff(BuffSourceType sourceType)
        {
            if (!SourceTypeDictionary.TryGetValue(sourceType, out var value))
            {
                Debug.LogError($"BuffSourceType {sourceType} not found in TimedBuffConfig");
                return default;
            }
            var buffs = value.Values;
            var randomBuff = buffs.RandomSelect();
            return randomBuff.buffId;
        }

        public TimedBuffConfigData GetTimedBuffData(int buffId)
        {
            foreach (var timedBuffData in timedBuffs)
            {
                if (timedBuffData.buffId == buffId)
                {
                    return timedBuffData;
                }
            }

            Debug.LogError($"BuffId {buffId} not found in TimedBuffConfig");
            return default;
        }

        public TimedBuffData GetCurrentBuffByDeltaTime(int buffId, float deltaTime)
        {
            var timedBuffData = GetTimedBuffData(buffId);
            var buffData = new TimedBuffData();
            buffData.buffId = timedBuffData.buffId;
            buffData.propertyType = timedBuffData.propertyType;
            buffData.sourceType = timedBuffData.sourceType;
            buffData.increaseValue = (float)CalculateY(deltaTime, timedBuffData.duration.max,
                timedBuffData.duration.min, timedBuffData.increaseRange.min,
                timedBuffData.increaseRange.max, timedBuffData.steepAngle, timedBuffData.growthType,
                timedBuffData.isPermanent);
            buffData.increaseType = timedBuffData.increaseType;
            buffData.increaseType = timedBuffData.increaseType;
            buffData.duration = timedBuffData.duration.max;
            buffData.isPermanent = timedBuffData.isPermanent;
            buffData.operationType = timedBuffData.operationType;
            buffData = buffData.Update(deltaTime);
            return buffData;
        }
        
        /// <summary>
        /// x的取值范围为[e,a]，y的取值范围为[b,c]，计算y=f(x)。
        /// 当bool值为true时，在x小于e时，x等于最小值b，x大于a时，y等于最小值c
        /// 在取值范围内，根据GrowthType的不同，计算y的不同方式。
        /// </summary>
        /// <param name="x"></param>
        /// <param name="a"></param>
        /// <param name="e"></param>
        /// <param name="b"></param>
        /// <param name="c"></param>
        /// <param name="d"></param>
        /// <param name="type"></param>
        /// <param name="allowOutOfBounds"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public static double CalculateY(double x, double a, double e, double b, double c, 
            double d, GrowthType type = GrowthType.Exponential, bool allowOutOfBounds = true)
        {
            // 基础参数验证
            if (a <= e) throw new ArgumentException("a必须大于e");
            if (d < 0) throw new ArgumentException("陡峭度d不能为负数");

            // 越界处理（当允许越界时立即返回极值）
            if (allowOutOfBounds)
            {
                if (x <= e) return b;  // 包含x=e的情况保证边界
                if (x >= a) return c;
            }
            else // 严格区间限制
            {
                if (x < e || x > a)
                    throw new ArgumentException($"x必须在[{e}, {a}]范围内");
            }

            // 计算归一化参数（此时x必然在(e,a)区间）
            double t = (x - e) / (a - e);  // t ∈ (0,1)

            // 核心计算逻辑
            switch (type)
            {
                case GrowthType.Linear:
                    return Linear(b, c, t);
                
                case GrowthType.Exponential:
                    return Exponential(b, c, t, d);
                
                case GrowthType.Sigmoid:
                    return Sigmoid(b, c, t, d);
                
                default:
                    throw new ArgumentException("未知的增长类型");
            }
        }

        // 线性增长
        private static double Linear(double b, double c, double t)
        {
            return b + (c - b) * t;
        }

        // 指数增长
        private static double Exponential(double b, double c, double t, double d)
        {
            return d == 0 
                ? Linear(b, c, t)
                : b + (c - b) * (Math.Exp(d * t) - 1) / (Math.Exp(d) - 1);
        }

        // Sigmoid曲线
        private static double Sigmoid(double b, double c, double t, double d)
        {
            double k = 10 * d;          // 基础陡峭度系数
            double shiftedT = k * (t - 0.5);  // 中心点偏移
            return b + (c - b) / (1 + Math.Exp(-shiftedT));
        }
    }

    public enum GrowthType
    {
        Linear, 
        Exponential, 
        Sigmoid
    }

    [Serializable]
    public struct TimedBuffConfigData
    {
        public int buffId;
        public PropertyTypeEnum propertyType;
        public Range duration;
        public BuffSourceType sourceType;
        public BuffIncreaseType increaseType;
        public Range increaseRange;
        public bool isPermanent;
        public GrowthType growthType;
        public double steepAngle;
        public OperationType operationType;
        public PlayerEffectType playerEffectType;
    }
}