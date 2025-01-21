using System;
using System.Collections.Generic;
using Mirror;

namespace HotUpdate.Scripts.Network.Data.PredictableObject
{
    public class PredictableValue<T> : PredictableSyncObject, IPredictableValueEvents<T>
    {
        private T _serverValue;
        private T _predictedValue;
        private bool _hasPrediction;

        public T Value 
        {
            get => _hasPrediction ? _predictedValue : _serverValue;
            set
            {
                if (AllowClientPredict)
                {
                    _predictedValue = value;
                    _hasPrediction = true;
                    IsDirty = true;
                }
            }
        }
        
        public void ClientSet(T value)
        {
            if (!AllowClientPredict) return;
            _predictedValue = value;
            _hasPrediction = true;
            IsDirty = true;
        }

        public void ServerSet(T value)
        {
            T oldValue = _serverValue;
            _serverValue = value;
            _predictedValue = value;
            _hasPrediction = false;
            IsDirty = true;
            if (!EqualityComparer<T>.Default.Equals(oldValue, value))
            {
                OnServerValueChanged?.Invoke(oldValue, value);
            }
        }

        public override void OnSerializeAll(NetworkWriter writer)
        {
            writer.Write(_serverValue);
        }

        public override void OnDeserializeAll(NetworkReader reader)
        {
            _serverValue = reader.Read<T>();
            _predictedValue = _serverValue;
            _hasPrediction = false;
        }

        public override void OnSerializeDelta(NetworkWriter writer)
        {
            writer.Write(_serverValue);
            IsDirty = false;
        }

        public override void OnDeserializeDelta(NetworkReader reader)
        {
            var value = reader.Read<T>();
            ServerSet(value); // 使用ServerSet来触发事件
        }

        public override void ResetSyncObjects()
        {
            _serverValue = default;
            _predictedValue = default;
            _hasPrediction = false;
            IsDirty = false;
        }

        public event Action<T, T> OnValueChanged;
        public event Action<T, T> OnServerValueChanged;
    }
}