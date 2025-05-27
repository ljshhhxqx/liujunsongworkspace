using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerShopCalculator : IPlayerStateCalculator
    {
        private static ShopCalculatorConstant Constant;

        public static void SetConstant(ShopCalculatorConstant constant)
        {
            Constant = constant;
        }

        public static ShopItemData[] GetRandomShopItemData(HashSet<int> preShopConfigIds = null)
        {
            var shopConfigIds = Constant.ShopConfig.RefreshShopItems(preShopConfigIds);
            var shopData = new ShopItemData[shopConfigIds.Count];
            var index = 0;
            foreach (var shopConfigId in shopConfigIds)
            {
                var shopConfigData = Constant.ShopConfig.GetShopConfigData(shopConfigId);
                var itemConfigData = Constant.ItemConfig.GetGameItemData(shopConfigData.itemId);
                var attributeData = PlayerItemCalculator.GetAttributeIncreaseDatas(itemConfigData.buffExtraData);
                var mainAttributeData = new AttributeIncreaseData[itemConfigData.buffExtraData.Length];
                var passiveAttributeData = new RandomAttributeIncreaseData[itemConfigData.buffExtraData.Length];
                for (int i = 0; i < attributeData.Length; i++)
                {
                    var expr = attributeData[i];
                    if (expr is AttributeIncreaseData passiveAttributeIncreaseData)
                    {
                        mainAttributeData[i] = passiveAttributeIncreaseData;
                    }
                    else if (expr is RandomAttributeIncreaseData passiveAttributeIncreaseData2)
                    {
                        passiveAttributeData[i] = passiveAttributeIncreaseData2;
                    }
                }

                var randomShopData = new ShopItemData
                {
                    ShopId = HybridIdGenerator.GenerateChestId(shopConfigId, GameSyncManager.CurrentTick),
                    ShopConfigId = shopConfigId,
                    RemainingCount = shopConfigData.maxCount,
                    ItemType = shopConfigData.playerItemType,
                    MaxCount = shopConfigData.maxCount,
                    ItemConfigId = shopConfigData.itemId,
                    Price = shopConfigData.price,
                    SellPrice = shopConfigData.sellPrice,
                    Quality = shopConfigData.qualityType,
                    MainIncreaseDatas = mainAttributeData,
                    PassiveIncreaseDatas = passiveAttributeData,
                };
                shopData[index] = randomShopData;
                index++;
            }
            return shopData;
        }

        public static void CommandBuyItem(ref PlayerShopState state, int connectionId, int shopId, int count, bool isServer = false)
        {
            var shopData = state.RandomShopItemsDict.Values.ToArray();
            var randomShopData = shopData.First(x => x.ShopId == shopId);
            var randomShopConfigData = Constant.ShopConfig.GetShopConfigData(randomShopData.ShopConfigId);
            var otherShopData = shopData.First(x => x.ItemType == randomShopData.ItemType);
            if (randomShopData.Equals(default) || randomShopData.RemainingCount < count)
                return;
            randomShopData.RemainingCount -= count;
            
            if (randomShopData.RemainingCount == 0)
            {
                RefreshShopItems(ref shopData, randomShopData, otherShopData);
            }

            foreach (var data in shopData)
            {
                state.RandomShopItemsDict[data.ShopId] = data;
            }
            if (!isServer)
                return;

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
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item, CommandAuthority.Server, CommandExecuteType.Immediate),
                Items = itemsCommandData,
            };
            Constant.GameSyncManager.EnqueueServerCommand(itemBuyCommand);
        }

        public static void CommandSellItem(int connectionId, int slotIndex, int count, bool isServer = false)
        {
            if (!isServer)
            {
                return;
            }
            var itemSyncSystem = Constant.GameSyncManager.GetSyncSystem<PlayerItemSyncSystem>(CommandType.Item);
            var playerBag = itemSyncSystem.GetPlayerBagSlotItems(connectionId);
            if (!playerBag.TryGetValue(slotIndex, out var itemData) || itemData.ConfigId == 0)
            {
                Debug.LogError($"Player slot {slotIndex} does not exist");
                return;
            }

            var data = Constant.ShopConfig.GetShopDataByItemId(itemData.ConfigId);
            if (data.id == 0)
            {
                Debug.LogError($"Item {itemData.ConfigId} does not exist in shop config");
                return;
            }
            //计算价值，给玩家涨金币
            var value = count * data.price * data.sellPrice;
            var goldCommand = new GoldChangedCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item, CommandAuthority.Server, CommandExecuteType.Immediate),
                Gold = value
            };
            Constant.GameSyncManager.EnqueueServerCommand(goldCommand);
            
            //从玩家背包中扣除物品
            var itemSelleCommand = new ItemsSellCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item, CommandAuthority.Server, CommandExecuteType.Immediate),
                Slots = new SlotIndexData[]
                {
                    new SlotIndexData
                    {
                        SlotIndex = slotIndex,
                        Count = count
                    }
                }
            };
            Constant.GameSyncManager.EnqueueServerCommand(itemSelleCommand);

        }

        public static void CommandRefreshItem(ref PlayerShopState state, int connectionId, bool isServer = false)
        {
            var propertySystem = Constant.GameSyncManager.GetSyncSystem<PlayerPropertySyncSystem>(CommandType.Property);
            if (propertySystem == null)
                return;
            var costGold = Constant.ShopConfig.GetShopConstantData().onceCostGold;
            if (!propertySystem.TryUseGold(connectionId, costGold, out var remaining))
            {
                Debug.Log($"Player {connectionId} has not enough gold to refresh shop items. current gold is {remaining + costGold}, needed {costGold}");
                return;
            }
            var shopConfigIds = state.RandomShopItemsDict.Values.Select(x => x.ShopConfigId).ToHashSet();
            var newShopData = GetRandomShopItemData(shopConfigIds);
            state.RandomShopItemsDict = newShopData.ToDictionary(x => x.ShopId, x => x);
            if (!isServer)
                return;
            var command = new GoldChangedCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item,
                    CommandAuthority.Server, CommandExecuteType.Immediate),
                Gold = -costGold
            };
            Constant.GameSyncManager.EnqueueServerCommand(command);
        }
        
        private static void RefreshShopItems(ref ShopItemData[] playerShopData, ShopItemData randomShopItemData, ShopItemData otherShopItemData)
        {
            var newConfigId = Constant.ShopConfig.GetRandomItem(randomShopItemData.ShopConfigId, otherShopItemData.ShopConfigId, randomShopItemData.ItemType);
            var newRandomConfigData = Constant.ShopConfig.GetShopConfigData(newConfigId);
            var hashSet = playerShopData.ToHashSet();
            hashSet.Remove(randomShopItemData);
            hashSet.Add(new ShopItemData
            {
                ShopId = HybridIdGenerator.GenerateChestId(newConfigId, GameSyncManager.CurrentTick),
                ShopConfigId = newConfigId,
                RemainingCount = newRandomConfigData.maxCount,
                ItemType = newRandomConfigData.playerItemType
            });
            playerShopData = hashSet.ToArray();
        }
    }

    public class ShopCalculatorConstant
    {
        public GameSyncManager GameSyncManager;
        public ShopConfig ShopConfig;
        public ItemConfig ItemConfig;
        public bool IsServer;
    }
}