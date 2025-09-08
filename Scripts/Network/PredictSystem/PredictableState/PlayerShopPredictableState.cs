using System.Collections.Generic;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool.Static;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerShopPredictableState : PredictableStateBase
    {
        private ShopConfig _shopConfig;
        private ItemConfig _itemConfig;
        private ConstantBuffConfig _constantBuffConfig;
        private RandomBuffConfig _randomBuffConfig;
        private BindingKey _bindKey;
        private BindingKey _bagBindKey;
        private PlayerInGameManager _playerInGameManager;
        private PropertyPredictionState _propertyPredictionState;
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Shop;
        
        [Inject]
        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _shopConfig = configProvider.GetConfig<ShopConfig>();
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            _propertyPredictionState = GetComponent<PropertyPredictionState>();
            _constantBuffConfig = configProvider.GetConfig<ConstantBuffConfig>();
            _randomBuffConfig = configProvider.GetConfig<RandomBuffConfig>();
            _playerInGameManager = PlayerInGameManager.Instance;
            _bindKey = new BindingKey(UIPropertyDefine.ShopItem);
            _bagBindKey = new BindingKey(UIPropertyDefine.BagItem);
            if (CurrentState is not PlayerShopState playerShopState)
            {
                return;
            }
            OnPlayerShopStateChanged(playerShopState);
        }

        public override void ApplyServerState<T>(T state)
        {
            if (state is not PlayerShopState playerShopState)
            {
                return;
            }
            base.ApplyServerState(state);
            OnPlayerShopStateChanged(playerShopState);
        }

        public void SetPlayerShopState(PlayerShopState state)
        {
            //OnPlayerShopStateChanged(state);
        }
        
        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is not null && state is PlayerShopState playerShopState)
            {
                return !playerShopState.Equals(CurrentState);
            }
            return true;
        }

        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType.HasAnyState(CommandType) || CurrentState is not PlayerShopState playerShopState)
            {
                return;
            }

            switch (command)
            {
                case BuyCommand buyCommand:
                    PlayerShopCalculator.CommandBuyItem(ref playerShopState, header.ConnectionId, buyCommand.ShopId, buyCommand.Count);
                    break;
                case RefreshShopCommand:
                    PlayerShopCalculator.CommandRefreshItem(ref playerShopState, header.ConnectionId);
                    break;
                case SellCommand sellCommand:
                    PlayerShopCalculator.CommandSellItem(header.ConnectionId, sellCommand.ItemSlotIndex, sellCommand.Count);
                    break;
            }
            OnPlayerShopStateChanged(playerShopState);
        }

        private void OnPlayerShopStateChanged(PlayerShopState playerShopState)
        {
            if(!isLocalPlayer)
               return;
            CurrentState = playerShopState;
            var shopItems = UIPropertyBinder.GetReactiveDictionary<RandomShopItemData>(_bindKey);
            foreach (var kvp in playerShopState.RandomShopItems)
            {
                var item = kvp.Value;
                var shopConfigData = _shopConfig.GetShopConfigData(item.ShopConfigId);
                var itemConfigData = _itemConfig.GetGameItemData(item.ItemConfigId);
                var skillId = PlayerItemCalculator.GetEquipSkillId(itemConfigData.itemType, itemConfigData.id);
                var equipBattleConfigData = PlayerItemCalculator.GetBattleEffectConditionConfigData(itemConfigData.id);

                string mainProperty = "";
                if (itemConfigData.itemType.IsEquipment())
                {
                    for (int i = 0; i < itemConfigData.buffExtraData.Length; i++)
                    {
                        var buff = _constantBuffConfig.GetBuffData(itemConfigData.buffExtraData[i].buffId);
                        mainProperty += GameStaticExtensions.GetPropertyDesc(buff);
                    }
                }
                else if (itemConfigData.itemType == PlayerItemType.Consume)
                {
                    mainProperty = itemConfigData.desc;
                }
                var randomBuffEffectDesc = "";
                var randomShopData = new RandomShopItemData();
                randomShopData.ShopConfigId = item.ShopConfigId;
                randomShopData.ShopId = item.ShopId;
                randomShopData.ItemConfigId = item.ItemConfigId;
                randomShopData.PlayerId = _playerInGameManager.LocalPlayerId;
                randomShopData.ItemType = item.ItemType;
                randomShopData.Price = item.Price;
                randomShopData.Icon = UISpriteContainer.GetSprite(itemConfigData.iconName);
                randomShopData.QualityIcon = UISpriteContainer.GetQualitySprite(itemConfigData.quality);
                randomShopData.Name = shopConfigData.name;
                randomShopData.MaxCount = shopConfigData.maxCount;
                randomShopData.RemainingCount = item.RemainingCount;
                randomShopData.QualityType = item.Quality;
                randomShopData.SellPrice = item.SellPrice;
                randomShopData.MainProperty = mainProperty ?? "";
                randomShopData.RandomProperty = randomBuffEffectDesc ?? "";
                randomShopData.SkillDescription = PlayerItemCalculator.GetSkillDescription(skillId, _propertyPredictionState.PlayerPredictablePropertyState.MemoryProperty);
                randomShopData.PassiveDescription = item.ItemType.IsEquipment() && equipBattleConfigData.id != 0
                    ? _shopConfig.GetShopConstantData().shopEquipPassiveDescription
                    : ""; 
                randomShopData.OnBuyItem = OnBuyItem;
                //Debug.Log($"Add item {item.ItemConfigId} {item.ShopConfigId} {shopConfigData.name} {item.ItemType} to shop");
                if (!shopItems.ContainsKey(kvp.Key))
                {
                    shopItems.Add(kvp.Key, randomShopData);
                    var removeKey = 0;
                    foreach (var shopItem in shopItems)
                    {
                        if (!playerShopState.RandomShopItems.ContainsKey(shopItem.Key))
                        {
                            removeKey = shopItem.Key;
                        }
                    }

                    if (removeKey != 0)
                    {
                        shopItems.Remove(removeKey);
                    }
                }
                else
                {
                    shopItems[kvp.Key] = randomShopData;
                }
            }
        }

        private void OnBuyItem(int shopId, int count)
        {
            if(!isLocalPlayer)
                return;
            var command = new BuyCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Shop,
                    CommandAuthority.Client),
                ShopId = shopId,
                Count = count
            };
            
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(command).Item1);
        }
    }
}