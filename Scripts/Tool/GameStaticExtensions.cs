using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using AOTScripts.Data;
using Mirror;
using TMPro;
using UnityEngine;
using Random = System.Random;

namespace HotUpdate.Scripts.Tool
{
    public static class GameStaticExtensions
    {
        public const string CommonMapName = "Town";
        private static readonly Random Random = new Random();

        public static Vector3 ToVector3XZ(this Vector2Int dir)
        {
            return new Vector3(dir.x, 0f, dir.y).normalized;
        }
        public static OperationType GetNegativeOperationType(this OperationType operationType)
        {
            return operationType switch
            {
                OperationType.Add => OperationType.Subtract,
                OperationType.Subtract => OperationType.Add,
                OperationType.Multiply => OperationType.Divide,
                OperationType.Divide => OperationType.Multiply,
                _ => OperationType.Add
            };
        }

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

        public static T[] RandomSelects<T>(this IEnumerable<T> list, int count = 1, bool allowDistinct = false)
        {
            var enumerable = list as T[] ?? list.ToArray();
            if (enumerable.Length == 0)
                throw new ArgumentException("At least one item is required");
            if (count > enumerable.Length)
                throw new ArgumentException("Count must be less than or equal to the number of items");
        
            var result = new T[count];
            var indexSet = new HashSet<int>();
            for (int i = 0; i < count; i++)
            {
                var index = UnityEngine.Random.Range(0, enumerable.Length);
                if (!allowDistinct && indexSet.Contains(index))
                {
                    continue;
                }
                result[i] = enumerable[UnityEngine.Random.Range(0, enumerable.Length)];
                indexSet.Add(index);
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
            header.operationType = data.operationType;
            attributeIncreaseData.header = header;
            return GetBuffEffectDesc(attributeIncreaseData);
        }
        public static string GetBuffEffectDesc(List<AttributeIncreaseData> extraDatas, bool showMainHeader = false, bool passiveHeader = false)
        {
            if (extraDatas == null || extraDatas.Count == 0)
            {
                return null;
            }
            var str = new StringBuilder();
            if (showMainHeader)
            {
                str.Append("主要属性:");
            }
            if (passiveHeader)
            {
                str.Append("附加属性:");
            }
            foreach (var data in extraDatas)
            {
                str.Append(GetBuffEffectDesc(data));
                str.Append("\n");
            }
            return str.ToString().TrimEnd('\n');
        }

        public static string GetBuffEffectDesc(AttributeIncreaseData[] extraDatas, bool showHeader = false)
        {
            if (extraDatas == null || extraDatas.Length == 0)
            {
                return null;
            }
            var str = new StringBuilder();
            if (showHeader)
            {
                str.Append("主要属性:");
            }
            foreach (var data in extraDatas)
            {
                str.Append(GetBuffEffectDesc(data));
                str.Append("\n");
            }
            return str.ToString().TrimEnd('\n');
        }

        public static string GetRandomBuffEffectDesc(RandomAttributeIncreaseData[] extraDatas, bool showHeader = false)
        {
            if (extraDatas == null || extraDatas.Length == 0)
            {
                return null;
            }
            var str = new StringBuilder();
            if (showHeader)
            {
                str.Append("附加属性:");
            }
            foreach (var data in extraDatas)
            {
                str.Append(GetRandomBuffEffectDesc(data));
                str.Append("\n");
            }   
            return str.ToString().TrimEnd('\n');
        }
        
        public static string GetRandomBuffEffectDesc(List<RandomAttributeIncreaseData> extraDatas, bool showHeader = false)
        {
            if (extraDatas == null || extraDatas.Count == 0)
            {
                return null;
            }
            var str = new StringBuilder();
            if (showHeader)
            {
                str.Append("附加属性:");
            }
            foreach (var data in extraDatas)
            {
                str.Append(GetRandomBuffEffectDesc(data));
                str.Append("\n");
            }
            return str.ToString().TrimEnd('\n');
        }

        public static string GetRandomBuffEffectDesc(RandomAttributeIncreaseData extraData)
        {
            var header = extraData.header;
            var propName = EnumHeaderParser.GetHeader(header.propertyType);
            var operation = header.operationType switch
            {
                OperationType.Add => "增加",
                OperationType.Subtract => "减少", 
                OperationType.Multiply => "提升",
                OperationType.Divide => "降低",
                _ => "调整"
            };
            var increaseDesc = header.buffIncreaseType switch
            {
                BuffIncreaseType.Base => "[基础]",
                BuffIncreaseType.Multiplier => "",
                BuffIncreaseType.Extra => "[额外]",
                BuffIncreaseType.CorrectionFactor => "[总]",
                BuffIncreaseType.Current => "[当前]",
                _ => "数值"
            };

            return $"{operation}[{GetDynamicValueDesc(extraData)}]的{increaseDesc}[{propName}]";
        }

        private static bool IsPercentProperty(PropertyTypeEnum propertyType)
        {
            return propertyType is PropertyTypeEnum.CriticalRate or PropertyTypeEnum.CriticalDamageRatio;
        }

        public static string GetBuffEffectDesc(AttributeIncreaseData effect)
        {
            var header = effect.header;
            var propName = EnumHeaderParser.GetHeader(header.propertyType);

            var operation = header.operationType switch
            {
                OperationType.Add => "增加",
                OperationType.Subtract => "减少", 
                OperationType.Multiply => "提升",
                OperationType.Divide => "降低",
                _ => "调整"
            };
            var increaseDesc = header.buffIncreaseType switch
            {
                BuffIncreaseType.Base => "[基础]",
                BuffIncreaseType.Multiplier => "",
                BuffIncreaseType.Extra => "[额外]",
                BuffIncreaseType.CorrectionFactor => "[总]",
                BuffIncreaseType.Current => "[当前]",
                _ => "数值"
            };

            return $"{operation}[{GetDynamicValueDesc(effect)}]的{increaseDesc}[{propName}]";
        }

        public static string GetDynamicValueDesc(AttributeIncreaseData effect)
        {
            return effect.header.buffIncreaseType switch
            {
                BuffIncreaseType.Multiplier => IsPercentProperty(effect.header.propertyType) ? $"{effect.increaseValue*100:P1}" : $"{effect.increaseValue*100:F1}",
                BuffIncreaseType.CorrectionFactor => $"{effect.increaseValue:P1}",
                _ => IsPercentProperty(effect.header.propertyType) ? $"{effect.increaseValue:P1}" : $"{effect.increaseValue:F1}"
            };
        }
        
        public static string GetDynamicValueDesc(RandomAttributeIncreaseData effect)
        {
            return effect.header.buffIncreaseType switch
            {
                BuffIncreaseType.Multiplier =>IsPercentProperty(effect.header.propertyType) ? $"{effect.increaseValueRange.min:P1}~{effect.increaseValueRange.max:P1}" : $"{effect.increaseValueRange.min:F1}~{effect.increaseValueRange.max:F1}",
                BuffIncreaseType.CorrectionFactor => $"{effect.increaseValueRange.min:P1}~{effect.increaseValueRange.max:P1}",
                _ => IsPercentProperty(effect.header.propertyType) ? $"{effect.increaseValueRange.min:P1}~{effect.increaseValueRange.max:P1}" : $"{effect.increaseValueRange.min:F1}~{effect.increaseValueRange.max:F1}"
            };
        }
        
        
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
            
            followParams.IndicatorUI.localScale = Vector3.one;

            // 将目标世界坐标转换为屏幕坐标
            var screenPos = followParams.MainCamera.WorldToScreenPoint(followParams.Target);
            var isBehind = screenPos.z < 0;
            screenPos.z = 0;
            
            if (isBehind)
            {
                screenPos.x = Screen.width - screenPos.x;
                screenPos.y = Screen.height - screenPos.y;
                if (!followParams.ShowBehindIndicator)
                {
                    followParams.IndicatorUI.localScale = Vector3.zero;
                    return; // 如果不需要显示背后的指示器，直接返回
                }
            }

            // 获取Canvas的渲染摄像机（在Screen Space - Camera模式下需要）
            Camera canvasCamera = followParams.CanvasCamera;
            
            if (!canvasCamera)
            {
                canvasCamera = followParams.MainCamera;
            }

            // 将屏幕坐标转换为Canvas坐标
            bool success = RectTransformUtility.ScreenPointToLocalPointInRectangle(
                followParams.CanvasRect, 
                screenPos, 
                canvasCamera, // 关键修改：传入Canvas摄像机
                out var localPos);
            
            if (!success)
            {
                // 如果转换失败，尝试使用默认方式
                localPos = Vector2.zero;
            }

            // 确保指示器在屏幕边界内
            var clampedPos = ClampToScreen(followParams, localPos);
            followParams.IndicatorUI.localPosition = clampedPos;
            
            // 计算指示器方向
            var direction = localPos - (Vector2)followParams.IndicatorUI.localPosition;
            var angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
            followParams.IndicatorUI.localRotation = Quaternion.Euler(0, 0, angle);
        }

