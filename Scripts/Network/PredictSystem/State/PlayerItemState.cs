using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.Tool.Static;
using MemoryPack;
using UnityEngine;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial struct PlayerItemState : ISyncPropertyState
    {
        [MemoryPackOrder(0)]
        private PlayerBagItem[] _items;

        [MemoryPackOrder(1)] 
        public int SlotCount;
        
        [MemoryPackIgnore]
        private Dictionary<EquipmentPart, PlayerEquipSlotItem> _playerEquipSlotItemsCache;
        
        // itemId-PlayerBagItem
        [MemoryPackIgnore]
        private Dictionary<int, PlayerBagItem> _playerItemsCache;
        
        // indexSlot-PlayerBagSlotItem
        [MemoryPackIgnore]
        private Dictionary<int, PlayerBagSlotItem> _playerSlotIndexItemConfigIdCache;
        
        public Dictionary<EquipmentPart, PlayerEquipSlotItem> PlayerEquipSlotItems
        {
            get
            {
                if (_playerEquipSlotItemsCache == null)
                {
                    RebuildCache();
                }
                return _playerEquipSlotItemsCache;
            }
            set
            {
                _playerEquipSlotItemsCache = value;
            }
        }

        [MemoryPackIgnore]
        public Dictionary<int, PlayerBagItem> PlayerItems
        {
            get
            {
                if (_playerItemsCache == null)
                {
                    RebuildCache();
                }
                return _playerItemsCache;
            }
            set
            {
                _playerItemsCache = value;
            }
        }
        
        [MemoryPackIgnore]
        public Dictionary<int, PlayerBagSlotItem> PlayerItemConfigIdSlotDictionary
        {
            get
            {
                if (_playerSlotIndexItemConfigIdCache == null)
                {
                    RebuildCache();
                }
                return _playerSlotIndexItemConfigIdCache;
            }
            set
            {
                _playerSlotIndexItemConfigIdCache = value;
            }
        }
        
        [MemoryPackOnSerializing]
        private void OnSerializing()
        {
            // 同步更新缓存
            if (_playerItemsCache != null)
            {
                _items = _playerItemsCache.Values.ToArray();
            }
        }

        [MemoryPackOnDeserialized]
        private void OnDeserialized()
        {
            RebuildCache();
        }

        private void RebuildCache()
        {
            _playerItemsCache = new Dictionary<int, PlayerBagItem>(
                _items?.Length ?? 0);
            _playerSlotIndexItemConfigIdCache = new Dictionary<int, PlayerBagSlotItem>(SlotCount);

            if (_items != null)
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    // _playerSlotIndexItemConfigIdCache[_items[i].IndexSlot] = _items[i].ConfigId;
                    if (_playerItemsCache.ContainsKey(_items[i].ItemId))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate property type: {_items[i]}");
                    }
                    _playerItemsCache[_items[i].ItemId] = _items[i];
                    if (!_playerSlotIndexItemConfigIdCache.ContainsKey(_items[i].IndexSlot))
                    {
                        _playerSlotIndexItemConfigIdCache[_items[i].IndexSlot] = new PlayerBagSlotItem
                        {
                            IndexSlot = _items[i].IndexSlot,
                            ConfigId = _items[i].ConfigId,
                            Count = 1,
                            ItemIds = new HashSet<int> {_items[i].ItemId},
                            MaxStack = _items[i].MaxStack,
                            PlayerItemType = _items[i].PlayerItemType,
                            State = _items[i].State,
                        };
                    }
                    else
                    {
                        var playerBagSlotItem = _playerSlotIndexItemConfigIdCache[_items[i].IndexSlot];
                        playerBagSlotItem.Count += 1;
                        playerBagSlotItem.ItemIds.Add(_items[i].ItemId);
                        _playerSlotIndexItemConfigIdCache[_items[i].IndexSlot] = playerBagSlotItem;
                    }

                    if (_items[i].PlayerItemType.IsEquipment() && _items[i].State == ItemState.IsEquipped)
                    {
                        var equipPart = _items[i].EquipmentPart;
                        if (PlayerEquipSlotItems.ContainsKey(equipPart))
                        {
                            throw new InvalidOperationException(
                                $"Duplicate property type: {_items[i]}");
                        }
                        PlayerEquipSlotItems[equipPart] = new PlayerEquipSlotItem
                        {
                            EquipmentPart = equipPart,
                            ItemId = _items[i].ItemId,
                            ConfigId = _items[i].ConfigId,
                        };
                    }
                }
            }
        }
        
        public static bool AddItems(ref PlayerItemState state, int[] itemIds, int configId, int maxStack, PlayerItemType itemType, ItemState itemState = ItemState.IsInBag, int count = 1)
        {
            if (TryGetSlotItemByConfigId( state, configId, out var slotItem) && slotItem.MaxStack > 1)
            {
                // 可堆叠物品 - 增加数量
                int remainingSpace = slotItem.MaxStack - slotItem.Count;
                if (remainingSpace >= count)
                {
                    slotItem.Count += count;
                    for (int i = 0; i < count; i++)
                    {
                        slotItem.ItemIds.Add(itemIds[i]);
                    }
                    UpdateSlotItem(ref state, slotItem);
                    return true;
                }

                slotItem.Count = slotItem.MaxStack;
                UpdateSlotItem(ref state, slotItem);
            }
            else
            {
                // 不可堆叠或新物品 - 创建新条目
                if (state.PlayerItemConfigIdSlotDictionary.Count >= state.SlotCount) 
                {
                    Debug.LogWarning("背包已满");
                    return false;
                }

                if (maxStack < count)
                {
                    Debug.LogWarning("获得物品数量超过最大堆叠数量");
                    return false;
                }

                var freeSlot = FindFreeSlotIndex(ref state); 

                var newItem = new PlayerBagItem
                {
                    ConfigId = configId,
                    PlayerItemType = itemType,
                    State = itemState,
                    IndexSlot = freeSlot,
                    MaxStack = maxStack 
                };

                var newSlotItem = new PlayerBagSlotItem
                {
                    IndexSlot = freeSlot, 
                    ConfigId = configId, 
                    Count = Mathf.Min(count, newItem.MaxStack), 
                    ItemIds = new HashSet<int>(), 
                    MaxStack = maxStack,
                    PlayerItemType = itemType,
                    State = itemState,
                };

                for (int i = 0; i < itemIds.Length; i++)
                {
                    newItem.ItemId = itemIds[i];
                    newSlotItem.ItemIds.Add(itemIds[i]);
                    state.PlayerItems.Add(itemIds[i], newItem);
                }
                state.PlayerItemConfigIdSlotDictionary.Add(freeSlot, newSlotItem);

                return true;
            }
            return false;
        }

        public static bool AddItem(ref PlayerItemState state, PlayerBagItem item)
        {
            // 检查是否是可堆叠物品
            if (TryGetSlotItemByConfigId( state, item.ConfigId, out var slotItem) && slotItem.MaxStack > 1)
            {
                // 可堆叠物品 - 增加数量
                int remainingSpace = slotItem.MaxStack - slotItem.Count;
                if (remainingSpace >= 1)
                {
                    slotItem.Count += 1;
                    slotItem.ItemIds.Add(item.ItemId);
                    UpdateSlotItem(ref state,slotItem);
                    return true;
                }

                slotItem.Count = slotItem.MaxStack;
                UpdateSlotItem(ref state, slotItem);
            }
            else
            {
                // 不可堆叠或新物品 - 创建新条目
                if (state.PlayerItemConfigIdSlotDictionary.Count >= state.SlotCount) 
                {
                    Debug.LogWarning("背包已满");
                    return false;
                }

                var freeSlot = FindFreeSlotIndex(ref state); // 需要实现查找空闲槽位的方法

                var newItem = new PlayerBagItem
                {
                    ItemId = item.ItemId,
                    ConfigId = item.ConfigId,
                    PlayerItemType = item.PlayerItemType,
                    State = item.State,
                    IndexSlot = freeSlot,
                    MaxStack = item.MaxStack,
                    EquipmentPart = item.EquipmentPart,
                };

                var newSlotItem = new PlayerBagSlotItem
                {
                    IndexSlot = freeSlot,
                    ConfigId = item.ConfigId,
                    Count = Mathf.Min(1, newItem.MaxStack),
                    ItemIds = new HashSet<int> {item.ItemId},
                    MaxStack = item.MaxStack,
                    PlayerItemType = item.PlayerItemType,
                    State = item.State,
                    
                };

                state.PlayerItems.Add(item.ItemId, newItem);
                state.PlayerItemConfigIdSlotDictionary.Add(freeSlot, newSlotItem);

                return true;
            }
            return false;
        }

        
        public static bool TryAddAndEquipItem(ref PlayerItemState state, PlayerBagItem bagItem)
        {
            if (!AddItem(ref state, bagItem))
            {
                return false;
            }
            if (bagItem.PlayerItemType.IsEquipment())
            {
                var equipPart = bagItem.EquipmentPart;
                if (state.PlayerEquipSlotItems.ContainsKey(equipPart))
                {
                    Debug.Log($"{equipPart}已经装备，存入背包");
                }
                state.PlayerEquipSlotItems[equipPart] = new PlayerEquipSlotItem
                {
                    EquipmentPart = equipPart,
                    ItemId = bagItem.ItemId,
                    ConfigId = bagItem.ConfigId,
                };
                return true;
            }
            return false;
        
        }

        public static bool RemoveItems(ref PlayerItemState state, int[] itemIds)
        {
            bool success = true;
            foreach (var itemId in itemIds)
            {
                if (!state.PlayerItems.Remove(itemId, out var item))
                {
                    success = false;
                    Debug.LogWarning($"物品不存在: {itemId}");
                    break;
                }
                if (item.State == ItemState.IsEquipped)
                {
                    var equipPart = item.EquipmentPart;
                    state.PlayerEquipSlotItems.Remove(equipPart);
                }
                var slotItem = state.PlayerItemConfigIdSlotDictionary[item.ConfigId];
                slotItem.Count -= 1;
                slotItem.ItemIds.Remove(itemId);
                if (slotItem.Count == 0)
                {
                    state.PlayerItemConfigIdSlotDictionary.Remove(item.ConfigId);
                }
                else
                {
                    state.PlayerItemConfigIdSlotDictionary[item.ConfigId] = slotItem;
                }
                state.PlayerItems.Remove(itemId);
            }
            return success;
        }

        public static bool RemoveItem(ref PlayerItemState state, int slotIndex, int count, out PlayerBagSlotItem slotItem)
        {
            if (!state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex, out slotItem))
            {
                Debug.LogWarning($"槽位物品不存在: {slotIndex}");
                return false;
            }
    
            if (slotItem.Count < count)
            {
                Debug.LogWarning($"物品数量不足: 需要{count}, 当前{slotItem.Count}");
                return false;
            }

            if (slotItem.State == ItemState.IsLocked)
            {
                Debug.LogWarning($"锁定的物品无法移除: {slotItem.State}");
                return false;
            }

            var itemId = slotItem.ItemIds.RandomSelects(count);
    
            // 更新数量或完全移除
            if (slotItem.Count > count)
            {
                slotItem.Count -= count;
                UpdateSlotItem(ref state, slotItem);
            }
            else
            {
                state.PlayerItemConfigIdSlotDictionary.Remove(slotIndex);
            }
            
            for (int i = 0; i < itemId.Length; i++)
            {
                state.PlayerItems.Remove(itemId[i]);
            }

    
            return true;
        }

        public static bool RemoveItemByConfigId(ref PlayerItemState state, int configId, int count = 1)
        {
            if (!TryGetSlotItemByConfigId(state, configId, out var slotItem))
            {
                Debug.LogWarning($"配置ID物品不存在: {configId}");
                return false;
            }
    
            return RemoveItem(ref state, slotItem.IndexSlot, count, out slotItem);
        }
        public static bool UpdateItemState(ref PlayerItemState state, int slotIndex, ItemState newState)
        {
            if ( !state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex, out var slotItem))
            {
                Debug.LogWarning($"物品槽位不存在: {slotIndex}");
                return false;
            }
            foreach (var itemId in slotItem.ItemIds)
            {
                if (state.PlayerItems.TryGetValue(itemId, out var item))
                {
                    if (item.State == newState)
                    {
                        Debug.LogWarning($"物品状态未改变: {item}");
                        return false;
                    }
                    if (newState == ItemState.IsEquipped )
                    {
                        
                        if (!item.PlayerItemType.IsEquipment())
                        {
                            Debug.LogWarning($"非装备物品不能装备: {item}");
                            return false;
                        }

                        var equipPart = item.EquipmentPart;
                        if (state.PlayerEquipSlotItems.ContainsKey(equipPart))
                        {
                            state.PlayerEquipSlotItems.Remove(equipPart);
                        }
                        state.PlayerEquipSlotItems[equipPart] = new PlayerEquipSlotItem
                        {
                            EquipmentPart = equipPart,
                            ItemId = item.ItemId,
                            ConfigId = item.ConfigId,
                        };
                    }
                    item.State = newState;
                    state.PlayerItems[itemId] = item;
                    slotItem.State = newState;
                    state.PlayerItemConfigIdSlotDictionary[slotItem.IndexSlot] = slotItem;
                }
            }
            return true;
        }

        // 交换物品到空闲槽位
        public static bool UpdateItemSlot(ref PlayerItemState state, int slotIndex, int newSlotIndex)
        {
            if (!state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex, out var oldSlotItem))
            {
                Debug.LogWarning($"源槽位物品不存在: {slotIndex}");
                return false;
            
            }

            if (state.PlayerItemConfigIdSlotDictionary.ContainsKey(newSlotIndex))
            {
                Debug.LogWarning($"目标槽位已被占用: {newSlotIndex}");
                return false;
            }

            foreach (var itemId in oldSlotItem.ItemIds)
            {
                var item = state.PlayerItems[itemId];
                item.IndexSlot = newSlotIndex;
                state.PlayerItems[itemId] = item;
            }
            state.PlayerItemConfigIdSlotDictionary[newSlotIndex] = oldSlotItem;
            state.PlayerItemConfigIdSlotDictionary.Remove(slotIndex);
            return true;
        }

        private static void UpdateSlotItem(ref PlayerItemState state, PlayerBagSlotItem slotItem)
        {
            state.PlayerItemConfigIdSlotDictionary[slotItem.IndexSlot] = slotItem;
        }

        public static bool TryGetSlotItemByConfigId(PlayerItemState state, int configId, out PlayerBagSlotItem slotItem)
        {
            slotItem = state.PlayerItemConfigIdSlotDictionary.Values
                .FirstOrDefault(x => x.ConfigId == configId);
            return !slotItem.Equals(default(PlayerBagSlotItem));
        }

        public static bool TryGetSlotItemBySlotIndex(PlayerItemState state, int slotIndex, out PlayerBagSlotItem slotItem)
        {
            return state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex, out slotItem);
        }

        public static bool TryGetEquipItemBySlotIndex(PlayerItemState state, int slotIndex,
            out PlayerBagItem bagItem)
        {
            if (state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex, out var slotItem))
            {
                return state.PlayerItems.TryGetValue(slotItem.ItemIds.First(), out bagItem);
            }
            bagItem = default(PlayerBagItem);
            return false;
        }

        public static bool TryGetItemByItemId(PlayerItemState state, int itemId, out PlayerBagItem item)
        {
            return state.PlayerItems.TryGetValue(itemId, out item);
        }

        public static int GetItemCount(PlayerItemState state, int configId)
        {
            if (TryGetSlotItemByConfigId(state, configId, out var slotItem))
            {
                return slotItem.Count;
            }
            return 0;
        }

        public static List<PlayerBagItem> GetAllItems(PlayerItemState state)
        {
            return state.PlayerItems.Values.ToList();
        }

        public static List<PlayerBagItem> GetItemsByType(PlayerItemState state,PlayerItemType type)
        {
            return state.PlayerItems.Values
                .Where(x => x.PlayerItemType == type)
                .ToList();
        }
        
        public static bool SwapItems(ref PlayerItemState state,int slotIndex1, int slotIndex2)
        {
            if (!state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex1, out var slotItem1) || 
               !state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex2, out var slotItem2))
            {
                Debug.LogWarning("交换物品中有不存在的物品");
                return false;
            }

            if (slotItem1.ConfigId == slotItem2.ConfigId)
            {
                Debug.LogWarning("只能交换不同配置ID的物品");
                return false;
            }
            foreach (var itemId in slotItem1.ItemIds)
            {
                var item = state.PlayerItems[itemId];
                item.IndexSlot = slotIndex2;
                state.PlayerItems[itemId] = item;
            }
            foreach (var itemId in slotItem2.ItemIds)
            {
                var item = state.PlayerItems[itemId];
                item.IndexSlot = slotIndex1;
                state.PlayerItems[itemId] = item;
            }
            (state.PlayerItemConfigIdSlotDictionary[slotIndex1], state.PlayerItemConfigIdSlotDictionary[slotIndex2]) = (state.PlayerItemConfigIdSlotDictionary[slotIndex2], state.PlayerItemConfigIdSlotDictionary[slotIndex1]);
            return true;
        }
        
        public static bool TryMergeItems(ref PlayerItemState state, int sourceSlot, int targetSlot)
        {
            if (!state.PlayerItemConfigIdSlotDictionary.TryGetValue(sourceSlot, out var sourceSlotItem) || 
               !state.PlayerItemConfigIdSlotDictionary.TryGetValue(targetSlot, out var targetSlotItem))
            {
                Debug.LogWarning("背包槽位不存在");
                return false;
            }
            
            if (sourceSlotItem.ConfigId != targetSlotItem.ConfigId)
            {
                Debug.LogWarning("只能合并相同配置ID的物品");
                return false;
            }

            int maxStack = sourceSlotItem.MaxStack;
            if (targetSlotItem.Count >= maxStack)
            {
                Debug.LogWarning("目标物品已满");
                return false;
            }
            
            // 计算可转移数量
            int transferAmount = Mathf.Min(sourceSlotItem.Count, maxStack - targetSlotItem.Count);
            
            // 更新目标物品数量
            targetSlotItem.Count += transferAmount;
            state.PlayerItemConfigIdSlotDictionary[targetSlotItem.IndexSlot] = targetSlotItem;
            
            // 未转移完全原物品
            if (sourceSlotItem.Count > transferAmount)
            {
                sourceSlotItem.Count -= transferAmount;
                var removedId = sourceSlotItem.ItemIds.RandomSelects(transferAmount);
                for (var i = 0; i < removedId.Length; i++)
                {
                    var item = state.PlayerItems[removedId[i]];
                    item.IndexSlot = targetSlot;
                    state.PlayerItems[removedId[i]] = item;
                }
                state.PlayerItemConfigIdSlotDictionary[sourceSlotItem.IndexSlot] = sourceSlotItem;
            }
            // 完全转移原物品
            else
            {
                foreach (var itemId in sourceSlotItem.ItemIds)
                {
                    var item = state.PlayerItems[itemId];
                    item.IndexSlot = targetSlot;
                    state.PlayerItems[itemId] = item;
                }
                state.PlayerItemConfigIdSlotDictionary.Remove(sourceSlot);
            }
            
            return true;
        }
        public static bool TrySplitItem(ref PlayerItemState state, int slotIndex, int splitCount)
        {
            if (!state.PlayerItemConfigIdSlotDictionary.TryGetValue(slotIndex, out var slotItem))
            {
                Debug.LogWarning("槽位物品不存在");
                return false;
            }
    
            if (slotItem.Count <= splitCount)
            {
                Debug.LogWarning("拆分数量必须小于当前数量");
                return false;
            }
    
            if (state.PlayerItems.Count >= state.SlotCount) // maxBagSize需要定义
            {
                Debug.LogWarning("背包已满，无法拆分");
                return false;
            }
    
            int freeSlot = FindFreeSlotIndex(ref state); // 需要实现查找空闲槽位的方法
            if (freeSlot == -1)
            {
                Debug.LogWarning("没有可用槽位");
                return false;
            }
    
            // 减少原物品数量
            var items = slotItem.ItemIds.RandomSelects(splitCount);
            
            for (var i = 0; i < items.Length; i++)
            {
                var item = state.PlayerItems[items[i]];
                item.IndexSlot = freeSlot;
                state.PlayerItems[items[i]] = item;
            }
            slotItem.Count -= splitCount;
            state.PlayerItemConfigIdSlotDictionary[slotIndex] = slotItem;
    
            var newSlotItem = new PlayerBagSlotItem
            {
                IndexSlot = freeSlot,
                ConfigId = slotItem.ConfigId,
                Count = splitCount,
                State = slotItem.State,
                PlayerItemType = slotItem.PlayerItemType,
                ItemIds = new HashSet<int>(items),
                MaxStack = slotItem.MaxStack,
                
            };
    
            state.PlayerItemConfigIdSlotDictionary.Add(freeSlot, newSlotItem);
    
            return true;
        }

        private static int FindFreeSlotIndex(ref PlayerItemState state)
        {
            // 查找第一个空闲的槽位
            for (int i = 0; i < state.SlotCount; i++)
            {
                if (!state.PlayerItemConfigIdSlotDictionary.ContainsKey(i))
                {
                    return i;
                }
            }
            return -1;
        }
    }

    [MemoryPackable]
    public partial struct PlayerBagItem : IEquatable<PlayerBagItem>
    {
        // 服务器唯一生成的id
        [MemoryPackOrder(0)]
        public int ItemId;
        [MemoryPackOrder(1)]
        public int ConfigId;
        // 物品类型
        [MemoryPackOrder(2)]
        public PlayerItemType PlayerItemType;
        // 物品的状态：锁定、装备中、空闲
        [MemoryPackOrder(3)]
        public ItemState State;
        [MemoryPackOrder(4)] 
        public int IndexSlot;
        [MemoryPackOrder(5)] 
        public int MaxStack;
        [MemoryPackOrder(6)]
        public EquipmentPart EquipmentPart;
        
        public PlayerBagItem(int itemId, int configId, PlayerItemType playerItemType, int maxStack, EquipmentPart equipmentPart = EquipmentPart.None, ItemState state = ItemState.IsInBag, int indexSlot = -1)
        {
            ItemId = itemId;
            ConfigId = configId;
            PlayerItemType = playerItemType;
            State = state;
            IndexSlot = indexSlot;
            MaxStack = maxStack;
            EquipmentPart = equipmentPart;
        }

        public bool Equals(PlayerBagItem other)
        {
            return ItemId == other.ItemId && EquipmentPart == other.EquipmentPart && ConfigId == other.ConfigId && PlayerItemType == other.PlayerItemType && State == other.State && IndexSlot == other.IndexSlot && MaxStack == other.MaxStack;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerBagItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(ItemId, ConfigId, (int)PlayerItemType, (int)State, IndexSlot, MaxStack);
        }
    }
    
    [MemoryPackable]
    public partial struct PlayerEquipSlotItem : IEquatable<PlayerEquipSlotItem>
    {
        [MemoryPackOrder(0)]
        public EquipmentPart EquipmentPart;
        [MemoryPackOrder(1)]
        public int ConfigId;
        [MemoryPackOrder(2)]
        public int ItemId;

        public bool Equals(PlayerEquipSlotItem other)
        {
            return EquipmentPart == other.EquipmentPart && ConfigId == other.ConfigId && ItemId == other.ItemId;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerEquipSlotItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine((int)EquipmentPart, ConfigId, ItemId);
        }
    }
    
    [MemoryPackable]
    public partial struct PlayerBagSlotItem : IEquatable<PlayerBagSlotItem>
    {
        [MemoryPackOrder(0)]
        public int IndexSlot;
        [MemoryPackOrder(1)]
        public int ConfigId;
        [MemoryPackOrder(2)]
        public int Count;
        [MemoryPackOrder(3)]
        public int MaxStack;
        [MemoryPackOrder(4)]
        public HashSet<int> ItemIds;
        [MemoryPackOrder(5)]
        public ItemState State;
        [MemoryPackOrder(6)]
        public PlayerItemType PlayerItemType;

        public bool Equals(PlayerBagSlotItem other)
        {
            return IndexSlot == other.IndexSlot && ConfigId == other.ConfigId && Count == other.Count && MaxStack == other.MaxStack;
        }

        public override bool Equals(object obj)
        {
            return obj is PlayerBagSlotItem other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(IndexSlot, ConfigId, Count, MaxStack);
        }
    }
}