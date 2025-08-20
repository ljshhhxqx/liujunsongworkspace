using System;
using System.Collections.Generic;
using System.Linq;
using AOTScripts.Data;
using HotUpdate.Scripts.Common;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Calculator;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Network.PredictSystem.State;
using HotUpdate.Scripts.Network.PredictSystem.SyncSystem;
using HotUpdate.Scripts.Network.PredictSystem.UI;
using HotUpdate.Scripts.Static;
using HotUpdate.Scripts.Tool.Static;
using HotUpdate.Scripts.UI.UIs.Panel.Item;
using MemoryPack;
using UniRx;
using UnityEngine;
using VContainer;

namespace HotUpdate.Scripts.Network.PredictSystem.PredictableState
{
    public class PlayerItemPredictableState : PredictableStateBase
    {
        protected override ISyncPropertyState CurrentState { get; set; }
        protected override CommandType CommandType => CommandType.Item;
        private ItemConfig _itemConfig;
        private BattleEffectConditionConfig _battleEffectConditionConfig;
        private BindingKey _itemBindKey;
        private BindingKey _equipBindKey;

        [Inject]
        protected override void Init(GameSyncManager gameSyncManager, IConfigProvider configProvider)
        {
            base.Init(gameSyncManager, configProvider);
            _itemConfig = configProvider.GetConfig<ItemConfig>();
            _battleEffectConditionConfig = configProvider.GetConfig<BattleEffectConditionConfig>();
            _itemBindKey = new BindingKey(UIPropertyDefine.BagItem);
            _equipBindKey = new BindingKey(UIPropertyDefine.EquipmentItem);
        }

        public override bool NeedsReconciliation<T>(T state)
        {
            if (state is not null && state is PlayerItemState playerItemState)
            {
                return !playerItemState.Equals(CurrentState);
            }
            return true;
        }

        public void RegisterState(PlayerItemState state)
        {
            //OnPlayerItemUpdate(state);
        }

        private PlayerItemState GetPlayerItemState()
        {
            if (CurrentState is not PlayerItemState playerItemState)
            {
                return default;
            }
            return playerItemState;
        }

        public override void Simulate(INetworkCommand command)
        {
            var header = command.GetHeader();
            if (header.CommandType.HasAnyState(CommandType) || CurrentState is not PlayerItemState playerItemState)
            {
                return;
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
                case ItemSkillEnableCommand itemSkillEnableCommand:
                    PlayerItemCalculator.CommandEnablePlayerSkill(ref playerItemState, itemSkillEnableCommand.SkillConfigId, itemSkillEnableCommand.SlotIndex, itemSkillEnableCommand.IsEnable, header.ConnectionId);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        public override void ApplyServerState<T>(T state)
        {
            if (state is not PlayerItemState playerItemState)
            {
                return;
            }
            base.ApplyServerState(playerItemState);
            CurrentState = playerItemState;
            OnPlayerItemUpdate(playerItemState);
        }

        private void OnUseItem(int slotIndex, int count)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var useItem = new SlotIndexData
            {
                SlotIndex = slotIndex,
                Count = count
            };
            var dic = new MemoryDictionary<int, SlotIndexData>(1);
            dic.Add(slotIndex, useItem);
            var useItemCommand = new ItemsUseCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = dic
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(useItemCommand).Item1);
        }

