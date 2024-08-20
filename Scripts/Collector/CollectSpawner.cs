using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using VContainer;

namespace Collector
{
    public class CollectSpawner
    {
        private GameDataConfig gameDataConfig;
        private Dictionary<CollectType, GameObject> collects = new Dictionary<CollectType, GameObject>();
        private Transform _collectParent;
        private MapBoundDefiner _mapBoundDefiner; // 地图边界定义器

        [Inject]
        private void Init(IConfigProvider configProvider, MapBoundDefiner mapBoundDefiner)
        {
            gameDataConfig = configProvider.GetConfig<GameDataConfig>();
            _mapBoundDefiner = mapBoundDefiner;
            _collectParent = GameObject.FindWithTag("SpawnedObjects").transform;
        }

        public async UniTaskVoid Spawn(CollectType collectType)
        {
            GameObject prefab = null;
            if (collects.TryGetValue(collectType, out var collect))
            {
                prefab = collect;
            }
            else
            {
                var collectPrefab = GameDefine.GetCollectPrefabName(collectType);
                if (!string.IsNullOrEmpty(collectPrefab))
                {
                    prefab = ResourceManager.Instance.GetResource<GameObject>(new ResourceData { Name = collectPrefab });
                    collects.Add(collectType, prefab);
                }
            }
            var spawnPosition = await FindValidPositionAsync(prefab);
            if (spawnPosition != Vector3.zero)
            {
                Object.Instantiate(prefab, spawnPosition, Quaternion.identity, _collectParent);
            }
        }

        private async UniTask<Vector3> FindValidPositionAsync(GameObject collect)
        {
            if (collect.TryGetComponent<Collider>(out var component))
            {
                // 尝试次数，避免无限循环
                const int maxAttempts = 100;
                for (var i = 0; i < maxAttempts; i++)
                {
                    // 定义地图的生成边界或者区域
                    var mapWidth = gameDataConfig.GameConfigData.MapWidth; // 假设地图宽度
                    var mapDepth = gameDataConfig.GameConfigData.MapDepth; // 假设地图深度
                    // 随机生成一个位置
                    var x = Random.Range(-mapWidth / 2, mapWidth / 2);
                    var z = Random.Range(-mapDepth / 2, mapDepth / 2);
                    var randomPosition = new Vector3(x, 10, z); // 假设从高空开始向下检测地面

                    // 使用Raycast向下检测，查找地面
                    if (Physics.Raycast(randomPosition, Vector3.down, out var hit, Mathf.Infinity, LayerMask.NameToLayer("Scene")))
                    {
                        Collider[] colliders = null;
                        // 检查这个位置周围是否有足够的空间放置宝箱，避免和其他对象重叠
                        if (component is BoxCollider boxCollider)
                        {
                            colliders = Physics.OverlapBox(hit.point, boxCollider.size);
                        }
                        if (colliders != null) // 没有其他物体与宝箱的位置重叠
                        {
                            // 返回有效位置
                            return hit.point;
                        }
                    }
                    await UniTask.DelayFrame(1);
                }
    
            }

            return Vector3.zero;
        }
    }
}
