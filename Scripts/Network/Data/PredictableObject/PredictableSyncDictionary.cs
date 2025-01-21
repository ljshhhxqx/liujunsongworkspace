using System;
using System.Collections.Generic;
using Mirror;

namespace HotUpdate.Scripts.Network.Data.PredictableObject
{
    public class PredictableSyncDictionary<TKey, TValue> : IPredictableSyncObject, IPredictableSyncEvents<TKey, TValue>
    {
        private readonly Dictionary<TKey, TValue> _objects = new Dictionary<TKey, TValue>();
        private readonly Dictionary<TKey, TValue> _predictedObjects = new Dictionary<TKey, TValue>();
        private readonly HashSet<TKey> _changedKeys = new HashSet<TKey>();
        
        public bool IsDirty { get; private set; }
        public bool AllowClientPredict { get; set; } = true;
        
        public bool ContainsKey(TKey key, bool isPredicted = false)
        {
            return isPredicted ? _predictedObjects.ContainsKey(key) : _objects.ContainsKey(key);
        }
        
        public void Add(TKey key, TValue value)
        {
            if (!AllowClientPredict) return;
        
            _predictedObjects[key] = value;
            _changedKeys.Add(key);
            IsDirty = true;
            OnAdd?.Invoke(key, value);
        }

        public bool Remove(TKey key)
        {
            if (!AllowClientPredict) return false;
        
            if (_predictedObjects.Remove(key, out var value))
            {
                _changedKeys.Add(key);
                IsDirty = true;
                OnRemove?.Invoke(key, value);
                return true;
            }
            return false;
        }

        public void Clear()
        {
            if (!AllowClientPredict) return;
        
            _predictedObjects.Clear();
            IsDirty = true;
            OnClear?.Invoke();
        }

        // 本地预测修改
        public void PredictSet(TKey key, TValue value)
        {
            if (!AllowClientPredict) return;

            _predictedObjects[key] = value;
            _changedKeys.Add(key);
            IsDirty = true;
        }

        // 服务器确认修改
        public void ServerSet(TKey key, TValue value)
        {
            TValue oldValue = _objects.ContainsKey(key) ? _objects[key] : default;
            bool isNewKey = !_objects.ContainsKey(key);

            _objects[key] = value;
            _predictedObjects[key] = value;
        
            if (isNewKey)
            {
                OnAdd?.Invoke(key, value);
            }
            else if (!EqualityComparer<TValue>.Default.Equals(oldValue, value))
            {
                OnValueChanged?.Invoke(key, oldValue, value);
            }
        }

        // 获取值（优先返回预测值）
        public TValue Get(TKey key)
        {
            if (_predictedObjects.TryGetValue(key, out var predictedValue))
                return predictedValue;
                
            return _objects.GetValueOrDefault(key);
        }

        // 序列化所有数据
        public void OnSerializeAll(NetworkWriter writer)
        {
            writer.WriteInt(_objects.Count);
            foreach (var kvp in _objects)
            {
                writer.Write(kvp.Key);
                writer.Write(kvp.Value);
            }
        }

        // 反序列化所有数据
        public void OnDeserializeAll(NetworkReader reader)
        {
            _objects.Clear();
            _predictedObjects.Clear();
            _changedKeys.Clear();
            
            var count = reader.ReadInt();
            for (var i = 0; i < count; i++)
            {
                var key = reader.Read<TKey>();
                var value = reader.Read<TValue>();
                _objects[key] = value;
                _predictedObjects[key] = value;
            }
        }

        // 序列化变化的数据
        public void OnSerializeDelta(NetworkWriter writer)
        {
            writer.WriteInt(_changedKeys.Count);
            foreach (var key in _changedKeys)
            {
                writer.Write(key);
                writer.Write(_objects[key]);
            }
            _changedKeys.Clear();
            IsDirty = false;
        }

        // 反序列化变化的数据
        public void OnDeserializeDelta(NetworkReader reader)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var key = reader.Read<TKey>();
                var value = reader.Read<TValue>();
                ServerSet(key, value); // 使用ServerSet来触发事件
            }
        }

        public void ResetSyncObjects()
        {
            _objects.Clear();
            _predictedObjects.Clear();
            _changedKeys.Clear();
            IsDirty = false;
        }

        public event Action<TKey, TValue> OnAdd;
        public event Action<TKey, TValue> OnRemove;
        public event Action<TKey, TValue, TValue> OnValueChanged;
        public event Action OnClear;
    }
}