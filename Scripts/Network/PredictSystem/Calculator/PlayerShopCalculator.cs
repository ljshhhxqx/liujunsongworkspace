using System.Collections.Generic;
using System.Linq;
using Codice.CM.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Config.JsonConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.Server.InGame;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.Calculator
{
    public class PlayerShopCalculator : IPlayerStateCalculator
    {
        public static ShopCalculatorConstant Constant { get; private set; }

        public static void SetConstant(ShopCalculatorConstant constant)
        {
            Constant = constant;
        }
        
        private static ShopItemData CreateShopItemData(int shopConfigId)
        {
            var shopConfigData = Constant.ShopConfig.GetShopConfigData(shopConfigId);
            var itemConfigData = Constant.ItemConfig.GetGameItemData(shopConfigData.itemId);
            var attributeData = PlayerItemCalculator.GetAttributeIncreaseDatas(itemConfigData.buffExtraData);
            var mainAttributeData = new MemoryList<AttributeIncreaseData>(itemConfigData.buffExtraData.Length);
            var passiveAttributeData = new MemoryList<RandomAttributeIncreaseData>(itemConfigData.buffExtraData.Length);
            for (int i = 0; i < attributeData.Length; i++)
            {
                var expr = attributeData[i];
                if (expr is AttributeIncreaseData passiveAttributeIncreaseData)
                {
                    mainAttributeData.Add(passiveAttributeIncreaseData);
                }
                else if (expr is RandomAttributeIncreaseData passiveAttributeIncreaseData2)
                {
                    passiveAttributeData.Add(passiveAttributeIncreaseData2);
                }
            }

            var shopData = new ShopItemData
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
            return shopData;
        }

        public static ShopItemData[] GetRandomShopItemData(HashSet<int> preShopConfigIds = null)
        {
            var shopConfigIds = Constant.ShopConfig.RefreshShopItems(preShopConfigIds);
            var shopData = new ShopItemData[shopConfigIds.Count];
            var index = 0;
            foreach (var shopConfigId in shopConfigIds)
            {
                var randomShopData = CreateShopItemData(shopConfigId);
                shopData[index] = randomShopData;
                index++;
            }
            return shopData;
        }

        public static bool CanUseShop(int connectionId)
        {
            var playerPos = Constant.PlayerInGameManager.GetPlayerPosition(connectionId);
            var playerBase = Constant.PlayerInGameManager.GetPlayerBasePositionById(connectionId);
            var distance = Vector3.Distance(playerPos, playerBase);
            //Debug.Log($"[CanUseShop] Player {connectionId} distance to base is {distance}, playerPos: {playerPos}, playerBase: {playerBase}");
            return distance <= Constant.PlayerConfigData.MaxShopBuyDistance;
        }

        public static void CommandBuyItem(ref PlayerShopState state, int connectionId, int shopId, int count, bool isServer = false)
        {
            if (!state.RandomShopItems.TryGetValue(shopId, out var randomShopData))
            {
                Debug.LogError($"ShopId {shopId} does not exist in shop data");
                return;
            }
            var randomShopConfigData = Constant.ShopConfig.GetShopConfigData(randomShopData.ShopConfigId);
            var otherShopData = state.RandomShopItems.Last(x => x.Value.ItemType == randomShopData.ItemType && x.Key != randomShopData.ShopId);
            if (randomShopData.Equals(default) || randomShopData.RemainingCount < count)
                return;
            randomShopData.RemainingCount -= count;
            state.RandomShopItems[randomShopData.ShopId] = randomShopData;
            var randomShopItems = state.RandomShopItems;
            if (randomShopData.RemainingCount == 0)
            {
                RefreshShopItems(ref randomShopItems, randomShopData, otherShopData.Value);
            }
            state.RandomShopItems = randomShopItems;
            if (!Constant.IsServer)
                return;

            
            var itemsCommandData = new MemoryList<ItemsCommandData>(count);
            for (var i = 0; i < count; i++)
            {
                itemsCommandData.Add(new ItemsCommandData
                {
                    Count = 1,
                    ItemShopId = shopId,
                    ItemType = randomShopData.ItemType,
                    ItemConfigId = randomShopConfigData.itemId,
                    ItemUniqueId = new []{HybridIdGenerator.GenerateItemId(randomShopConfigData.itemId, GameSyncManager.CurrentTick)},
                });
            }
            var itemBuyCommand = new ItemsBuyCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item, CommandAuthority.Server, CommandExecuteType.Immediate),
                Items = itemsCommandData,
            };
            var totalPrice = randomShopConfigData.price * count;
            var goldScoreChangedCommand = new PropertyGetScoreGoldCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Property, CommandAuthority.Server, CommandExecuteType.Immediate),
                Gold = -(int)totalPrice,
            };
            
            Constant.GameSyncManager.EnqueueServerCommand(itemBuyCommand);
            Constant.GameSyncManager.EnqueueServerCommand(goldScoreChangedCommand);
        }

        public static void CommandSellItem(int connectionId, int slotIndex, int count, bool isServer = false)
        {
            if (!Constant.IsServer)
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
            var list = new MemoryList<SlotIndexData>(1);
            list[0] = new SlotIndexData
            {
                SlotIndex = slotIndex,
                Count = count
            };
            
            //从玩家背包中扣除物品
            var itemSelleCommand = new ItemsSellCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item, CommandAuthority.Server, CommandExecuteType.Immediate),
                Slots = list
            };
            Constant.GameSyncManager.EnqueueServerCommand(itemSelleCommand);

        }

        public static void RefreshShopFree(ref PlayerShopState state)
        {
            var shopConfigIds = state.RandomShopItems.Values.Select(x => x.ShopConfigId).ToHashSet();
            var newShopData = GetRandomShopItemData(shopConfigIds);
            var dic = newShopData.ToDictionary(x => x.ShopId, x => x);
            state.RandomShopItems = new MemoryDictionary<int, ShopItemData>(dic);
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
            var shopConfigIds = state.RandomShopItems.Values.Select(x => x.ShopConfigId).ToHashSet();
            var newShopData = GetRandomShopItemData(shopConfigIds);
            var dic = newShopData.ToDictionary(x => x.ShopId, x => x);
            state.RandomShopItems = new MemoryDictionary<int, ShopItemData>(dic);
            if (!Constant.IsServer)
                return;
            var command = new GoldChangedCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(connectionId, CommandType.Item,
                    CommandAuthority.Server, CommandExecuteType.Immediate),
                Gold = -costGold
            };
            Constant.GameSyncManager.EnqueueServerCommand(command);
        }
        
        private static void RefreshShopItems(ref MemoryDictionary<int, ShopItemData> playerShopData, ShopItemData randomShopItemData, ShopItemData otherShopItemData)
        {
            var newConfigId = Constant.ShopConfig.GetRandomItem(randomShopItemData.ShopConfigId, otherShopItemData.ShopConfigId, randomShopItemData.ItemType);
            playerShopData.Remove(randomShopItemData.ShopId);
            var newShopId = HybridIdGenerator.GenerateChestId(newConfigId, GameSyncManager.CurrentTick);
            var shopData = CreateShopItemData(newConfigId);
            playerShopData.Add(newShopId, shopData);
        }
    }

    public class ShopCalculatorConstant
    {
        public GameSyncManager GameSyncManager;
        public ShopConfig ShopConfig;
        public ItemConfig ItemConfig;
        public PlayerInGameManager PlayerInGameManager;
        public PlayerConfigData PlayerConfigData;
        public bool IsServer;
        public bool IsClient;
        public bool IsLocalPlayer;
    }
}