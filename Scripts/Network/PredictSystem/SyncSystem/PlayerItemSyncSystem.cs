﻿using System;
using System.Collections.Generic;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using MemoryPack;
using Mirror;
using UnityEngine;
using VContainer;
using Object = UnityEngine.Object;

namespace HotUpdate.Scripts.Network.PredictSystem.SyncSystem
{
    public class PlayerItemSyncSystem : BaseSyncSystem
    {
        private readonly Dictionary<int, PlayerItemPredictableState> _playerItemSyncStates = new Dictionary<int, PlayerItemPredictableState>();
        private ItemConfig _itemConfig;
        private WeaponConfig _weaponConfig;
        private ArmorConfig _armorConfig;
        private InteractSystem _interactSystem;
        protected override CommandType CommandType => CommandType.Item;

        [Inject]
        private void Init(IConfigProvider configProvider)
        {
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            _weaponConfig = configProvider.GetConfig<WeaponConfig>();
            _armorConfig = configProvider.GetConfig<ArmorConfig>();
            _interactSystem = Object.FindObjectOfType<InteractSystem>();
        }

        protected override void OnClientProcessStateUpdate(int connectionId, byte[] state, CommandType commandType)
        {
            if (commandType != CommandType.Item)
            {
                return;
            }
            var playerStates = NetworkCommandExtensions.DeserializePlayerState(state);
            // if (playerStates is not PlayerItemState playerItemState)
            // {
            //     Debug.LogError($"Player {playerStates.GetStateType().ToString()} item state is not PlayerItemState.");
            //     return;
            // }

            if (PropertyStates.ContainsKey(connectionId))
            {
                PropertyStates[connectionId] = playerStates;
            }
        }

        protected override void RegisterState(int connectionId, NetworkIdentity player)
        {
            var playerPredictableState = player.GetComponent<PlayerItemPredictableState>();
            var state = GetPlayerItemState();
            playerPredictableState.RegisterState(GetPlayerItemState());
            PropertyStates.TryAdd(connectionId, state);
            _playerItemSyncStates.TryAdd(connectionId, playerPredictableState);
            RpcSetPlayerItemState(connectionId, NetworkCommandExtensions.SerializePlayerState(state));
            
        }

        [ClientRpc]
        private void RpcSetPlayerItemState(int connectionId, byte[] playerItemState)
        {
            var syncState = NetworkServer.connections[connectionId].identity.GetComponent<PlayerItemPredictableState>();
            var playerState = NetworkCommandExtensions.DeserializePlayerState(playerItemState);
            syncState.InitCurrentState(playerState);
        }

        private PlayerItemState GetPlayerItemState()
        {
            var playerItemState = new PlayerItemState();
            PlayerItemState.Init(ref playerItemState, _itemConfig.MaxBagSize);
            return playerItemState;
        }
        
        public Dictionary<int, PlayerBagSlotItem> GetPlayerBagSlotItems(int connectionId)
        {
            var playerItemState = GetState<PlayerItemState>(connectionId);
            return playerItemState.PlayerItemConfigIdSlotDictionary;
        }

        public override CommandType HandledCommandType => CommandType.Item;
        public override ISyncPropertyState ProcessCommand(INetworkCommand command)
        {
            var header = command.GetHeader();
            var itemState = PropertyStates[header.ConnectionId];
            if (itemState is not PlayerItemState playerItemState)
            {
                Debug.LogError("PlayerItemState not found");
                return null;
            }
            switch (command)
            {
                case ItemsGetCommand itemsGetCommand:
                    for (var i = 0; i < itemsGetCommand.Items.Count; i++)
                    {
                        PlayerItemCalculator.CommandGetItem(ref playerItemState, itemsGetCommand.Items[i], header);
                    }
                    break;
                case ItemsUseCommand itemUseCommand:
                    PlayerItemCalculator.CommandUseItems(itemUseCommand, ref playerItemState);
                    break;
                case ItemEquipCommand itemEquipCommand:
                    PlayerItemCalculator.CommandEquipItem(itemEquipCommand, ref playerItemState, header.ConnectionId);
                    break;
                case ItemLockCommand itemLockCommand:
                    PlayerItemCalculator.CommandLockItem(itemLockCommand, ref playerItemState);
                    break;
                case ItemDropCommand itemDropCommand:
                    PlayerItemCalculator.CommandDropItem(itemDropCommand, ref playerItemState , header.ConnectionId);
                    break;
                case ItemsBuyCommand itemBuyCommand:
                    PlayerItemCalculator.CommandBuyItem(itemBuyCommand, ref playerItemState);
                    break;
                case ItemsSellCommand itemSellCommand:
                    PlayerItemCalculator.CommandSellItem(itemSellCommand, ref playerItemState, header.ConnectionId);
                    break;
                case ItemExchangeCommand itemExchangeCommand:
                    PlayerItemCalculator.CommandExchangeItem(itemExchangeCommand, ref playerItemState);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
            PropertyStates[header.ConnectionId] = playerItemState;
            return playerItemState;
        }


        public override byte[] GetPlayerSerializedState(int connectionId)
        {
            if (PropertyStates.TryGetValue(connectionId, out var playerState))
            {
                if (playerState is PlayerItemState playerItemState)
                {
                    return NetworkCommandExtensions.SerializePlayerState(playerItemState);
                }

                Debug.LogError($"Player {connectionId} equipment state is not PlayerItemState.");
                return null;
            }
            Debug.LogError($"Player {connectionId} equipment state not found.");
            return null;
        }

        public override void SetState<T>(int connectionId, T state)
        {
            var playerPredictableState = _playerItemSyncStates[connectionId];
            playerPredictableState.ApplyServerState(state);
        }

        public override bool HasStateChanged(ISyncPropertyState oldState, ISyncPropertyState newState)
        {
            return false;
        }

        public override void Clear()
        {
            base.Clear();
            _playerItemSyncStates.Clear();
        }

        public static int CreateItemId(int configId)
        {
            return HybridIdGenerator.GenerateItemId(configId, GameSyncManager.CurrentTick);
        }
        
        public static int CreateChestId(int configId)
        {
            return HybridIdGenerator.GenerateChestId(configId, GameSyncManager.CurrentTick);
        }
    }
}