        private static Vector2 ClampToScreen(FollowTargetParams targetParams, Vector2 position)
        {
            // 获取Canvas的一半大小
            var halfSize = targetParams.CanvasRect.rect.size * 0.5f;
            halfSize -= new Vector2(targetParams.ScreenBorderOffset, targetParams.ScreenBorderOffset);

            // 限制位置在屏幕边界内
            var clampedX = Mathf.Clamp(position.x, -halfSize.x, halfSize.x);
            var clampedY = Mathf.Clamp(position.y, -halfSize.y, halfSize.y);

            return new Vector2(clampedX, clampedY);
        }

        public static float GetAttackExpectancy(float attack, float criticalRate, float criticalDamage)
        {
            return attack * (1 + criticalRate * (criticalDamage - 1));
        }

        public static NetworkIdentity GetNetworkIdentity(uint netId)
        {
            if (NetworkServer.spawned.TryGetValue(netId, out NetworkIdentity networkIdentity))
            {
                return networkIdentity;
            }

            if (NetworkClient.spawned.TryGetValue(netId, out networkIdentity))
            {
                return networkIdentity;
            }
            Debug.LogError($"No network identity for {netId}");
            return null;
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
        // 可选：直接传入Canvas的渲染摄像机
        public Camera CanvasCamera;
        public bool ShowBehindIndicator;
    }
}