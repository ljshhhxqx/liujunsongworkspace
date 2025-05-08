using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HotUpdate.Scripts.Config;
using TMPro;
using UnityEngine;
using Random = System.Random;

namespace HotUpdate.Scripts.Tool.Static
{
    public static class GameStaticExtensions
    {
        private static readonly Random Random = new Random();
        
        public static IWeight RandomSelect(IEnumerable<IWeight> items)
        {
            var enumerable = items as IWeight[] ?? items.ToArray();
            if (items == null || !enumerable.Any())
                throw new ArgumentException("At least one item is required");
        
            var totalWeight = enumerable.Sum(item => item.GetWeight());
        
            if (totalWeight <= 0)
                throw new InvalidOperationException("Total weight must be positive");
        
            var randomValue = Random.Next(totalWeight);
            var accumulated = 0;
        
            foreach (var item in enumerable)
            {
                var weight = item.GetWeight();
                if (weight < 0)
                    throw new ArgumentException("Negative weight detected");
            
                accumulated += weight;
                if (randomValue < accumulated)
                    return item;
            }
        
            return enumerable.Last();
        }
        
        
        public static T SelectByWeight<T>(this Dictionary<T, float> weightedItems)
        {
            float total = weightedItems.Values.Sum();
            float random = UnityEngine.Random.Range(0, total);
        
            foreach (var item in weightedItems)
            {
                if (random < item.Value) return item.Key;
                random -= item.Value;
            }
        
            return weightedItems.Last().Key;
        }
        
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            var list = source.ToList();
            int n = list.Count;
            while (n > 1)
            {
                n--;
                int k = Random.Next(n + 1);
                (list[k], list[n]) = (list[n], list[k]);
            }
            return list;
        }

        public static T RandomSelect<T>(this IList<T> list)
        {
            if (list == null || list.Count == 0)
                throw new ArgumentException("At least one item is required");
        
            return list[UnityEngine.Random.Range(0, list.Count)];
        }

