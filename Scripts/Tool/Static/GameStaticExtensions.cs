using System;
using System.Collections.Generic;
using System.Linq;

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
        
        public static IList<T> Shuffle<T>(this IList<T> list)
        {
            var random = new Random();
            var enumerable = list as T[] ?? list.ToArray();
            var n = enumerable.Length;
            while (n > 1) 
            {
                n--;
                int k = random.Next(n + 1);
                (enumerable[k], enumerable[n]) = (enumerable[n], enumerable[k]);
            }
            return enumerable;
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
    }
    
    
    public interface IWeight
    {
        int GetWeight();
    }
}