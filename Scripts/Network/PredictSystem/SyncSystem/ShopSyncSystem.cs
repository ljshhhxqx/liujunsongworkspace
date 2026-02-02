using System.Collections.Generic;
using AOTScripts.Data;
using AOTScripts.Data.State;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Static;
using Mirror;
using UnityEngine;
using VContainer;
using ISyncPropertyState = HotUpdate.Scripts.Network.State.ISyncPropertyState;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class ShopSyncSystem : BaseSyncSystem
    {
        private ShopConfig _shopConfig;
        private ItemConfig _itemConfig;
        private readonly Dictionary<int, PlayerShopPredictableState> _playerShopSyncStates = new Dictionary<int, PlayerShopPredictableState>();
        protected override CommandType CommandType => CommandType.Shop;
        
        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _shopConfig = configProvider.GetConfig<ShopConfig>();
            _itemConfig = configProvider.GetConfig<ItemConfig>();
        }
        

        protected override void OnClientProcessStateUpdate(int connectionId, byte[] state, CommandType commandType)
        {
            if (commandType != CommandType.Shop)
            {
                return;
            }
            var playerStates = NetworkCommandExtensions.DeserializePlayerState(state);
            
            // if (playerStates is not PlayerShopState playerShopState)
            // {
            //     Debug.LogError($"Player {playerStates.GetStateType().ToString()} shop state is not PlayerShopState.");
            //     return;
            // }
            if (PropertyStates.ContainsKey(connectionId))
            {
                PropertyStates[connectionId] = playerStates;
            }
        }

        protected override void RegisterState(int connectionId, uint netId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerShopPredictableState>();
            var state = new PlayerShopState();
            var randomItems = PlayerShopCalculator.GetRandomShopItemData();
            state.RandomShopItems = new State.MemoryDictionary<int, ShopItemData>();
            for (int i = 0; i < randomItems.Length; i++)
            {
                state.RandomShopItems.AddOrUpdate(randomItems[i].ShopId, randomItems[i]);
            }
            playerPredictableState.SetPlayerShopState(state);
            PropertyStates[connectionId] = state;
            _playerShopSyncStates[connectionId] = playerPredictableState;
            RpcSetPlayerShopState(connectionId, netId, NetworkCommandExtensions.SerializePlayerState(state).Item1);
        }
        public override byte[] GetPlayerSerializedState(int connectionId)
        {
            if (PropertyStates.TryGetValue(connectionId, out var playerState))
            {
                if (playerState is PlayerShopState shopState)
                {
                    return NetworkCommandExtensions.SerializePlayerState(shopState).Item1;
                }

                Debug.LogError($"Player {connectionId} property state is not PlayerPredictablePropertyState.");
                return null;
            }
            Debug.LogError($"Player {connectionId} equipment state not found.");
            return null;
        }
        [ClientRpc]
        private void RpcSetPlayerShopState(int connectionId, uint netId, byte[] playerSkillState)
        {
            
            var player = GameSyncManager.GetPlayerConnection(netId);
            var syncState = player.GetComponent<PlayerShopPredictableState>();
            var playerState = NetworkCommandExtensions.DeserializePlayerState(playerSkillState);
            syncState.InitCurrentState(playerState);
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