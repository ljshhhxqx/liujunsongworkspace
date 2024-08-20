using System.Collections.Generic;
using UnityEngine;
using VContainer;

namespace Collector
{
    public class CollectItemSpawner
    {
        private GameObject[] _itemPrefabs; // 物品预制体数组
        private Vector3 _mapMinBoundary; // 地图最小边界
        private Vector3 _mapMaxBoundary; // 地图最大边界
        private LayerMask _spawnLayer; // 用于检测生成点是否被阻挡的层级
        private MapBoundDefiner _mapBoundDefiner; // 地图边界定义器
        private GameDataConfig _gameDataConfig; // 游戏数据配置

        private readonly System.Random _random = new System.Random();

        [Inject]
        private void Init(MapBoundDefiner mapBoundDefiner, IConfigProvider configProvider)
        {
            _mapBoundDefiner = mapBoundDefiner;
            _gameDataConfig = configProvider.GetConfig<GameDataConfig>();
        }

        public int GenerateRandomWeight()
        {
            // 根据需要生成随机权重
            return _random.Next(1, 100);
        }

        public int GenerateRandomSpawnMethod()
        {
            // 随机选择生成方式，0-3分别对应四种生成方式
            return _random.Next(0, 4);
        }

        public void SpawnItems(int weight, int spawnMethod)
        {
            List<GameObject> itemsToSpawn = GenerateItemsList(weight, spawnMethod);

            foreach (GameObject item in itemsToSpawn)
            {
                Vector3 spawnPoint = FindValidSpawnPoint(item);
                if (spawnPoint != Vector3.zero)
                {
                    Object.Instantiate(item, spawnPoint, Quaternion.identity);
                }
            }
        }

        private List<GameObject> GenerateItemsList(int weight, int spawnMethod)
        {
            List<GameObject> itemsList = new List<GameObject>();

            switch (spawnMethod)
            {
                case 0: // 较小的随机X的权重
                    while (weight > 0)
                    {
                        itemsList.Add(_itemPrefabs[0]); // a物品
                        weight -= GetItemWeight(_itemPrefabs[0]);
                        if (weight <= 0)
                        {
                            itemsList.Add(_itemPrefabs[2]); // c物品
                            break;
                        }
                    }
                    break;
                case 1: // 中等的随机Y的权重
                    while (weight > 0)
                    {
                        int itemIndex = (weight > GetItemWeight(_itemPrefabs[1]) && itemsList.Count > 0) ? 1 : 0;
                        itemsList.Add(_itemPrefabs[itemIndex]);
                        weight -= GetItemWeight(_itemPrefabs[itemIndex]);
                        if (weight <= 0 && !itemsList.Contains(_itemPrefabs[2]))
                        {
                            itemsList.Add(_itemPrefabs[2]);
                        }
                    }
                    break;
                case 2: // 较大数量的随机Z权重
                    while (weight > 0)
                    {
                        int itemIndex = itemsList.Count % 2;
                        itemsList.Add(_itemPrefabs[itemIndex]);
                        weight -= GetItemWeight(_itemPrefabs[itemIndex]);
                    }
                    break;
                case 3: // 大量的随机P权重
                    while (weight > 0)
                    {
                        int itemIndex = _random.Next(0, _itemPrefabs.Length);
                        itemsList.Add(_itemPrefabs[itemIndex]);
                        weight -= GetItemWeight(_itemPrefabs[itemIndex]);
                    }
                    break;
            }

            return itemsList;
        }

        private int GetItemWeight(GameObject item)
        {
            // 返回物品的权重，这里简单假设a=1, b=2, c=3
            if (item == _itemPrefabs[0]) return 1;
            if (item == _itemPrefabs[1]) return 2;
            if (item == _itemPrefabs[2]) return 3;
            return 0;
        }

        private Vector3 FindValidSpawnPoint(GameObject item)
        {
            for (int i = 0; i < 10; i++) // 尝试10次找到一个有效的生成点
            {
                Vector3 randomPoint = new Vector3(
                    Random.Range(_mapMinBoundary.x, _mapMaxBoundary.x),
                    0.5f,
                    Random.Range(_mapMinBoundary.z, _mapMaxBoundary.z)
                );

                if (IsValidSpawnPoint(randomPoint, item))
                {
                    return randomPoint;
                }
            }

            return Vector3.zero;
        }

        private bool IsValidSpawnPoint(Vector3 point, GameObject item)
        {
            Collider itemCollider = item.GetComponent<Collider>();
            if (!itemCollider) return false;

            Vector3 halfExtents = itemCollider.bounds.extents;
            Collider[] colliders = Physics.OverlapBox(point, halfExtents, Quaternion.identity, _spawnLayer);

            foreach (Collider collider in colliders)
            {
                if (collider.gameObject.layer == LayerMask.NameToLayer("Scene"))
                {
                    if (Physics.Raycast(point, Vector3.up, out var hit))
                    {
                        point.y = hit.point.y + 0.5f;
                        return true;
                    }
                }
                else
                {
                    return false;
                }
            }

            return true;
        }
    }
}
