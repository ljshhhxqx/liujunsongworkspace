using System;
using System.Collections.Generic;
using System.Linq;
using HotUpdate.Scripts.Collector;
using HotUpdate.Scripts.Config.ArrayConfig;
using HotUpdate.Scripts.Network.PredictSystem.Data;
using HotUpdate.Scripts.UI.UIs.Common;
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

        // itemId-PlayerBagItem
        [MemoryPackIgnore]
        private Dictionary<int, PlayerBagItem> _playerItemsCache;
        
        //configid-PlayerBagSlotItem
        [MemoryPackIgnore]
        private Dictionary<int, PlayerBagSlotItem> _playerSlotIndexItemConfigIdCache;

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
                }
            }
        }
        
        public bool AddItem(int configId, int count, PlayerItemType type, ItemState state = ItemState.IsInBag)
        {
            // 检查是否是可堆叠物品
            if (TryGetSlotItemByConfigId(configId, out var slotItem) && slotItem.MaxStack > 1)
            {
                // 可堆叠物品 - 增加数量
                int remainingSpace = slotItem.MaxStack - slotItem.Count;
                if (remainingSpace >= count)
                {
                    slotItem.Count += count;
                    UpdateSlotItem(slotItem);
                    return true;
                }
                else
                {
                    slotItem.Count = slotItem.MaxStack;
                    UpdateSlotItem(slotItem);
                    return AddItem(configId, count - remainingSpace, type, state);
                }
            }
            else
            {
                // 不可堆叠或新物品 - 创建新条目
                if (_playerItemsCache.Count >= SlotCount) // maxBagSize需要定义
                {
                    Debug.LogWarning("背包已满");
                    return false;
                }
        
                int newItemId = GenerateNewItemId(); // 需要实现生成唯一ID的方法
                int freeSlot = FindFreeSlotIndex(); // 需要实现查找空闲槽位的方法
        
                var newItem = new PlayerBagItem
                {
                    ItemId = newItemId,
                    ConfigId = configId,
                    PlayerItemType = type,
                    State = state,
                    IndexSlot = freeSlot,
                    MaxStack = GetMaxStackForConfig(configId) // 需要实现获取配置最大堆叠数的方法
                };
        
                var newSlotItem = new PlayerBagSlotItem
                {
                    IndexSlot = freeSlot,
                    ConfigId = configId,
                    Count = Mathf.Min(count, newItem.MaxStack)
                };
        
                _playerItemsCache.Add(newItemId, newItem);
                _playerSlotIndexItemConfigIdCache.Add(freeSlot, newSlotItem);
        
                // 如果还有剩余数量，递归添加
                if (count > newItem.MaxStack)
                {
                    return AddItem(configId, count - newItem.MaxStack, type, state);
                }
        
                return true;
            }
        }
        public bool RemoveItem(int itemId, int count = 1)
        {
            if (!_playerItemsCache.TryGetValue(itemId, out var item))
            {
                Debug.LogWarning($"物品不存在: {itemId}");
                return false;
            }
    
            if (!_playerSlotIndexItemConfigIdCache.TryGetValue(item.IndexSlot, out var slotItem))
            {
                Debug.LogWarning($"槽位物品不存在: {item.IndexSlot}");
                return false;
            }
    
            if (slotItem.Count < count)
            {
                Debug.LogWarning($"物品数量不足: 需要{count}, 当前{slotItem.Count}");
                return false;
            }
    
            // 更新数量或完全移除
            if (slotItem.Count > count)
            {
                slotItem.Count -= count;
                UpdateSlotItem(slotItem);
            }
            else
            {
                _playerItemsCache.Remove(itemId);
                _playerSlotIndexItemConfigIdCache.Remove(item.IndexSlot);
            }
    
            return true;
        }

        public bool RemoveItemByConfigId(int configId, int count = 1)
        {
            if (!TryGetSlotItemByConfigId(configId, out var slotItem))
            {
                Debug.LogWarning($"配置ID物品不存在: {configId}");
                return false;
            }
    
            // 找到对应的PlayerBagItem
            var item = _playerItemsCache.Values.FirstOrDefault(x => 
                x.ConfigId == configId && x.IndexSlot == slotItem.IndexSlot);
    
            if (item.Equals(default(PlayerBagItem)))
            {
                Debug.LogWarning($"找不到对应的PlayerBagItem: {configId}");
                return false;
            }
    
            return RemoveItem(item.ItemId, count);
        }
        public bool UpdateItemState(int itemId, ItemState newState)
        {
            if (!_playerItemsCache.TryGetValue(itemId, out var item))
            {
                Debug.LogWarning($"物品不存在: {itemId}");
                return false;
            }
    
            item.State = newState;
            _playerItemsCache[itemId] = item;
            return true;
        }

        public bool UpdateItemSlot(int itemId, int newSlotIndex)
        {
            if (!_playerItemsCache.TryGetValue(itemId, out var item))
            {
                Debug.LogWarning($"物品不存在: {itemId}");
                return false;
            }
    
            if (_playerSlotIndexItemConfigIdCache.ContainsKey(newSlotIndex))
            {
                Debug.LogWarning($"目标槽位已被占用: {newSlotIndex}");
                return false;
            }
    
            // 更新PlayerBagItem
            item.IndexSlot = newSlotIndex;
            _playerItemsCache[itemId] = item;
    
            // 更新PlayerBagSlotItem
            if (_playerSlotIndexItemConfigIdCache.TryGetValue(item.IndexSlot, out var slotItem))
            {
                _playerSlotIndexItemConfigIdCache.Remove(item.IndexSlot);
                slotItem.IndexSlot = newSlotIndex;
                _playerSlotIndexItemConfigIdCache.Add(newSlotIndex, slotItem);
            }
    
            return true;
        }

        private void UpdateSlotItem(PlayerBagSlotItem slotItem)
        {
            _playerSlotIndexItemConfigIdCache[slotItem.IndexSlot] = slotItem;
        }
        public bool TryGetItem(int itemId, out PlayerBagItem item)
        {
            return _playerItemsCache.TryGetValue(itemId, out item);
        }

        public bool TryGetSlotItemByConfigId(int configId, out PlayerBagSlotItem slotItem)
        {
            slotItem = _playerSlotIndexItemConfigIdCache.Values
                .FirstOrDefault(x => x.ConfigId == configId);
            return !slotItem.Equals(default(PlayerBagSlotItem));
        }

        public bool TryGetSlotItemBySlotIndex(int slotIndex, out PlayerBagSlotItem slotItem)
        {
            return _playerSlotIndexItemConfigIdCache.TryGetValue(slotIndex, out slotItem);
        }

        public int GetItemCount(int configId)
        {
            if (TryGetSlotItemByConfigId(configId, out var slotItem))
            {
                return slotItem.Count;
            }
            return 0;
        }

        public List<PlayerBagItem> GetAllItems()
        {
            return _playerItemsCache.Values.ToList();
        }

        public List<PlayerBagItem> GetItemsByType(PlayerItemType type)
        {
            return _playerItemsCache.Values
                .Where(x => x.PlayerItemType == type)
                .ToList();
        }
        public bool SwapItems(int itemId1, int itemId2)
        {
            if (!_playerItemsCache.TryGetValue(itemId1, out var item1) || 
                !_playerItemsCache.TryGetValue(itemId2, out var item2))
            {
                Debug.LogWarning("交换物品中有不存在的物品");
                return false;
            }
    
            // 交换槽位
            (item1.IndexSlot, item2.IndexSlot) = (item2.IndexSlot, item1.IndexSlot);

            // 更新缓存
            _playerItemsCache[itemId1] = item1;
            _playerItemsCache[itemId2] = item2;
    
            // 更新SlotItem
            if (_playerSlotIndexItemConfigIdCache.TryGetValue(item1.IndexSlot, out var slotItem1))
            {
                slotItem1.IndexSlot = item1.IndexSlot;
                _playerSlotIndexItemConfigIdCache[item1.IndexSlot] = slotItem1;
            }
    
            if (_playerSlotIndexItemConfigIdCache.TryGetValue(item2.IndexSlot, out var slotItem2))
            {
                slotItem2.IndexSlot = item2.IndexSlot;
                _playerSlotIndexItemConfigIdCache[item2.IndexSlot] = slotItem2;
            }
    
            return true;
        }
        public bool TryMergeItems(int sourceItemId, int targetItemId)
        {
            if (!_playerItemsCache.TryGetValue(sourceItemId, out var sourceItem) || 
                !_playerItemsCache.TryGetValue(targetItemId, out var targetItem))
            {
                Debug.LogWarning("合并物品中有不存在的物品");
                return false;
            }
    
            if (sourceItem.ConfigId != targetItem.ConfigId)
            {
                Debug.LogWarning("只能合并相同配置ID的物品");
                return false;
            }
    
            if (!_playerSlotIndexItemConfigIdCache.TryGetValue(sourceItem.IndexSlot, out var sourceSlot) || 
                !_playerSlotIndexItemConfigIdCache.TryGetValue(targetItem.IndexSlot, out var targetSlot))
            {
                Debug.LogWarning("槽位信息缺失");
                return false;
            }
    
            int totalCount = sourceSlot.Count + targetSlot.Count;
            int maxStack = sourceItem.MaxStack;
    
            if (targetSlot.Count >= maxStack)
            {
                Debug.LogWarning("目标物品已满");
                return false;
            }
    
            // 计算可转移数量
            int transferAmount = Mathf.Min(sourceSlot.Count, maxStack - targetSlot.Count);
    
            // 更新目标物品数量
            targetSlot.Count += transferAmount;
            _playerSlotIndexItemConfigIdCache[targetItem.IndexSlot] = targetSlot;
    
            // 更新或移除源物品
            if (sourceSlot.Count > transferAmount)
            {
                sourceSlot.Count -= transferAmount;
                _playerSlotIndexItemConfigIdCache[sourceItem.IndexSlot] = sourceSlot;
            }
            else
            {
                _playerItemsCache.Remove(sourceItemId);
                _playerSlotIndexItemConfigIdCache.Remove(sourceItem.IndexSlot);
            }
    
            return true;
        }
        public bool TrySplitItem(int itemId, int splitCount)
        {
            if (!_playerItemsCache.TryGetValue(itemId, out var item))
            {
                Debug.LogWarning("物品不存在");
                return false;
            }
    
            if (!_playerSlotIndexItemConfigIdCache.TryGetValue(item.IndexSlot, out var slotItem))
            {
                Debug.LogWarning("槽位物品不存在");
                return false;
            }
    
            if (slotItem.Count <= splitCount)
            {
                Debug.LogWarning("拆分数量必须小于当前数量");
                return false;
            }
    
            if (_playerItemsCache.Count >= SlotCount) // maxBagSize需要定义
            {
                Debug.LogWarning("背包已满，无法拆分");
                return false;
            }
    
            int freeSlot = FindFreeSlotIndex(); // 需要实现查找空闲槽位的方法
            if (freeSlot == -1)
            {
                Debug.LogWarning("没有可用槽位");
                return false;
            }
    
            // 减少原物品数量
            slotItem.Count -= splitCount;
            _playerSlotIndexItemConfigIdCache[item.IndexSlot] = slotItem;
    
            // 创建新物品
            int newItemId = GenerateNewItemId();
            var newItem = new PlayerBagItem
            {
                ItemId = newItemId,
                ConfigId = item.ConfigId,
                PlayerItemType = item.PlayerItemType,
                State = ItemState.IsInBag,
                IndexSlot = freeSlot,
                MaxStack = item.MaxStack
            };
    
            var newSlotItem = new PlayerBagSlotItem
            {
                IndexSlot = freeSlot,
                ConfigId = item.ConfigId,
                Count = splitCount
            };
    
            _playerItemsCache.Add(newItemId, newItem);
            _playerSlotIndexItemConfigIdCache.Add(freeSlot, newSlotItem);
    
            return true;
        }
        private int GenerateNewItemId()
        {
            // 简单实现 - 实际项目中可能需要更复杂的ID生成逻辑
            return _playerItemsCache.Count > 0 ? _playerItemsCache.Keys.Max() + 1 : 1;
        }

        private int FindFreeSlotIndex()
        {
            // 查找第一个空闲的槽位
            for (int i = 0; i < SlotCount; i++)
            {
                if (!_playerSlotIndexItemConfigIdCache.ContainsKey(i))
                {
                    return i;
                }
            }
            return -1;
        }

        private int GetMaxStackForConfig(int configId)
        {
            // 这里应该从配置表获取最大堆叠数
            // 简单实现返回默认值
            return 99; // 或其他默认值
        }
        // public static void RemovePlayerItem(ref PlayerItemState state, int itemId)
        // {
        //     if (!state.PlayerItems.TryGetValue(itemId, out var playerBagItem))
        //     {
        //         Debug.LogError($"未找到物品{itemId}");
        //         return;
        //     }
        //     var dic = state.PlayerItemConfigIdSlotDictionary;
        //     if (dic.TryGetValue(playerBagItem.ConfigId, out var playerBagSlotItem))
        //     {
        //         playerBagSlotItem.Count--;
        //         if (playerBagSlotItem.Count <= 0)
        //         {
        //             dic.Remove(playerBagItem.ConfigId);
        //         }
        //     }
        //     state.PlayerItems.Remove(itemId);
        // }
        //
        // public static void AddPlayerItem(ref PlayerItemState state, PlayerBagItem playerBagItem)
        // {
        //     var dic = state.PlayerItemConfigIdSlotDictionary;
        //     var isNewSlot = false;
        //     var canStack = true;
        //     //先检查是否可堆叠
        //     if (dic.TryGetValue(playerBagItem.ConfigId, out var playerBagSlotItem))
        //     {
        //         isNewSlot = playerBagSlotItem.Count == playerBagItem.MaxStack;
        //     }
        //     //再检查是否有空闲槽位
        //     if (dic.Count == state.SlotCount)
        //     {
        //         canStack = false;
        //     }
        //
        //     if (!canStack)
        //     {
        //         // 没有空闲槽位，直接返回
        //         Debug.Log("玩家背包已满");
        //         return;
        //     }
        //
        //     if (isNewSlot)
        //     {
        //         for (int i = 0; i < state.SlotCount; i++)
        //         {
        //             if (dic.ContainsKey(i))
        //             {
        //                 continue;
        //             }
        //             playerBagItem.IndexSlot = i;
        //             dic[i] = new PlayerBagSlotItem
        //             {
        //                 IndexSlot = i,
        //                 ConfigId = playerBagItem.ConfigId,
        //                 Count = 1
        //             };
        //             break;
        //         }   
        //     }
        //     else
        //     {
        //         playerBagItem.IndexSlot = playerBagSlotItem.IndexSlot;
        //         playerBagSlotItem.Count++;
        //     }
        //     state.PlayerItems[playerBagItem.ItemId] = playerBagItem;
        //     state.PlayerItemConfigIdSlotDictionary[playerBagItem.ConfigId] = new PlayerBagSlotItem
        //     {
        //         IndexSlot = playerBagItem.IndexSlot,
        //         ConfigId = playerBagItem.ConfigId,
        //         Count = playerBagSlotItem.Count
        //     };
        // }
    }

    [MemoryPackable]
    public partial struct PlayerBagItem
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
        [MemoryPackOrder(4)] 
        public int MaxStack;
    }
    
    [MemoryPackable]
    public partial struct PlayerBagSlotItem
    {
        [MemoryPackOrder(0)]
        public int IndexSlot;
        [MemoryPackOrder(1)]
        public int ConfigId;
        [MemoryPackOrder(2)]
        public int Count;
        [MemoryPackOrder(2)]
        public int MaxStack;
    }
    
}