        public static T RandomSelect<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable == null)
                throw new ArgumentException("At least one item is required");
        
            var list = enumerable as IList<T> ?? enumerable.ToList();
            return list.RandomSelect();
        }

        public static T[] RandomSelects<T>(this IEnumerable<T> list, int count = 1)
        {
            var enumerable = list as T[] ?? list.ToArray();
            if (enumerable.Length == 0)
                throw new ArgumentException("At least one item is required");
            if (count > enumerable.Length)
                throw new ArgumentException("Count must be less than or equal to the number of items");
        
            var result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = enumerable[UnityEngine.Random.Range(0, enumerable.Length)];
            }
            return result;
        }

        public static T[] RandomSelects<T>(this IList<T> list, int count = 1)
        {
            if (list == null || list.Count == 0)
                throw new ArgumentException("At least one item is required");
            if (count > list.Count)
                throw new ArgumentException("Count must be less than or equal to the number of items");
        
            var result = new T[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = list[UnityEngine.Random.Range(0, list.Count)];
            }
            return result;
        }
        
        public static string GetPropertyDesc(BuffData extraData)
        {
            var data = extraData.increaseDataList[0];
            var attributeIncreaseData = new AttributeIncreaseData();
            var header = new AttributeIncreaseDataHeader();
            header.propertyType = extraData.propertyType;
            header.buffIncreaseType = extraData.mainIncreaseType;
            attributeIncreaseData.increaseValue = data.increaseValue;
            header.buffOperationType = data.operationType;
            attributeIncreaseData.header = header;
            return GetBuffEffectDesc(attributeIncreaseData);
        }

        public static string GetBuffEffectDesc(AttributeIncreaseData[] extraDatas)
        {
            if (extraDatas == null || extraDatas.Length == 0)
            {
                return null;
            }
            var str = new StringBuilder();
            foreach (var data in extraDatas)
            {
                str.Append(GetBuffEffectDesc(data));
                str.Append("\\n");
            }
            return str.ToString().TrimEnd('\n');
        }

        public static string GetRandomBuffEffectDesc(RandomAttributeIncreaseData[] extraDatas)
        {
            if (extraDatas == null || extraDatas.Length == 0)
            {
                return null;
            }
            var str = new StringBuilder();
            foreach (var data in extraDatas)
            {
                str.Append(GetRandomBuffEffectDesc(data));
                str.Append("\\n");
            }   
            return str.ToString().TrimEnd('\n');
        }

        public static string GetRandomBuffEffectDesc(RandomAttributeIncreaseData extraData)
        {
            var header = extraData.header;
            var propName = EnumHeaderParser.GetHeader(header.propertyType);
            var operation = header.buffOperationType switch
            {
                BuffOperationType.Add => "增加",
                BuffOperationType.Subtract => "减少", 
                BuffOperationType.Multiply => "提升",
                BuffOperationType.Divide => "降低",
                _ => "调整"
            };
            var increaseDesc = header.buffIncreaseType switch
            {
                BuffIncreaseType.Base => "基础",
                BuffIncreaseType.Multiplier => "",
                BuffIncreaseType.Extra => "额外",
                BuffIncreaseType.CorrectionFactor => "总",
                BuffIncreaseType.Current => "当前",
                _ => "数值"
            };

            return $"{operation}{{{GetDynamicValueDesc(extraData)}的{increaseDesc}{propName}}}";
        }

        public static string GetBuffEffectDesc(AttributeIncreaseData effect)
        {
            var header = effect.header;
            var propName = EnumHeaderParser.GetHeader(header.propertyType);

            var operation = header.buffOperationType switch
            {
                BuffOperationType.Add => "增加",
                BuffOperationType.Subtract => "减少", 
                BuffOperationType.Multiply => "提升",
                BuffOperationType.Divide => "降低",
                _ => "调整"
            };
            var increaseDesc = header.buffIncreaseType switch
            {
                BuffIncreaseType.Base => "基础",
                BuffIncreaseType.Multiplier => "",
                BuffIncreaseType.Extra => "额外",
                BuffIncreaseType.CorrectionFactor => "总",
                BuffIncreaseType.Current => "当前",
                _ => "数值"
            };

            return $"{operation}{{{GetDynamicValueDesc(effect)}的{increaseDesc}{propName}}}";
        }

        public static string GetDynamicValueDesc(AttributeIncreaseData effect)
        {
            return effect.header.buffIncreaseType switch
            {
                BuffIncreaseType.Multiplier => $"{effect.increaseValue:P0}",
                BuffIncreaseType.CorrectionFactor => $"{effect.increaseValue:P0}",
                _ => $"{effect.increaseValue:F1}"
            };
        }
        
        public static string GetDynamicValueDesc(RandomAttributeIncreaseData effect)
        {
            return effect.header.buffIncreaseType switch
            {
                BuffIncreaseType.Multiplier => $"{effect.increaseValueRange.min:P0}~{effect.increaseValueRange.max:P0}",
                BuffIncreaseType.CorrectionFactor => $"{effect.increaseValueRange.min:P0}~{effect.increaseValueRange.max:P0}",
                _ => $"{effect.increaseValueRange.min:F1}~{effect.increaseValueRange.max:F1}"
            };
        }
        
        private static readonly System.Random ChunkRandom = new System.Random();
    
        public static IEnumerable<IEnumerable<T>> Chunk<T>(
            this IEnumerable<T> source, 
            int count, 
            bool isRandomChunk = false)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (count <= 0) throw new ArgumentOutOfRangeException(nameof(count), "Count must be greater than 0.");
        
            if (isRandomChunk)
            {
                // 随机化处理
                var randomized = source.Shuffle().ToList();
                return ChunkInternal(randomized, count);
            }
        
            return ChunkInternal(source, count);
        }
    
        private static IEnumerable<IEnumerable<T>> ChunkInternal<T>(IEnumerable<T> source, int count)
        {
            var enumerator = source.GetEnumerator();
            while (enumerator.MoveNext())
            {
                yield return GetChunk(enumerator, count - 1);
            }
        }
    
        private static IEnumerable<T> GetChunk<T>(this IEnumerator<T> enumerator, int count)
        {
            do
            {
                yield return enumerator.Current;
            }
            while (count-- > 0 && enumerator.MoveNext());
        }

        public static Vector3 GetNearestVector(this IEnumerable<Vector3> vectors, Vector3 target)
        {
            var distance = float.MaxValue;
            Vector3 nearest = default;
            foreach (var vector in vectors)
            {
                var dis = Vector3.Distance(vector, target);
                if (dis < distance)
                {
                    distance = dis;
                    nearest = vector;
                }
            }
            return nearest;
        }
        
        public static void FollowTarget(FollowTargetParams followParams)
        {
            // 计算距离
            if (followParams.DistanceText)
            {
                var distance = Vector3.Distance(followParams.Player, followParams.Target);
                followParams.DistanceText.text = $"{distance:F1}m";
            }

            // 将目标世界坐标转换为屏幕坐标
            var screenPos = followParams.MainCamera.WorldToScreenPoint(followParams.Target);

            // 检查目标是否在相机前方
            var isBehind = screenPos.z < 0;
            screenPos.z = 0;

            // 如果目标在相机后方，将指示器翻转到屏幕另一边
            if (isBehind)
            {
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
            }

            // 将屏幕坐标转换为Canvas坐标
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                followParams.CanvasRect, screenPos, null, out var localPos);
            
            // 确保指示器在屏幕边界内
            var clampedPos = ClampToScreen(followParams, localPos);
            followParams.IndicatorUI.localPosition = clampedPos;

            // 计算指示器的旋转（指向目标方向）
            var direction = localPos - (Vector2)followParams.IndicatorUI.localPosition;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            followParams.IndicatorUI.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private static Vector2 ClampToScreen(FollowTargetParams targetParams,Vector2 position)
        {
            // 获取Canvas的一半大小
            var halfSize = targetParams.CanvasRect.rect.size * 0.5f;
            halfSize -= new Vector2(targetParams.ScreenBorderOffset, targetParams.ScreenBorderOffset);

            // 限制位置在屏幕边界内
            var clampedX = Mathf.Clamp(position.x, -halfSize.x, halfSize.x);
            var clampedY = Mathf.Clamp(position.y, -halfSize.y, halfSize.y);

            return new Vector2(clampedX, clampedY);
        }
    }
    
    
    public interface IWeight
    {
        int GetWeight();
    }

    public class FollowTargetParams
    {
        public Vector3 Target;
        public Vector3 Player;
        public TextMeshProUGUI DistanceText;
        public RectTransform IndicatorUI;
        public Camera MainCamera;
        public RectTransform CanvasRect;
        public float ScreenBorderOffset;
    }
}