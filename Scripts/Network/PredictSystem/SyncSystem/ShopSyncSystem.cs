﻿using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using Mirror;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class ShopSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerShopPredictableState> _playerShopSyncStates = new Dictionary<int, PlayerShopPredictableState>();
        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerShopState>>(state);
            foreach (var playerState in playerStates)
            {
                if (!PropertyStates.ContainsKey(playerState.Key))
                {
                    continue;
                }
                PropertyStates[playerState.Key] = playerState.Value;
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerShopPredictableState>();
            var state = new PlayerShopState();
            var randomItems = PlayerShopCalculator.GetRandomShopItemData();
            state.RandomShopItemsDict = randomItems.ToDictionary(x => x.ShopId, x => x);
            playerPredictableState.SetPlayerShopState(state);
            PropertyStates[connectionId] = state;
            _playerShopSyncStates[connectionId] = playerPredictableState;
            
        }

        public override CommandType HandledCommandType => CommandType.Shop;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            var itemState = PropertyStates[header.ConnectionId];
            if (itemState is not PlayerShopState shopState)
            {
                Debug.LogError("PlayerItemState not found");
                return null;
            }

            switch (command)
            {
                case BuyCommand buyCommand:
                    PlayerShopCalculator.CommandBuyItem(ref shopState, header.ConnectionId, buyCommand.ShopId, buyCommand.Count, true);
                    break;
                case RefreshShopCommand:
                    PlayerShopCalculator.CommandRefreshItem(ref shopState, header.ConnectionId, true);
                    break;
                case SellCommand sellCommand:
                    PlayerShopCalculator.CommandSellItem(header.ConnectionId, sellCommand.ItemSlotIndex, sellCommand.Count, true);
                    break;
            }
            PropertyStates[header.ConnectionId] = shopState;
            return shopState;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _playerShopSyncStates[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return true;
        }

        public override void Clear()
        {
            base.Clear();
            _playerShopSyncStates.Clear();
        }
    }
}