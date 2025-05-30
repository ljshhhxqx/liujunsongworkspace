﻿using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.Item;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.Interact;
using HotUpdate.Scripts.Network.PredictSystem.PredictableState;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.Server.InGame;
using HotUpdate.Scripts.Tool.ObjectPool;
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
        private PlayerInGameManager _playerInGameManager;

        [Inject]
        private void Init(IConfigProvider configProvider, PlayerInGameManager playerInGameManager)
        {
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            _weaponConfig = configProvider.GetConfig<WeaponConfig>();
            _armorConfig = configProvider.GetConfig<ArmorConfig>();
            _playerInGameManager = playerInGameManager;
            _interactSystem = Object.FindObjectOfType<InteractSystem>();
        }

        protected override void OnClientProcessStateUpdate(byte[] state)
        {
            var playerStates = MemoryPackSerializer.Deserialize<Dictionary<int, PlayerItemState>>(state);
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
            var playerPredictableState = player.GetComponent<PlayerItemPredictableState>();
            var state = GetPlayerItemState();
            playerPredictableState.RegisterState(GetPlayerItemState());
            PropertyStates.Add(connectionId, state);
            _playerItemSyncStates.Add(connectionId, playerPredictableState);
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
                    for (var i = 0; i < itemsGetCommand.Items.Length; i++)
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
            return HybridIdGenerator.GenerateItemId(configId, SyncSystem.GameSyncManager.CurrentTick);
        }
        
        public static int CreateChestId(int configId)
        {
            return HybridIdGenerator.GenerateChestId(configId, SyncSystem.GameSyncManager.CurrentTick);
        }
    }
}