using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using MemoryPack;
using Mirror;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.Shop
{
    /// <summary>
    /// 存储每个玩家的随机物品和剩余可购买次数
    /// </summary>
    public class ShopSystem : NetworkBehaviour
    {
        // private PlayerInGameManager _playerInGameManager;
        // private GameSyncManager _gameSyncManager;
        // private ShopConfig _shopConfig;
        // private SyncDictionary<int, RandomShopItemData[]> _playerShopDataDict;
        // private Dictionary<int, ReactiveDictionary<int, RandomShopItemData>> _playerInventoryDict;
        //
        // [Inject]
        // private void Init(PlayerInGameManager playerInGameManager, IConfigProvider configProvider)
        // {
        //     _gameSyncManager = FindObjectOfType<GameSyncManager>();
        //     _shopConfig = configProvider.GetConfig<ShopConfig>();
        //     _playerInGameManager = playerInGameManager;
        //     _playerShopDataDict = new SyncDictionary<int, RandomShopItemData[]>();
        //     _playerInventoryDict = new Dictionary<int, ReactiveDictionary<int, RandomShopItemData>>();
        //     _playerShopDataDict.OnChange += OnPlayerShopDataChanged;
        //     _gameSyncManager.OnPlayerConnected += OnPlayerConnected;
        //     _gameSyncManager.OnPlayerDisconnected += OnPlayerDisconnected;
        // }
        //
        // private void OnPlayerShopDataChanged(SyncIDictionary<int, RandomShopItemData[]>.Operation operation, int connectionId, RandomShopItemData[] newData)
        // {
        //     switch (operation)
        //     {
        //         case SyncIDictionary<int, RandomShopItemData[]>.Operation.OP_ADD:
        //             foreach (var key in _playerInventoryDict.Keys)
        //             {
        //                 
        //             }
        //             break;
        //         case SyncIDictionary<int, RandomShopItemData[]>.Operation.OP_CLEAR:
        //             break;
        //         case SyncIDictionary<int, RandomShopItemData[]>.Operation.OP_REMOVE:
        //             break;
        //         case SyncIDictionary<int, RandomShopItemData[]>.Operation.OP_SET:
        //             break;
        //         default:
        //             throw new ArgumentOutOfRangeException(nameof(operation), operation, null);
        //     }
        // }
        //
        // [ServerCallback]
        // private void OnPlayerDisconnected(int connectionId)
        // {
        //     if (_playerShopDataDict.ContainsKey(connectionId))
        //         _playerShopDataDict.Remove(connectionId);
        // }
        //
        // [ServerCallback]
        // private void OnPlayerConnected(int connectionId, NetworkIdentity identity)
        // {
        //     var shopIds = _shopConfig.RefreshShopItems();
        //     var shopData = new HashSet<RandomShopItemData>();
        //     foreach (var shopId in shopIds)
        //     {
        //         var shopConfigData = _shopConfig.GetShopConfigData(shopId);
        //         var randomShopData = new RandomShopItemData
        //         {
        //             ShopId = HybridIdGenerator.GenerateChestId(shopId, GameSyncManager.CurrentTick),
        //             ShopConfigId = shopId,
        //             RemainingCount = shopConfigData.maxCount,
        //             ItemType = shopConfigData.playerItemType
        //         };
        //         shopData.Add(randomShopData);
        //     }
        //     _playerShopDataDict.Add(connectionId, shopData.ToArray());
        // }
        //
        // public bool TryRefreshShopItems(int connectionId)
        // {
        //     var propertySystem = _gameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
        //     if (propertySystem == null)
        //         return false;
        //     if (!_playerShopDataDict.TryGetValue(connectionId, out var playerShopData))
        //         return false;
        //     var costGold = _shopConfig.GetShopConstantData().onceCostGold;
        //     if (!propertySystem.TryUseGold(connectionId, costGold, out var remaining))
        //     {
        //         Debug.Log($"Player {connectionId} has not enough gold to refresh shop items. current gold is {remaining + costGold}, needed {costGold}");
        //         return false;
        //     }
        //     var shopIds = _shopConfig.RefreshShopItems();
        //     var shopData = new HashSet<RandomShopItemData>();
        //     foreach (var shopId in shopIds)
        //     {
        //         var shopConfigData = _shopConfig.GetShopConfigData(shopId);
        //         var randomShopData = new RandomShopItemData
        //         {
        //             ShopId = HybridIdGenerator.GenerateChestId(shopId, GameSyncManager.CurrentTick),
        //             ShopConfigId = shopId,
        //             RemainingCount = shopConfigData.maxCount,
        //             ItemType = shopConfigData.playerItemType
        //         };
        //         shopData.Add(randomShopData);
        //     }
        //     _playerShopDataDict[connectionId] = shopData.ToArray();
        //     var command = new GoldChangedCommand
        //     {
        //         Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item,
        //             CommandAuthority.Client, CommandExecuteType.Immediate),
        //         Gold = -costGold
        //     };
        //     var commandData = MemoryPackSerializer.Serialize(command);
        //     _gameSyncManager.EnqueueCommand(commandData);
        //     return true;
        // }
        //
        // public void BuyShopItem(int connectionId, int shopId, int count)
        // {
        //     if (connectionId == 0 || !isLocalPlayer)
        //         return;
        //     if (!_playerShopDataDict.TryGetValue(connectionId, out var playerShopData))
        //         return;
        //     var shopData = playerShopData;
        //     var randomShopData = shopData.First(x => x.ShopId == shopId);
        //     var randomShopConfigData = _shopConfig.GetShopConfigData(randomShopData.ShopConfigId);
        //     var otherShopData = shopData.First(x => x.ItemType == randomShopData.ItemType);
        //     if (randomShopData.Equals(default) || randomShopData.RemainingCount < count)
        //         return;
        //     randomShopData.RemainingCount -= count;
        //     
        //     if (randomShopData.RemainingCount == 0)
        //     {
        //         RefreshShopItems(ref playerShopData, randomShopData, otherShopData);
        //     }
        //     _playerShopDataDict[connectionId] = playerShopData;
        //
        //     var itemsCommandData = new ItemsCommandData[count];
        //     for (var i = 0; i < count; i++)
        //     {
        //         itemsCommandData[i] = new ItemsCommandData
        //         {
        //             Count = count,
        //             ItemShopId = shopId,
        //             ItemType = randomShopData.ItemType,
        //             ItemConfigId = randomShopConfigData.itemId,
        //         };
        //     }
        //     var itemBuyCommand = new ItemsBuyCommand
        //     {
        //         Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item, CommandAuthority.Client, CommandExecuteType.Immediate),
        //         Items = itemsCommandData,
        //     };
        //     var itemBuyCommandData = MemoryPackSerializer.Serialize(itemBuyCommand);
        //     _gameSyncManager.EnqueueCommand(itemBuyCommandData);
        // }
        //
        // private void RefreshShopItems(ref RandomShopItemData[] playerShopData, RandomShopItemData randomShopItemData, RandomShopItemData otherShopItemData)
        // {
        //     var newConfigId = _shopConfig.GetRandomItem(randomShopItemData.ShopConfigId, otherShopItemData.ShopConfigId, randomShopItemData.ItemType);
        //     var newRandomConfigData = _shopConfig.GetShopConfigData(newConfigId);
        //     var hashSet = playerShopData.ToHashSet();
        //     hashSet.Remove(randomShopItemData);
        //     hashSet.Add(new RandomShopItemData
        //     {
        //         ShopId = HybridIdGenerator.GenerateChestId(newConfigId, GameSyncManager.CurrentTick),
        //         ShopConfigId = newConfigId,
        //         RemainingCount = newRandomConfigData.maxCount,
        //         ItemType = newRandomConfigData.playerItemType
        //     });
        //     playerShopData = hashSet.ToArray();
        // }
    }
}