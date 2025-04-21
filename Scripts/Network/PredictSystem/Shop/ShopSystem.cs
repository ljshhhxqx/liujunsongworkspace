using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using MemoryPack;
using Mirror;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.Shop
{
    /// <summary>
    /// 存储每个玩家的随机物品和剩余可购买次数
    /// </summary>
    public class ShopSystem : NetworkBehaviour
    {
        private PlayerInGameManager _playerInGameManager;
        private GameSyncManager _gameSyncManager;
        private ShopConfig _shopConfig;
        private SyncDictionary<int, PlayerShopData> _playerShopDataDict;

        [Inject]
        private void Init(PlayerInGameManager playerInGameManager, IConfigProvider configProvider)
        {
            _gameSyncManager = FindObjectOfType<GameSyncManager>();
            _shopConfig = configProvider.GetConfig<ShopConfig>();
            _playerInGameManager = playerInGameManager;
            _playerShopDataDict = new SyncDictionary<int, PlayerShopData>();
            _gameSyncManager.OnPlayerConnected += OnPlayerConnected;
            _gameSyncManager.OnPlayerDisconnected += OnPlayerDisconnected;
        }

        [ServerCallback]
        private void OnPlayerDisconnected(int connectionId)
        {
            if (_playerShopDataDict.ContainsKey(connectionId))
                _playerShopDataDict.Remove(connectionId);
        }

        [ServerCallback]
        private void OnPlayerConnected(int connectionId, NetworkIdentity identity)
        {
            PlayerShopData playerShopData;
            var shopIds = _shopConfig.RefreshShopItems();
            var shopData = new HashSet<RandomShopData>();
            foreach (var shopId in shopIds)
            {
                var shopConfigData = _shopConfig.GetShopConfigData(shopId);
                var randomShopData = new RandomShopData
                {
                    ShopId = HybridIdGenerator.GenerateChestId(shopId, GameSyncManager.CurrentTick),
                    ShopConfigId = shopId,
                    RemainingCount = shopConfigData.maxCount,
                    ItemType = shopConfigData.playerItemType
                };
                shopData.Add(randomShopData);
            }
            _playerShopDataDict.Add(connectionId, new PlayerShopData
            {
                PlayerId = connectionId,
                ShopData = shopData,
            });
        }

        public void RefreshShopItems(int connectionId)
        {
            
        }

        public void SellShopItem(int connectionId, int shopId, int count)
        {
            if (connectionId == 0 || !isLocalPlayer)
                return;
            if (!_playerShopDataDict.TryGetValue(connectionId, out var playerShopData))
                return;
        }

        public void BuyShopItem(int connectionId, int shopId, int count)
        {
            if (connectionId == 0 || !isLocalPlayer)
                return;
            if (!_playerShopDataDict.TryGetValue(connectionId, out var playerShopData))
                return;
            var shopData = playerShopData.ShopData;
            var randomShopData = shopData.First(x => x.ShopId == shopId);
            var randomShopConfigData = _shopConfig.GetShopConfigData(randomShopData.ShopConfigId);
            var otherShopData = shopData.First(x => x.ItemType == randomShopData.ItemType);
            if (randomShopData.Equals(default) || randomShopData.RemainingCount < count)
                return;
            randomShopData.RemainingCount -= count;
            
            if (randomShopData.RemainingCount == 0)
            {
                var newConfigId = _shopConfig.GetRandomItem(randomShopData.ShopConfigId, otherShopData.ShopConfigId, randomShopData.ItemType);
                var newRandomConfigData = _shopConfig.GetShopConfigData(newConfigId);
                playerShopData.ShopData.Remove(randomShopData);
                playerShopData.ShopData.Add(new RandomShopData
                {
                    ShopId = HybridIdGenerator.GenerateChestId(newConfigId, GameSyncManager.CurrentTick),
                    ShopConfigId = newConfigId,
                    RemainingCount = newRandomConfigData.maxCount,
                    ItemType = newRandomConfigData.playerItemType
                });
            }
            _playerShopDataDict[connectionId] = playerShopData;

            var itemsCommandData = new ItemsCommandData[count];
            for (var i = 0; i < count; i++)
            {
                itemsCommandData[i] = new ItemsCommandData
                {
                    Count = count,
                    ItemShopId = shopId,
                    ItemType = randomShopData.ItemType,
                    ItemConfigId = randomShopConfigData.itemId,
                };
            }
            var itemBuyCommand = new ItemsBuyCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item, CommandAuthority.Client, CommandExecuteType.Immediate),
                Items = itemsCommandData,
            };
            var itemBuyCommandData = MemoryPackSerializer.Serialize(itemBuyCommand);
            _gameSyncManager.EnqueueCommand(itemBuyCommandData);
        }
    }

    public struct PlayerShopData
    {
        public int PlayerId;
        public HashSet<RandomShopData> ShopData;
    }

    public struct RandomShopData : IEquatable<RandomShopData>
    {
        public int ShopId;
        public int ShopConfigId;
        public int RemainingCount;
        public PlayerItemType ItemType;

        public bool Equals(RandomShopData other)
        {
            return ShopId == other.ShopId && RemainingCount == other.RemainingCount;
        }

        public override bool Equals(object obj)
        {
            return obj is RandomShopData other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ShopId, RemainingCount);
        }
    }
}