        private void OnEquipItem(int slotIndex, bool isEquip)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var state = GetPlayerItemState();
            var playerItemType = state.PlayerItemConfigIdSlotDictionary[slotIndex].PlayerItemType;
            var equipItemCommand = new ItemEquipCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                SlotIndex = slotIndex,
                PlayerItemType = playerItemType,
                IsEquip = isEquip
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(equipItemCommand).Item1);
        }

        private void OnLockItem(int slotIndex, bool isLock)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var lockItemCommand = new ItemLockCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                SlotIndex = slotIndex,
                IsLocked = isLock
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(lockItemCommand).Item1);
        }

        private void OnDropItem(int slotIndex, int count)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var dropItem = new SlotIndexData
            {
                SlotIndex = slotIndex,
                Count = count
            };
            var dic = new MemoryDictionary<int, SlotIndexData>(1);
            dic.Add(slotIndex, dropItem);
            var dropItemCommand = new ItemDropCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                Slots = dic
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(dropItemCommand).Item1);
        }

        private void OnExchangeItem(int fromSlotIndex, int toSlotIndex)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var exchangeItemCommand = new ItemExchangeCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Item, CommandAuthority.Client),
                FromSlotIndex = fromSlotIndex,
                ToSlotIndex = toSlotIndex
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(exchangeItemCommand).Item1);
        }

        private void OnSellItem(int slotIndex, int count)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var sellItemCommand = new SellCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Shop, CommandAuthority.Client),
                ItemSlotIndex = slotIndex,
                Count = count
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(sellItemCommand).Item1);
        }

        private void OnEnableSkill(int slotIndex, int skillId, bool isEnable)
        {
            if(!NetworkIdentity.isLocalPlayer)
                return;
            var enableCommand = new ItemSkillEnableCommand
            {
                Header = GameSyncManager.CreateNetworkCommandHeader(NetworkIdentity.connectionToClient.connectionId, CommandType.Item,
                    CommandAuthority.Client),
                SlotIndex = slotIndex,
                IsEnable = isEnable,
                SkillConfigId = skillId
            };
            GameSyncManager.EnqueueCommand(NetworkCommandExtensions.SerializeCommand(enableCommand).Item1);
        }
        
        private int nowCount = 0;

        private void OnPlayerItemUpdate(PlayerItemState playerItemState)
        {
            if (!NetworkIdentity.isLocalPlayer)
                return;
            //Debug.Log("OnPlayerItemUpdate");
            CurrentState = playerItemState;
            var bagItems = UIPropertyBinder.GetReactiveDictionary<BagItemData>(_itemBindKey);
            var isDebug = nowCount != bagItems.Count;
            nowCount = bagItems.Count;
            if (playerItemState.PlayerItemConfigIdSlotDictionary.Count == 0)
            {
                if (bagItems.Count > 0)
                {
                    bagItems.Clear();
                }
                return;
            }
            if (bagItems.Count > playerItemState.PlayerItemConfigIdSlotDictionary.Count)
            {
                var removeKey = 0;
                foreach (var kvp in bagItems)
                {
                    if (!playerItemState.PlayerItemConfigIdSlotDictionary.ContainsKey(kvp.Key))
                    {
                        removeKey = kvp.Key;
                    }
                }

                if (removeKey != 0)
                {
                    bagItems.Remove(removeKey);
                }
            }

            foreach (var kvp in playerItemState.PlayerItemConfigIdSlotDictionary)
            {
                var playerBagSlotItem = kvp.Value;
                var itemConfig = _itemConfig.GetGameItemData(playerBagSlotItem.ConfigId);
                var battleCondition = PlayerItemCalculator.GetBattleEffectConditionConfigData(itemConfig.id);
                var mainProperty = GameStaticExtensions.GetBuffEffectDesc(playerBagSlotItem.MainIncreaseDatas, true);
                var randomBuffEffectDesc = GameStaticExtensions.GetRandomBuffEffectDesc(playerBagSlotItem.RandomIncreaseDatas, true);
                var passiveProperty = GameStaticExtensions.GetBuffEffectDesc(playerBagSlotItem.PassiveAttributeIncreaseDatas, false, true);
                var conditionStr = "";
                if (itemConfig.itemType.IsEquipment() && !string.IsNullOrEmpty(passiveProperty) && battleCondition.triggerType != TriggerType.None && battleCondition.triggerType != TriggerType.Default)
                {
                    var increaseData = playerBagSlotItem.PassiveAttributeIncreaseDatas[0];
                    conditionStr = _battleEffectConditionConfig.ToLocalizedString(battleCondition,
                        new EquipmentPropertyData
                        {
                            propertyType = increaseData.header.propertyType,
                            increaseData = increaseData
                        });
                }

                var bagItem = new BagItemData
                {
                    ItemName = itemConfig.name,
                    Index = playerBagSlotItem.IndexSlot,
                    Stack = playerBagSlotItem.Count,
                    ConfigId = playerBagSlotItem.ConfigId,
                    Icon = UISpriteContainer.GetSprite(itemConfig.iconName),
                    QualityIcon = UISpriteContainer.GetQualitySprite(itemConfig.quality),
                    Description = itemConfig.desc,
                    PropertyDescription = mainProperty ?? "",
                    RandomDescription = randomBuffEffectDesc ?? "",
                    ConditionDescription = conditionStr,
                    PassiveDescription = passiveProperty,
                    PlayerItemType = itemConfig.itemType,
                    IsEquip = playerBagSlotItem.State == ItemState.IsEquipped,
                    IsLock = playerBagSlotItem.State == ItemState.IsLocked,
                    MaxStack = itemConfig.maxStack,
                    Price = itemConfig.price,
                    SellRatio = itemConfig.sellPriceRatio,
                    SkillId = playerBagSlotItem.SkillId,
                    IsEnable = playerBagSlotItem.IsEnableSkill,
                    EquipmentPart = playerBagSlotItem.EquipmentPart,
                    OnUseItem = OnUseItem,
                    OnDropItem = OnDropItem,
                    OnLockItem = OnLockItem,
                    OnEquipItem = OnEquipItem,
                    OnExchangeItem = OnExchangeItem,
                    OnSellItem = OnSellItem,
                    OnEnableSkill = OnEnableSkill,
                };
                if (isDebug)
                {
                    Debug.Log(bagItem.ToString());
                    // foreach (var increaseData in playerBagSlotItem.MainIncreaseDatas)
                    // {
                    //     Debug.Log(increaseData.ToString());
                    // }
                    // foreach (var increaseData in playerBagSlotItem.RandomIncreaseDatas)
                    // {
                    //     Debug.Log(increaseData.ToString());
                    // }
                    // foreach (var increaseData in playerBagSlotItem.PassiveAttributeIncreaseDatas)
                    // {
                    //     Debug.Log(increaseData.ToString());
                    // }
                }

                if (!bagItems.ContainsKey(kvp.Key))
                {
                    bagItems.Add(kvp.Key, bagItem);
                }
                else
                {
                    bagItems[kvp.Key] = bagItem;
                }
            }
        }
    }
}