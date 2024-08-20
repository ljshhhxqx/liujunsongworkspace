using UnityEngine;

namespace Collector
{
    public class ItemSpawnStrategyFactory
    {
        public IItemSpawnStrategy CreateStrategy()
        {
            var strategyType = Random.Range(0, 4);
            return strategyType switch
            {
                0 => new StrategySmallRandomX(),
                1 => new StrategySmallRandomY(),
                2 => new StrategySmallRandomZ(),
                3 => new StrategySmallRandomP(),
                _ => null
            };
        }
    }

    public interface IItemSpawnStrategy 
    {
        void SpawnItems(Vector3 startPoint, int weight);
    }

    internal class StrategySmallRandomX : IItemSpawnStrategy
    {
        public void SpawnItems(Vector3 startPoint, int weight) 
        {
        }
    }

    internal class StrategySmallRandomY : IItemSpawnStrategy
    {
        public void SpawnItems(Vector3 startPoint, int weight) 
        {
        }
    }

    internal class StrategySmallRandomZ : IItemSpawnStrategy
    {
        public void SpawnItems(Vector3 startPoint, int weight) 
        {
        }
    }

    internal class StrategySmallRandomP : IItemSpawnStrategy
    {
        public void SpawnItems(Vector3 startPoint, int weight) 
        {
        }
    }
}