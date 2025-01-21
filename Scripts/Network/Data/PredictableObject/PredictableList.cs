using System;
using System.Collections.Generic;
using Mirror;

namespace HotUpdate.Scripts.Network.Data.PredictableObject
{
    public class PredictableList<T> : PredictableSyncObject, IPredictableListEvents<T>
    {
        private readonly List<T> _serverList = new List<T>();
        private readonly List<T> _predictedList = new List<T>();
        private readonly HashSet<int> _changedIndexes = new HashSet<int>();

        public int Count => _predictedList.Count;

        public T this[int index]
        {
            get => _predictedList[index];
            set
            {
                if (AllowClientPredict)
                {
                    _predictedList[index] = value;
                    _changedIndexes.Add(index);
                    IsDirty = true;
                }
            }
        }
        
        public void Insert(int index, T item)
        {
            if (AllowClientPredict)
            {
                _predictedList.Insert(index, item);
                _changedIndexes.Add(index);
                IsDirty = true;
                OnInsert?.Invoke(index, item);
            }
        }

        public void RemoveAt(int index)
        {
            if (AllowClientPredict)
            {
                T oldItem = _predictedList[index];
                _predictedList.RemoveAt(index);
                IsDirty = true;
                OnRemoveAt?.Invoke(index, oldItem);
            
                // 更新changed indexes
                _changedIndexes.Clear();
                for (int i = index; i < _predictedList.Count; i++)
                {
                    _changedIndexes.Add(i);
                }
            }
        }

        public void Clear()
        {
            if (AllowClientPredict)
            {
                _predictedList.Clear();
                _changedIndexes.Clear();
                IsDirty = true;
                OnClear?.Invoke();
            }
        }

        public void Add(T item)
        {
            if (AllowClientPredict)
            {
                _predictedList.Add(item);
                _changedIndexes.Add(_predictedList.Count - 1);
                IsDirty = true;
            }
        }
        
        // 本地预测修改，不触发事件
        public void PredictSet(int index, T value)
        {
            if (!AllowClientPredict) return;

            while (_predictedList.Count <= index)
                _predictedList.Add(default);

            _predictedList[index] = value;
            _changedIndexes.Add(index);
            IsDirty = true;
        }

        public void ServerSet(int index, T item)
        {
            while (_serverList.Count <= index)
                _serverList.Add(default);

            T oldValue = _serverList[index];
            _serverList[index] = item;
            _predictedList[index] = item;

            if (!EqualityComparer<T>.Default.Equals(oldValue, item))
            {
                OnSet?.Invoke(index, oldValue, item);
            }
        }

        public override void OnSerializeAll(NetworkWriter writer)
        {
            writer.WriteInt(_serverList.Count);
            foreach (var item in _serverList)
            {
                writer.Write(item);
            }
        }

        public override void OnDeserializeAll(NetworkReader reader)
        {
            _serverList.Clear();
            _predictedList.Clear();
            _changedIndexes.Clear();
            
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                var item = reader.Read<T>();
                _serverList.Add(item);
                _predictedList.Add(item);
            }
        }

        public override void OnSerializeDelta(NetworkWriter writer)
        {
            writer.WriteInt(_changedIndexes.Count);
            foreach (var index in _changedIndexes)
            {
                writer.WriteInt(index);
                writer.Write(_serverList[index]);
            }
            _changedIndexes.Clear();
            IsDirty = false;
        }

        public override void OnDeserializeDelta(NetworkReader reader)
        {
            int count = reader.ReadInt();
            for (int i = 0; i < count; i++)
            {
                int index = reader.ReadInt();
                var value = reader.Read<T>();
                ServerSet(index, value); // 使用ServerSet来触发事件
            }
        }

        public override void ResetSyncObjects()
        {
            _serverList.Clear();
            _predictedList.Clear();
            _changedIndexes.Clear();
            IsDirty = false;
        }

        public event Action<int, T> OnInsert;
        public event Action<int, T> OnRemoveAt;
        public event Action<int, T, T> OnSet;
        public event Action OnClear;
    }
}