using System;
using System.Collections.Generic;
using System.Linq;
using MemoryPack;

namespace AOTScripts.Data.State
{
    /// <summary>
    /// 服务器强制同步内容
    /// </summary>
    [MemoryPackable(GenerateType.NoGenerate)]
    [MemoryPackUnion(0, typeof(PlayerEquipmentState))]
    [MemoryPackUnion(1, typeof(PlayerItemState))]
    [MemoryPackUnion(2, typeof(PlayerInputState))]
    [MemoryPackUnion(3, typeof(HotUpdate.Scripts.Network.State.PlayerPredictablePropertyState))]
    [MemoryPackUnion(4, typeof(PlayerShopState))]
    [MemoryPackUnion(5, typeof(PlayerSkillState))]
    public partial interface ISyncPropertyState
    {
        public PlayerSyncStateType GetStateType();
    }
    
    public enum PlayerSyncStateType
    {
        PlayerInput = 2,
        PlayerProperty = 3,
        PlayerItem = 1,
        PlayerEquipment = 0,
        PlayerShop = 4,
        PlayerSkill = 5,
    }
    
    [MemoryPackable(GenerateType.Collection)]
    public partial class MemoryDictionary<T1, T2> : Dictionary<T1, T2>
    {
        [MemoryPackOrder(0)] private T1[] _keys;
        [MemoryPackOrder(1)] private T2[] _values;

        public MemoryDictionary()
        {
            _keys = Array.Empty<T1>();
            _values = Array.Empty<T2>();
        }

        public MemoryDictionary(int capacity) : base(capacity)
        {
            _keys = new T1[capacity];
            _values = new T2[capacity];
        }

        public MemoryDictionary(IEqualityComparer<T1> comparer) : base(comparer)
        {
        }

        public MemoryDictionary(IDictionary<T1, T2> dictionary) : base(dictionary)
        {
            _keys = new T1[dictionary.Count];
            _values = new T2[dictionary.Count];
            int i = 0;
            foreach (var item in dictionary)
            {
                _keys[i] = item.Key;
                _values[i] = item.Value;
                i++;
            }
        }

        public MemoryDictionary(IEnumerable<KeyValuePair<T1, T2>> collection) : base(collection)
        {
            var dictionary = collection.ToDictionary(x => x.Key, x => x.Value);
            _keys = new T1[dictionary.Count];
            _values = new T2[dictionary.Count];
            int i = 0;
            foreach (var item in dictionary)
            {
                _keys[i] = item.Key;
                _values[i] = item.Value;
                i++;
            }
        }

        public MemoryDictionary(int capacity, IEqualityComparer<T1> comparer) : base(capacity, comparer)
        {
            _keys = new T1[capacity];
            _values = new T2[capacity];
        }

        public T1[] Keys
        {
            get => _keys;
            set => _keys = value;
        }

        public T2[] Values
        {
            get => _values;
            set => _values = value;
        }

        public override bool Equals(object obj)
        {
            if (obj is MemoryDictionary<T1, T2> other)
            {
                if (_keys == null || other._keys == null || _values == null || other._values == null)
                {
                    return base.Equals(obj);
                }

                if (_keys.Length != other._keys.Length || _values.Length != other._values.Length)
                {
                    return false;
                }

                for (int i = 0; i < _keys.Length; i++)
                {
                    if (!Equals(_keys[i], other._keys[i]) || !Equals(_values[i], other._values[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (_keys == null || _values == null)
            {
                return base.GetHashCode();
            }

            int hash = 17;
            for (int i = 0; i < _keys.Length; i++)
            {
                hash = hash * 31 + (_keys[i] == null ? 0 : _keys[i].GetHashCode());
                hash = hash * 31 + (_values[i] == null ? 0 : _values[i].GetHashCode());
            }

            return hash;
        }

        public static bool operator ==(MemoryDictionary<T1, T2> left, MemoryDictionary<T1, T2> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MemoryDictionary<T1, T2> left, MemoryDictionary<T1, T2> right)
        {
            return !Equals(left, right);
        }
    }
    
    [MemoryPackable(GenerateType.Collection)]
    public partial class MemoryList<T> : List<T>
    {
        [MemoryPackOrder(0)] private T[] _items;

        public MemoryList()
        {
            _items = Array.Empty<T>();
        }

        public MemoryList(int capacity) : base(capacity)
        {
            _items = new T[capacity];
        }

        public MemoryList(IEnumerable<T> collection) : base(collection)
        {
            _items = collection.ToArray();
        }

        public MemoryList(T[] items)
        {
            _items = items;
        }

        public T[] Items
        {
            get => _items;
            private set => _items = value;
        }

        public override bool Equals(object obj)
        {
            if (obj is MemoryList<T> other)
            {
                if (_items == null || other._items == null)
                {
                    return base.Equals(obj);
                }

                if (_items.Length != other._items.Length)
                {
                    return false;
                }

                for (int i = 0; i < _items.Length; i++)
                {
                    if (!Equals(_items[i], other._items[i]))
                    {
                        return false;
                    }
                }

                return true;
            }

            return base.Equals(obj);
        }

        public override int GetHashCode()
        {
            if (_items == null)
            {
                return base.GetHashCode();
            }

            int hash = 17;
            foreach (var item in _items)
            {
                hash = hash * 31 + (item == null ? 0 : item.GetHashCode());
            }

            return hash;
        }

        public static bool operator ==(MemoryList<T> left, MemoryList<T> right)
        {
            return Equals(left, right);
        }

        public static bool operator !=(MemoryList<T> left, MemoryList<T> right)
        {
            return !Equals(left, right);
        }
    }
}