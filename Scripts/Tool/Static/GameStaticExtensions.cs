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
        
        
        public static T Select<T>(Dictionary<T, float> weightedItems)
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
        
        public static IList<T> Shuffle<T>(IList<T> list)
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

        public static T RandomSelect<T>(IList<T> list)
        {
            if (list == null || list.Count == 0)
                throw new ArgumentException("At least one item is required");
        
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
    }
    
    
    public interface IWeight
    {
        int GetWeight();
    }
}