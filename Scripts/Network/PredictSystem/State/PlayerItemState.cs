using System;
using System.Collections.Generic;
using System.Linq;
using Codice.Client.BaseCommands.Merge.Xml;
using MemoryPack;

namespace HotUpdate.Scripts.Network.PredictSystem.State
{
    [MemoryPackable]
    public partial struct PlayerItemState : ISyncPropertyState
    {
        [MemoryPackOrder(0)]
        private PlayerItem[] _items;

        // 添加字典缓存字段
        [MemoryPackIgnore]
        private Dictionary<uint, PlayerItem> _playerItemsCache;

        public Dictionary<uint, PlayerItem> PlayerItems
        {
            get
            {
                if (_playerItemsCache == null)
                {
                    RebuildCache();
                }
                return _playerItemsCache;
            }
            set => _playerItemsCache = value;
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
            _playerItemsCache = new Dictionary<uint, PlayerItem>(
                _items?.Length ?? 0);

            if (_items != null)
            {
                for (int i = 0; i < _items.Length; i++)
                {
                    // 添加重复键检测
                    if (_playerItemsCache.ContainsKey(_items[i].ItemId))
                    {
                        throw new InvalidOperationException(
                            $"Duplicate property type: {_items[i]}");
                    }
                    _playerItemsCache[_items[i].ItemId] = _items[i];
                }
            }
        }
    }

    [MemoryPackable]
    public partial struct PlayerItem
    {
        // 服务器唯一生成的id
        [MemoryPackOrder(0)]
        public uint ItemId;
        [MemoryPackOrder(1)]
        public int ItemCount;
        [MemoryPackOrder(2)]
        public int ConfigId;
        [MemoryPackOrder(3)]
        public ItemType ItemType;
    }
}